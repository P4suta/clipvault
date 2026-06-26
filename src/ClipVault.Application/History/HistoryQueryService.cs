using ClipVault.Application.Insights;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.History;

/// <summary>
/// Retrieves and filters the history. The list is paged with a keyset cursor and filtered by streaming one batch
/// at a time, so the working set stays bounded regardless of how large the history grows. The search targets the
/// preview (the unencrypted beginning) and the originating application name; encrypted previews cannot be queried
/// with SQL, so matching is done in the application after decrypting each batch.
/// </summary>
/// <param name="repository">The clipboard history repository.</param>
public sealed class HistoryQueryService(IClipboardHistoryRepository repository)
{
    // Entries scanned per repository round-trip while filtering. One batch bounds the in-memory working set during
    // a query, independent of the total history size.
    private const int ScanBatch = 128;

    // Upper bound on how many (most-recent) entries the facet scan reads. Facets drive the filter dropdowns and are
    // recomputed on every structural reload, so the scan is capped to keep that automatic cost bounded regardless of
    // history size; the dropdowns then reflect recent history. (Search is uncapped — it is user-initiated.)
    private const int FacetScanLimit = 2000;

    /// <summary>Queries the whole history in memory, optionally filtering by search term, content type, content kind, and source application.</summary>
    /// <param name="search">The search term to match against the preview and source application name, or <see langword="null"/> for no text filter.</param>
    /// <param name="typeFilter">The content type to filter by, or <see langword="null"/> for no type filter.</param>
    /// <param name="kindFilter">The derived content kind to filter by, or <see langword="null"/> for no kind filter.</param>
    /// <param name="sourceApp">The source application process name to filter by, or <see langword="null"/> for no app filter.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that produces the matching entries.</returns>
    public async Task<IReadOnlyList<ClipboardEntry>> QueryAsync(
        string? search = null,
        ClipContentType? typeFilter = null,
        ContentKind? kindFilter = null,
        string? sourceApp = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new HistoryFilter(search, typeFilter, kindFilter, sourceApp);
        var all = await repository.GetAllAsync(cancellationToken);
        return all.Where(entry => Matches(entry, filter)).ToList();
    }

    /// <summary>
    /// Reads one filtered page of the history, starting strictly after the given cursor. Scans the keyset-paged
    /// repository one batch at a time, decrypting and matching each batch, until a page is filled or the source is
    /// exhausted. Memory stays bounded to a single batch; a highly selective filter costs more time, not more memory.
    /// </summary>
    /// <param name="filter">The filter criteria to apply.</param>
    /// <param name="after">The cursor to resume after, or <see langword="null"/> to start from the first entry.</param>
    /// <param name="pageSize">The number of matching entries to fill the page with.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The matching entries and the cursor to resume after (<see langword="null"/> once exhausted).</returns>
    public async Task<HistoryPage> QueryPageAsync(
        HistoryFilter filter, HistoryCursor? after, int pageSize, CancellationToken cancellationToken = default)
    {
        var matches = new List<ClipboardEntry>(pageSize);
        var cursor = after;

        while (matches.Count < pageSize)
        {
            var page = await repository.GetPageAsync(cursor, ScanBatch, cancellationToken);

            // Finish the whole batch before re-checking the count, so the resume cursor never skips an un-examined row.
            matches.AddRange(page.Entries.Where(entry => Matches(entry, filter)));

            cursor = page.NextCursor;
            if (cursor is null)
            {
                break;
            }
        }

        return new HistoryPage(matches, cursor);
    }

    /// <summary>Fetches one entry's thumbnail bytes on demand (used when a list row scrolls into view).</summary>
    /// <param name="id">The identifier of the entry whose thumbnail should be fetched.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The decrypted thumbnail bytes, or <see langword="null"/> when there is none.</returns>
    public Task<byte[]?> GetThumbnailAsync(EntryId id, CancellationToken cancellationToken = default) =>
        repository.GetThumbnailAsync(id, cancellationToken);

    /// <summary>Computes the distinct content kinds and source applications present across the whole history, for building filter options that actually exist.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that produces the available filter facets.</returns>
    public async Task<HistoryFacets> GetFacetsAsync(CancellationToken cancellationToken = default)
    {
        // Encrypted previews cannot be indexed, so facets require an O(n)-time decrypt pass; it streams one batch at
        // a time (O(1) memory) and is only run on structural reloads, never on every keystroke.
        var kinds = new HashSet<ContentKind>();
        var apps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allKinds = Enum.GetValues<ContentKind>().Length;

        HistoryCursor? cursor = null;
        var scanned = 0;
        do
        {
            var page = await repository.GetPageAsync(cursor, ScanBatch, cancellationToken);
            foreach (var entry in page.Entries)
            {
                if (kinds.Count < allKinds)
                {
                    kinds.Add(ContentInsightService.Classify(entry));
                }

                if (!string.IsNullOrWhiteSpace(entry.Source.ProcessName))
                {
                    apps.Add(entry.Source.ProcessName);
                }
            }

            scanned += page.Entries.Count;
            cursor = page.NextCursor;
        }
        while (cursor is not null && scanned < FacetScanLimit);

        return new HistoryFacets(
            kinds.OrderBy(kind => (int)kind).ToList(),
            apps.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    // Shared filter predicate for both the in-memory QueryAsync and the streaming QueryPageAsync, so they match
    // identically. The QueryAsync tests serve as the executable specification for this predicate.
    private static bool Matches(ClipboardEntry entry, HistoryFilter filter)
    {
        if (filter.Type is { } type && entry.ContentType != type)
        {
            return false;
        }

        if (filter.Kind is { } kind && ContentInsightService.Classify(entry) != kind)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.SourceApp)
            && !string.Equals(entry.Source.ProcessName, filter.SourceApp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(filter.Search))
        {
            return true;
        }

        var term = filter.Search.Trim();
        return entry.Preview.Contains(term, StringComparison.OrdinalIgnoreCase)
            || entry.Source.ProcessName.Contains(term, StringComparison.OrdinalIgnoreCase);
    }
}
