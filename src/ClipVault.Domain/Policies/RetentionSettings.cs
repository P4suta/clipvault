namespace ClipVault.Domain.Policies;

/// <summary>The upper-bound settings for history retention. Pinned entries are exempt.</summary>
public sealed record RetentionSettings
{
    /// <summary>Gets the default retention settings.</summary>
    public static RetentionSettings Default { get; } = new();

    /// <summary>Gets the maximum age beyond which unpinned entries expire.</summary>
    public TimeSpan MaxAge { get; init; } = TimeSpan.FromDays(30);

    /// <summary>Gets the maximum number of entries to retain (the oldest excess entries are removed).</summary>
    public int MaxEntries { get; init; } = 500;

    /// <summary>Gets the maximum total number of bytes to retain (the oldest excess entries are removed).</summary>
    public long MaxTotalBytes { get; init; } = 256L * 1024 * 1024;
}
