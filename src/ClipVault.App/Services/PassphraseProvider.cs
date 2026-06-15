using ClipVault.Application.Abstractions;

namespace ClipVaultApp.Services;

/// <summary>
/// Mutable holder for the validated master passphrase captured at startup.
/// In disk mode with passphrase protection, the key vault (the <see cref="IPassphraseProvider"/> consumer)
/// uses it to decrypt the DEK. It is null in volatile mode or when unset. Registered as a singleton in DI.
/// </summary>
/// <remarks>
/// Held as an immutable string and cannot be zeroed (a documented, accepted residue; it originates in WinUI's
/// <c>PasswordBox</c>). In memory only while the vault is unlocked.
/// </remarks>
public sealed class PassphraseProvider : IPassphraseProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PassphraseProvider"/> class.
    /// </summary>
    /// <param name="passphrase">The passphrase validated at the startup gate, or null when unset or in volatile mode.</param>
    public PassphraseProvider(string? passphrase) => Passphrase = passphrase;

    /// <inheritdoc/>
    public string? Passphrase { get; }
}
