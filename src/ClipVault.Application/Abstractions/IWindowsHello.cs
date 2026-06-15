namespace ClipVault.Application.Abstractions;

/// <summary>
/// Signing via Windows Hello (a TPM-protected key plus biometric/PIN authentication). Signing a fixed challenge is
/// deterministic, and that signature is used as key material to protect the DEK (a Hello prompt appears on every unlock).
/// </summary>
public interface IWindowsHello
{
    /// <summary>Determines whether Windows Hello is available.</summary>
    /// <returns>A task that produces <see langword="true"/> when Windows Hello is available; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Signs the challenge with Windows Hello and returns the signature bytes. Returns <see langword="null"/> if the user cancels or fails.
    /// </summary>
    /// <param name="challenge">The challenge bytes to sign.</param>
    /// <param name="createIfMissing">Whether to create the credential when none exists (<see langword="true"/> when enabling, <see langword="false"/> when unlocking).</param>
    /// <returns>A task that produces the signature bytes, or <see langword="null"/> when the user cancels or signing fails.</returns>
    Task<byte[]?> SignChallengeAsync(byte[] challenge, bool createIfMissing);
}
