using ClipVault.Domain.Entities;

namespace ClipVault.Domain.Policies;

/// <summary>
/// Decides whether a single entry has expired (age-based). The count and total-size limits require
/// ordering across all entries, so they are handled by the retention service in the application layer.
/// </summary>
public interface IRetentionPolicy
{
    /// <summary>Determines whether the specified entry should be evicted.</summary>
    /// <param name="entry">The entry to evaluate.</param>
    /// <param name="now">The current time used to evaluate the entry's age.</param>
    /// <returns><see langword="true"/> if the entry should be evicted; otherwise, <see langword="false"/>.</returns>
    bool ShouldEvict(ClipboardEntry entry, DateTimeOffset now);
}
