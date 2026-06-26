using ClipVault.Domain.Entities;

namespace ClipVault.Domain.Policies;

/// <summary>The default expiry decision based on a maximum age, exempting pinned entries.</summary>
/// <param name="settings">The retention settings that define the maximum age.</param>
public sealed class DefaultRetentionPolicy(RetentionSettings settings) : IRetentionPolicy
{
    /// <inheritdoc/>
    public bool ShouldEvict(ClipboardEntry entry, DateTimeOffset now) =>
        !entry.IsPinned && entry.CapturedAt < EvictionCutoff(now);

    /// <inheritdoc/>
    public DateTimeOffset EvictionCutoff(DateTimeOffset now) => now - settings.MaxAge;
}
