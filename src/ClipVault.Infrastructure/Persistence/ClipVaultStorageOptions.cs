using ClipVault.Application.Abstractions;

namespace ClipVault.Infrastructure.Persistence;

/// <summary>
/// Configuration for persistence and the placement of the key file.
/// </summary>
public sealed record ClipVaultStorageOptions
{
    /// <summary>
    /// Gets the storage mode (volatile memory or encrypted disk persistence). The default is volatile (privacy first).
    /// </summary>
    public StorageMode Storage { get; init; } = StorageMode.VolatileMemory;

    /// <summary>
    /// Gets the path to the SQLite database (":memory:" in tests; unused in volatile mode).
    /// </summary>
    public required string DatabasePath { get; init; }

    /// <summary>
    /// Gets the path to the master key file sealed with DPAPI (unused in volatile mode).
    /// </summary>
    public required string KeyFilePath { get; init; }

    /// <summary>
    /// Creates the default configuration that uses the per-channel folder under %LOCALAPPDATA%\ClipVault
    /// (see <see cref="AppPaths.LocalAppDataRoot"/>), so non-stable builds never touch the released data.
    /// </summary>
    /// <returns>The default storage options.</returns>
    public static ClipVaultStorageOptions Default()
    {
        var dir = AppPaths.LocalAppDataRoot();
        return new ClipVaultStorageOptions
        {
            DatabasePath = Path.Combine(dir, "history.db"),
            KeyFilePath = Path.Combine(dir, "dek.bin"),
        };
    }
}
