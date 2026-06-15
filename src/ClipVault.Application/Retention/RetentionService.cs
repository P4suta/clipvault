using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Policies;

namespace ClipVault.Application.Retention;

/// <summary>
/// Enforces the retention limits. It removes entries oldest-first, excluding pinned ones, in the order age, then count, then total size.
/// </summary>
/// <param name="repository">The clipboard history repository.</param>
/// <param name="policy">The retention policy used to determine expiry.</param>
/// <param name="settings">The retention settings (count and total-size limits).</param>
public sealed class RetentionService(
    IClipboardHistoryRepository repository,
    IRetentionPolicy policy,
    RetentionSettings settings)
{
    /// <summary>Runs the cleanup and returns the number of entries removed.</summary>
    /// <param name="now">The current time used to evaluate expiry.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that produces the number of entries removed.</returns>
    public async Task<int> EnforceAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var all = await repository.GetAllAsync(cancellationToken);
        var removed = 0;

        // 1) Past the age limit (unpinned).
        var expired = all.Where(entry => entry.IsExpired(policy, now)).ToList();
        foreach (var entry in expired)
        {
            await repository.RemoveAsync(entry.Id, cancellationToken);
            removed++;
        }

        // 2) Count and total-size limits (pinned entries are exempt; keep the most recent first).
        var unpinned = all
            .Where(entry => !entry.IsPinned && !entry.IsExpired(policy, now))
            .OrderByDescending(entry => entry.LastUsedAt)
            .ToList();

        var kept = 0;
        long cumulativeBytes = 0;
        foreach (var entry in unpinned)
        {
            var overCount = kept >= settings.MaxEntries;
            var overBytes = kept > 0 && cumulativeBytes + entry.SizeInBytes > settings.MaxTotalBytes;
            if (overCount || overBytes)
            {
                await repository.RemoveAsync(entry.Id, cancellationToken);
                removed++;
                continue;
            }

            kept++;
            cumulativeBytes += entry.SizeInBytes;
        }

        return removed;
    }
}
