using ClipVault.Application.Abstractions;
using ClipVault.Domain.Abstractions;
using ClipVault.Infrastructure.Persistence;

namespace ClipVault.Infrastructure.Security;

/// <summary>
/// Vault operations. Provides setting and removing passphrase or Windows Hello protection (via
/// <see cref="KeyProtector"/>; because the DEK is immutable, encryption in progress can continue) and a panic
/// wipe (erase everything plus destroy the key).
/// </summary>
/// <param name="options">The storage options that determine the mode and key file path.</param>
/// <param name="repository">The clipboard history repository used by the panic wipe.</param>
/// <param name="hello">The Windows Hello service used for Hello protection.</param>
public sealed class VaultManagement(
    ClipVaultStorageOptions options,
    IClipboardHistoryRepository repository,
    IWindowsHello hello) : IVaultManagement
{
    /// <inheritdoc/>
    public VaultProtection Protection
    {
        get
        {
            if (options.Storage == StorageMode.VolatileMemory)
            {
                return VaultProtection.Volatile;
            }

            var protector = new KeyProtector(options.KeyFilePath);
            if (!protector.Exists())
            {
                return VaultProtection.DpapiOnly;
            }

            if (protector.RequiresPassphrase())
            {
                return VaultProtection.Passphrase;
            }

            return protector.RequiresHello() ? VaultProtection.Hello : VaultProtection.DpapiOnly;
        }
    }

    /// <inheritdoc/>
    public Task<bool> IsHelloAvailableAsync() => hello.IsAvailableAsync();

    /// <inheritdoc/>
    public Task SetOrChangePassphraseAsync(
        string? currentPassphrase, string newPassphrase, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(newPassphrase);
        EnsureDiskMode();

        // Argon2id is CPU-intensive, so run it on a separate thread to avoid blocking the UI.
        return Task.Run(
            () => new KeyProtector(options.KeyFilePath).ChangePassphrase(currentPassphrase, newPassphrase),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task RemovePassphraseAsync(string currentPassphrase, CancellationToken cancellationToken = default)
    {
        EnsureDiskMode();
        return Task.Run(
            () => new KeyProtector(options.KeyFilePath).ChangePassphrase(currentPassphrase, newPassphrase: null),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task EnableHelloAsync(string? currentPassphrase = null, CancellationToken cancellationToken = default)
    {
        EnsureDiskMode();
        var protector = new KeyProtector(options.KeyFilePath);

        // Undo the current protection (DPAPI-only or passphrase) to extract the DEK, then rewrite it with Hello protection.
        var dek = await Task.Run(() => protector.Unlock(currentPassphrase), cancellationToken);
        try
        {
            await protector.WriteHelloAsync(dek, hello);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(dek);
        }
    }

    /// <inheritdoc/>
    public async Task DisableHelloAsync(CancellationToken cancellationToken = default)
    {
        EnsureDiskMode();
        var protector = new KeyProtector(options.KeyFilePath);
        var dek = await protector.UnlockWithHelloAsync(hello);
        try
        {
            protector.Write(dek, passphrase: null);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(dek);
        }
    }

    /// <inheritdoc/>
    public async Task PanicWipeAsync(CancellationToken cancellationToken = default)
    {
        await repository.ClearAsync(cancellationToken);
        if (options.Storage == StorageMode.EncryptedDisk)
        {
            new KeyProtector(options.KeyFilePath).CryptoErase();
        }
    }

    private void EnsureDiskMode()
    {
        if (options.Storage != StorageMode.EncryptedDisk)
        {
            throw new InvalidOperationException("Key protection cannot be changed in volatile-memory mode.");
        }
    }
}
