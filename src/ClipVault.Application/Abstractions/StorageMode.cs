namespace ClipVault.Application.Abstractions;

/// <summary>The storage mode for the history.</summary>
public enum StorageMode
{
    /// <summary>Persisted to SQLite in encrypted form (the default). Survives a restart.</summary>
    EncryptedDisk,

    /// <summary>
    /// Volatile memory: nothing is ever written to disk and data is kept only in RAM. The key is also confined to RAM
    /// and is completely gone when the application exits (the most secure option).
    /// </summary>
    VolatileMemory,
}
