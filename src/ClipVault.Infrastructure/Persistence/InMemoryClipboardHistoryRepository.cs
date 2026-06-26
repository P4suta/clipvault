using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.Policies;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Infrastructure.Persistence;

/// <summary>
/// A volatile repository that keeps the history only in RAM. Because it never writes to disk, it disappears
/// entirely when the app exits. (Volatile-memory mode. Encryption is unnecessary, since both the key and the
/// content live in the same RAM.) It is a bounded ring: every insert evicts the oldest unpinned entries until
/// the configured count and byte budget are met, so RAM use stays capped no matter how much is copied.
/// </summary>
/// <param name="settings">The retention budget that bounds the in-memory ring; defaults to <see cref="RetentionSettings.Default"/>.</param>
public sealed class InMemoryClipboardHistoryRepository(RetentionSettings? settings = null) : IClipboardHistoryRepository
{
    private readonly RetentionSettings _settings = settings ?? RetentionSettings.Default;
    private readonly Lock _gate = new();
    private readonly Dictionary<EntryId, Record> _byId = [];
    private readonly Dictionary<ContentHash, Record> _byHash = [];

    // Running total of the stored payload sizes, kept in sync on add/remove/clear so the byte budget is O(1) to check.
    private long _totalBytes;

    /// <inheritdoc/>
    public Task<ClipboardEntry?> FindByHashAsync(ContentHash hash, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_byHash.TryGetValue(hash, out var record) ? record.Entry : null);
        }
    }

    /// <inheritdoc/>
    public Task AddAsync(ClipboardEntry entry, ClipContent content, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var record = new Record(entry, content);
            _byId[entry.Id] = record;
            _byHash[entry.Hash] = record;
            _totalBytes += entry.SizeInBytes;
            EvictToBudgetUnderLock();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UpdateAsync(ClipboardEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask; // No-op: entries are held by mutable reference, so pin-state/last-used changes are already reflected.

    /// <inheritdoc/>
    public Task RemoveAsync(EntryId id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_byId.Remove(id, out var record))
            {
                _byHash.Remove(record.Entry.Hash);
                _totalBytes -= record.Entry.SizeInBytes;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ClipboardEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var ordered = _byId.Values
                .Select(record => record.Entry)
                .OrderByDescending(entry => entry.IsPinned)
                .ThenByDescending(entry => entry.LastUsedAt)
                .ToList();
            return Task.FromResult<IReadOnlyList<ClipboardEntry>>(ordered);
        }
    }

    /// <inheritdoc/>
    public Task<HistoryPage> GetPageAsync(HistoryCursor? after, int limit, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var query = _byId.Values.Select(record => record.Entry);
            if (after is { } cursor)
            {
                query = query.Where(entry => IsAfter(cursor, entry));
            }

            var page = query
                .OrderByDescending(entry => entry.IsPinned)
                .ThenByDescending(entry => entry.LastUsedAt)
                .ThenBy(entry => entry.Id.Value.ToByteArray(), ByteLexComparer.Instance)
                .Take(limit)
                .ToList();

            var next = page.Count == limit ? HistoryCursor.After(page[^1]) : (HistoryCursor?)null;
            return Task.FromResult(new HistoryPage(page, next));
        }
    }

    /// <inheritdoc/>
    public Task<byte[]?> GetThumbnailAsync(EntryId id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            // Hand back a caller-owned copy so decoding cannot mutate the bytes retained in the store.
            var thumbnail = _byId.TryGetValue(id, out var record) ? record.Entry.Image?.Thumbnail : null;
            return Task.FromResult(thumbnail is { Length: > 0 } ? (byte[])thumbnail.Clone() : null);
        }
    }

    /// <inheritdoc/>
    public Task<ClipContent?> MaterializeAsync(EntryId id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            // Hand back a caller-owned copy: the consumer disposes (zeroes) the materialized content,
            // which must not corrupt the instance retained in the store.
            var result = _byId.TryGetValue(id, out var record)
                ? new ClipContent(record.Content.Type, (byte[])record.Content.Payload.Clone())
                : null;
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc/>
    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_byId.Count);
        }
    }

    /// <inheritdoc/>
    public Task<int> DeleteExpiredAsync(DateTimeOffset capturedBefore, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var expired = _byId.Values
                .Select(record => record.Entry)
                .Where(entry => !entry.IsPinned && entry.CapturedAt < capturedBefore)
                .ToList();

            foreach (var entry in expired)
            {
                if (_byId.Remove(entry.Id, out var record))
                {
                    _byHash.Remove(record.Entry.Hash);
                    _totalBytes -= record.Entry.SizeInBytes;
                }
            }

            return Task.FromResult(expired.Count);
        }
    }

    /// <inheritdoc/>
    public Task<int> TrimAsync(int maxEntries, long maxTotalBytes, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            // Mirror of the SQLite window-function trim: rank unpinned most-recently-used first (id as the
            // tiebreak, ordered the way SQLite compares the id BLOB), accumulate a running size sum, and evict
            // those past the count budget or the byte budget. Rank 1 is always kept.
            var unpinned = _byId.Values
                .Select(record => record.Entry)
                .Where(entry => !entry.IsPinned)
                .OrderByDescending(entry => entry.LastUsedAt)
                .ThenBy(entry => entry.Id.Value.ToByteArray(), ByteLexComparer.Instance)
                .ToList();

            var removed = 0;
            long running = 0;
            for (var rn = 1; rn <= unpinned.Count; rn++)
            {
                var entry = unpinned[rn - 1];
                running += entry.SizeInBytes;
                if ((rn > maxEntries || (rn > 1 && running > maxTotalBytes)) && _byId.Remove(entry.Id, out var record))
                {
                    _byHash.Remove(record.Entry.Hash);
                    _totalBytes -= record.Entry.SizeInBytes;
                    removed++;
                }
            }

            return Task.FromResult(removed);
        }
    }

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _byId.Clear();
            _byHash.Clear();
            _totalBytes = 0;
        }

        return Task.CompletedTask;
    }

    // Strictly-after-cursor predicate: the line-by-line image of the SQLite keyset WHERE clause. A row sorts after
    // the cursor when it is less pinned, or equally pinned but older, or equal on both but with a larger id.
    private static bool IsAfter(HistoryCursor cursor, ClipboardEntry entry)
    {
        if (entry.IsPinned != cursor.IsPinned)
        {
            return cursor.IsPinned && !entry.IsPinned;
        }

        if (entry.LastUsedAt != cursor.LastUsedAt)
        {
            return entry.LastUsedAt < cursor.LastUsedAt;
        }

        return ByteLexComparer.Instance.Compare(entry.Id.Value.ToByteArray(), cursor.Id.Value.ToByteArray()) > 0;
    }

    // Bounded RAM ring: after each insert, evict the oldest unpinned entries until both the count and the byte
    // budget are satisfied. This is the immediate hard ceiling that keeps volatile mode from growing without
    // bound, independent of the periodic retention sweep. Pinned entries are exempt (kept even when over budget).
    private void EvictToBudgetUnderLock()
    {
        if (_byId.Count <= _settings.MaxEntries && _totalBytes <= _settings.MaxTotalBytes)
        {
            return;
        }

        foreach (var entry in _byId.Values
                     .Select(record => record.Entry)
                     .Where(entry => !entry.IsPinned)
                     .OrderBy(entry => entry.LastUsedAt)
                     .ToList())
        {
            if (_byId.Count <= _settings.MaxEntries && _totalBytes <= _settings.MaxTotalBytes)
            {
                break;
            }

            if (_byId.Remove(entry.Id, out var record))
            {
                _byHash.Remove(record.Entry.Hash);
                _totalBytes -= record.Entry.SizeInBytes;
            }
        }
    }

    // Orders Guid bytes the way SQLite compares the id BLOB (unsigned, memcmp-style), so the in-memory tiebreak
    // matches the SQLite ordering. Guid.CompareTo must NOT be used here: its field-wise order differs from memcmp.
    private sealed class ByteLexComparer : IComparer<byte[]>
    {
        public static readonly ByteLexComparer Instance = new();

        public int Compare(byte[]? x, byte[]? y) => x.AsSpan().SequenceCompareTo(y.AsSpan());
    }

    private sealed record Record(ClipboardEntry Entry, ClipContent Content);
}
