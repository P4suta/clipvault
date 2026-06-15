using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Infrastructure.Persistence;

/// <summary>
/// A volatile repository that keeps the history only in RAM. Because it never writes to disk, it disappears
/// entirely when the app exits. (Volatile-memory mode. Encryption is unnecessary, since both the key and the
/// content live in the same RAM.)
/// </summary>
public sealed class InMemoryClipboardHistoryRepository : IClipboardHistoryRepository
{
    private readonly Lock _gate = new();
    private readonly Dictionary<EntryId, Record> _byId = [];
    private readonly Dictionary<ContentHash, Record> _byHash = [];

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
    public Task<ClipContent?> MaterializeAsync(EntryId id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            // Hand back a caller-owned copy: the consumer disposes (zeroes) the materialized content,
            // which must not corrupt the instance retained in the store.
            ClipContent? result = _byId.TryGetValue(id, out var record)
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
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _byId.Clear();
            _byHash.Clear();
        }

        return Task.CompletedTask;
    }

    private sealed record Record(ClipboardEntry Entry, ClipContent Content);
}
