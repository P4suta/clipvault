namespace ClipVault.Application.Abstractions;

/// <summary>The storage mode for the history.</summary>
public enum StorageMode
{
    /// <summary>Persisted to SQLite in encrypted form. Survives a restart (opt-in).</summary>
    EncryptedDisk,

    /// <summary>
    /// Volatile memory: nothing is ever written to disk and data is kept only in RAM. The key is also confined to RAM
    /// and is completely gone when the application exits (the default; strongest privacy posture).
    /// </summary>
    VolatileMemory,
}
