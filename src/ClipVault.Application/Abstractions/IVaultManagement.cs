namespace ClipVault.Application.Abstractions;

/// <summary>Operations on the vault (master key): configuring and removing passphrase or Hello protection, and cryptographic erase.</summary>
public interface IVaultManagement
{
    /// <summary>Gets the current protection state of the vault.</summary>
    VaultProtection Protection { get; }

    /// <summary>Determines whether Windows Hello is available.</summary>
    /// <returns>A task that produces <see langword="true"/> when Windows Hello is available; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsHelloAvailableAsync();

    /// <summary>Sets or changes the passphrase (pass <see langword="null"/> for the current value when none is set).</summary>
    /// <param name="currentPassphrase">The current passphrase, or <see langword="null"/> when none is set.</param>
    /// <param name="newPassphrase">The new passphrase to set.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SetOrChangePassphraseAsync(string? currentPassphrase, string newPassphrase, CancellationToken cancellationToken = default);

    /// <summary>Removes passphrase protection and reverts to DPAPI only.</summary>
    /// <param name="currentPassphrase">The current passphrase.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RemovePassphraseAsync(string currentPassphrase, CancellationToken cancellationToken = default);

    /// <summary>Enables Windows Hello protection (the current passphrase is required when the current protection is a passphrase).</summary>
    /// <param name="currentPassphrase">The current passphrase, or <see langword="null"/> when none is set.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task EnableHelloAsync(string? currentPassphrase = null, CancellationToken cancellationToken = default);

    /// <summary>Disables Windows Hello protection and reverts to DPAPI only (Hello authentication is required).</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DisableHelloAsync(CancellationToken cancellationToken = default);

    /// <summary>Erases all history and destroys the key (cryptographic erase: everything is irrecoverable afterwards).</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task PanicWipeAsync(CancellationToken cancellationToken = default);
}
