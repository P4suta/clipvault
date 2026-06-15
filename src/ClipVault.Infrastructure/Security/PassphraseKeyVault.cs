using ClipVault.Application.Abstractions;
using ClipVault.Infrastructure.Persistence;

namespace ClipVault.Infrastructure.Security;

/// <summary>
/// The key vault for disk mode when passphrase protection is enabled. It decrypts the DEK with the passphrase
/// entered at startup. If the passphrase is missing or incorrect, <see cref="KeyProtector.Unlock"/> throws.
/// </summary>
/// <param name="options">The storage options that provide the key file path.</param>
/// <param name="passphrases">The provider that supplies the startup passphrase.</param>
public sealed class PassphraseKeyVault(ClipVaultStorageOptions options, IPassphraseProvider passphrases) : IKeyVault
{
    /// <inheritdoc/>
    public byte[] GetOrCreateMasterKey()
    {
        var protector = new KeyProtector(options.KeyFilePath);
        return protector.Exists()
            ? protector.Unlock(passphrases.Passphrase)
            : protector.CreateNew(passphrases.Passphrase);
    }
}
