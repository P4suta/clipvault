using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Policies;

namespace ClipVault.Application.Retention;

/// <summary>
/// Enforces the retention limits by delegating to the repository's content-free eviction primitives: first the
/// age cutoff, then the count and total-size budget. Nothing is materialized into memory, so the sweep stays
/// bounded regardless of how large the history is.
/// </summary>
/// <param name="repository">The clipboard history repository.</param>
/// <param name="policy">The retention policy that provides the age-based eviction cutoff.</param>
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
        var removed = await repository.DeleteExpiredAsync(policy.EvictionCutoff(now), cancellationToken);
        removed += await repository.TrimAsync(settings.MaxEntries, settings.MaxTotalBytes, cancellationToken);
        return removed;
    }
}
