using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Domain.Abstractions;

/// <summary>
/// A keyset pagination cursor identifying a position in the history's stable sort order (pinned first, then
/// most-recently-used, then id). Paging strictly after a cursor is O(log n + page) and stays correct under
/// concurrent inserts and deletes, unlike an offset.
/// </summary>
/// <param name="IsPinned">The pinned flag of the row at the cursor.</param>
/// <param name="LastUsedAt">The last-used time of the row at the cursor.</param>
/// <param name="Id">The identifier of the row at the cursor (the total-order tiebreaker).</param>
public readonly record struct HistoryCursor(bool IsPinned, DateTimeOffset LastUsedAt, EntryId Id)
{
    /// <summary>Creates the cursor positioned at the given entry, so the next page starts strictly after it.</summary>
    /// <param name="entry">The entry whose sort position becomes the cursor.</param>
    /// <returns>A cursor at the entry's sort position.</returns>
    public static HistoryCursor After(ClipboardEntry entry) => new(entry.IsPinned, entry.LastUsedAt, entry.Id);
}
