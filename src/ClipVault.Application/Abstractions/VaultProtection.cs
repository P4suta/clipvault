namespace ClipVault.Application.Abstractions;

/// <summary>The protection state of the vault.</summary>
public enum VaultProtection
{
    /// <summary>DPAPI only (no passphrase or Hello configured).</summary>
    DpapiOnly,

    /// <summary>DPAPI plus a passphrase (two factors).</summary>
    Passphrase,

    /// <summary>DPAPI plus Windows Hello (two factors).</summary>
    Hello,

    /// <summary>Volatile memory mode (the key is never on disk).</summary>
    Volatile,
}
