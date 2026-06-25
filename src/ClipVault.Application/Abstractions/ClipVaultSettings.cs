namespace ClipVault.Application.Abstractions;

/// <summary>User settings. Privacy-related defaults are consolidated here.</summary>
public sealed record ClipVaultSettings
{
    /// <summary>Gets the process names of password managers and similar applications that are excluded by default.</summary>
    public static IReadOnlySet<string> DefaultExclusions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "keepass", "keepassxc", "1password", "1passwordcli", "agilebits",
            "bitwarden", "dashlane", "lastpass", "nordpass", "protonpass",
            "enpass", "roboform", "keeper",
        };

    /// <summary>Gets the default settings instance.</summary>
    public static ClipVaultSettings Default { get; } = new();

    /// <summary>
    /// Gets the storage mode (volatile memory or encrypted disk persistence). Changes take effect after a restart.
    /// The default is volatile: simply starting the application leaves nothing on disk (privacy first). Persistence
    /// is an explicit opt-in.
    /// </summary>
    public StorageMode Storage { get; init; } = StorageMode.VolatileMemory;

    /// <summary>Gets the process names excluded from capture (case-insensitive).</summary>
    public IReadOnlySet<string> ExcludedProcessNames { get; init; } = DefaultExclusions;

    /// <summary>Gets a value indicating whether strings that look like generic passwords are masked (off by default because of frequent false positives).</summary>
    public bool MaskGenericPasswords { get; init; }

    /// <summary>Gets a value indicating whether tracking parameters (utm_, fbclid, ...) are stripped from captured URLs (off by default; opt-in).</summary>
    public bool StripTrackingParameters { get; init; }

    /// <summary>Gets the maximum number of bytes for a captured image (anything larger is discarded).</summary>
    public long MaxImageBytes { get; init; } = 10L * 1024 * 1024;

    /// <summary>Gets the maximum number of days to retain entries (unpinned entries older than this are removed). Changes take effect after a restart.</summary>
    public int MaxAgeDays { get; init; } = 30;

    /// <summary>Gets the maximum number of entries to retain. Changes take effect after a restart.</summary>
    public int MaxEntries { get; init; } = 500;

    /// <summary>Gets a value indicating whether the application starts automatically when the user logs on to Windows.</summary>
    public bool RunAtStartup { get; init; }

    /// <summary>Gets the UI language. The default follows the OS display language. Changes take effect after a restart.</summary>
    public AppLanguage Language { get; init; } = AppLanguage.System;

    /// <summary>Gets the UI theme. The default follows the OS theme. Changes are applied immediately (no restart).</summary>
    public AppTheme Theme { get; init; } = AppTheme.System;
}
