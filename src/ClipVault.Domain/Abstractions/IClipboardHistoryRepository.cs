using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Domain.Abstractions;

/// <summary>
/// A port for persisting history. Encryption is contained within this implementation (Infrastructure),
/// and callers always deal with plaintext domain objects. The full-size content is fetched lazily via
/// <see cref="MaterializeAsync"/>.
/// </summary>
public interface IClipboardHistoryRepository
{
    /// <summary>Performs duplicate detection by returning the existing entry whose keyed hash matches (or <see langword="null"/> if none).</summary>
    /// <param name="hash">The keyed hash to look up.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The matching entry, or <see langword="null"/> if no entry matches.</returns>
    Task<ClipboardEntry?> FindByHashAsync(ContentHash hash, CancellationToken cancellationToken = default);

    /// <summary>Stores a new entry together with its content (the content is stored encrypted).</summary>
    /// <param name="entry">The entry metadata to store.</param>
    /// <param name="content">The content to store, encrypted at rest.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task AddAsync(ClipboardEntry entry, ClipContent content, CancellationToken cancellationToken = default);

    /// <summary>Persists metadata changes such as the pinned state or the last-used time.</summary>
    /// <param name="entry">The entry whose metadata changes should be persisted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task UpdateAsync(ClipboardEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Removes the entry with the specified identifier.</summary>
    /// <param name="id">The identifier of the entry to remove.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RemoveAsync(EntryId id, CancellationToken cancellationToken = default);

    /// <summary>Gets all entries, newest first (pinned entries first). Searching is performed in memory.</summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A read-only list of all entries, newest first with pinned entries first.</returns>
    Task<IReadOnlyList<ClipboardEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one page of entries in the history's sort order (pinned first, then most-recently-used, then id),
    /// starting strictly after the given cursor (from the start when it is <see langword="null"/>). Thumbnail bytes
    /// are not loaded; fetch them on demand via <see cref="GetThumbnailAsync"/>. Keeps memory bounded to one page.
    /// </summary>
    /// <param name="after">The cursor to resume after, or <see langword="null"/> to start from the first entry.</param>
    /// <param name="limit">The maximum number of entries to return.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The page of entries and the cursor to resume after.</returns>
    Task<HistoryPage> GetPageAsync(HistoryCursor? after, int limit, CancellationToken cancellationToken = default);

    /// <summary>Decrypts and returns one entry's thumbnail bytes, or <see langword="null"/> when it has no thumbnail or no longer exists.</summary>
    /// <param name="id">The identifier of the entry whose thumbnail should be fetched.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The decrypted thumbnail bytes, or <see langword="null"/>.</returns>
    Task<byte[]?> GetThumbnailAsync(EntryId id, CancellationToken cancellationToken = default);

    /// <summary>Decrypts and returns the full-size content (for paste-back or full-size preview).</summary>
    /// <param name="id">The identifier of the entry whose content should be materialized.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The decrypted content, or <see langword="null"/> if the entry no longer exists.</returns>
    Task<ClipContent?> MaterializeAsync(EntryId id, CancellationToken cancellationToken = default);

    /// <summary>Counts the number of stored entries.</summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The number of stored entries.</returns>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all unpinned entries captured strictly before the given cutoff. Pinned entries are exempt.
    /// Implementations evict without materializing content (retention must stay bounded in memory).
    /// </summary>
    /// <param name="capturedBefore">The exclusive capture-time upper bound for removal.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The number of entries removed.</returns>
    Task<int> DeleteExpiredAsync(DateTimeOffset capturedBefore, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trims unpinned entries so that, ordered most-recently-used first, the kept set stays within both the entry
    /// count and the cumulative byte budget; the single most-recently-used unpinned entry is always kept. Pinned
    /// entries are exempt and do not count toward either budget. Implementations evict without materializing content.
    /// </summary>
    /// <param name="maxEntries">The maximum number of unpinned entries to keep.</param>
    /// <param name="maxTotalBytes">The maximum cumulative size in bytes of the kept unpinned entries.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The number of entries removed.</returns>
    Task<int> TrimAsync(int maxEntries, long maxTotalBytes, CancellationToken cancellationToken = default);

    /// <summary>Removes all stored entries.</summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
