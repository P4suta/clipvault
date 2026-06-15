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
    /// <summary>Queries the history, optionally filtering by search term and content type.</summary>
    /// <param name="search">The search term to match against the preview and source application name, or <see langword="null"/> for no text filter.</param>
    /// <param name="typeFilter">The content type to filter by, or <see langword="null"/> for no type filter.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that produces the matching entries.</returns>
    public async Task<IReadOnlyList<ClipboardEntry>> QueryAsync(
        string? search = null,
        ClipContentType? typeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var all = await repository.GetAllAsync(cancellationToken);
        IEnumerable<ClipboardEntry> query = all;

        if (typeFilter is { } type)
        {
            query = query.Where(entry => entry.ContentType == type);
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
}
