using ClipVault.Application.Insights;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.History;

/// <summary>
/// Retrieves and filters the history. Because the entry count is on the order of a few hundred, the search runs in
/// memory. The search targets the preview (the unencrypted beginning) and the originating application name.
/// </summary>
/// <param name="repository">The clipboard history repository.</param>
public sealed class HistoryQueryService(IClipboardHistoryRepository repository)
{
    /// <summary>Queries the history, optionally filtering by search term, content type, content kind, and source application.</summary>
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
        var all = await repository.GetAllAsync(cancellationToken);
        IEnumerable<ClipboardEntry> query = all;

        if (typeFilter is { } type)
        {
            query = query.Where(entry => entry.ContentType == type);
        }

        if (kindFilter is { } kind)
        {
            query = query.Where(entry => ContentInsightService.Classify(entry) == kind);
        }

        if (!string.IsNullOrWhiteSpace(sourceApp))
        {
            query = query.Where(entry =>
                string.Equals(entry.Source.ProcessName, sourceApp, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(entry =>
                entry.Preview.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                entry.Source.ProcessName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return query.ToList();
    }

    /// <summary>Computes the distinct content kinds and source applications present across the whole history, for building filter options that actually exist.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that produces the available filter facets.</returns>
    public async Task<HistoryFacets> GetFacetsAsync(CancellationToken cancellationToken = default)
    {
        var all = await repository.GetAllAsync(cancellationToken);

        var kinds = all
            .Select(ContentInsightService.Classify)
            .Distinct()
            .OrderBy(kind => (int)kind)
            .ToList();

        var apps = all
            .Select(entry => entry.Source.ProcessName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new HistoryFacets(kinds, apps);
    }
}
