namespace ClipVault.Application.Abstractions;

/// <summary>
/// Supplies the master passphrase entered at startup. When passphrase protection is enabled, the disk-mode
/// key vault uses it to decrypt the DEK. Returns <see langword="null"/> when no passphrase is set.
/// </summary>
public interface IPassphraseProvider
{
    /// <summary>Gets the master passphrase entered at startup, or <see langword="null"/> when none is set.</summary>
    string? Passphrase { get; }
}
