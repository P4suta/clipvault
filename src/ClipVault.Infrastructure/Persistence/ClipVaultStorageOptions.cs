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
    /// Creates the default configuration that uses a folder under %LOCALAPPDATA%\ClipVault.
    /// </summary>
    /// <returns>The default storage options.</returns>
    public static ClipVaultStorageOptions Default()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipVault");
        return new ClipVaultStorageOptions
        {
            DatabasePath = Path.Combine(dir, "history.db"),
            KeyFilePath = Path.Combine(dir, "dek.bin"),
        };
    }
}
