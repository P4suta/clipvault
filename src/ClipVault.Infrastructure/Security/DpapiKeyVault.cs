using ClipVault.Infrastructure.Persistence;

namespace ClipVault.Infrastructure.Security;

/// <summary>
/// The key vault for disk-persistent mode under DPAPI-only protection (the default protector). Using
/// <see cref="KeyProtector"/>, it generates the DEK on first use and seals it with DPAPI (CurrentUser), then
/// decrypts and returns it afterwards (no passphrase).
/// Migrating to passphrase protection or crypto-erasing is handled by higher-level features that operate on
/// <see cref="KeyProtector"/> directly.
/// </summary>
/// <param name="options">The storage options that provide the key file path.</param>
public sealed class DpapiKeyVault(ClipVaultStorageOptions options) : IKeyVault
{
    /// <inheritdoc/>
    public byte[] GetOrCreateMasterKey()
    {
        var protector = new KeyProtector(options.KeyFilePath);
        return protector.Exists() ? protector.Unlock(passphrase: null) : protector.CreateNew(passphrase: null);
    }
}
