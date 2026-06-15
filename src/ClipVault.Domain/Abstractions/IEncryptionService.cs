using ClipVault.Domain.ValueObjects;

namespace ClipVault.Domain.Abstractions;

/// <summary>
/// An abstraction over encryption at rest. The implementation is expected to use ChaCha20-Poly1305
/// (with the key sealed via DPAPI). Knowledge of cryptography is not leaked into the Domain and is
/// confined behind this port (Infrastructure).
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts the plaintext. The return value is in the form [version | nonce | tag | ciphertext].
    /// The <paramref name="associatedData"/> (AAD) is authenticated and bound to the ciphertext and the
    /// same value is required on decryption (to prevent swapping or repurposing ciphertext across fields or entries).
    /// </summary>
    /// <param name="plaintext">The plaintext bytes to encrypt.</param>
    /// <param name="associatedData">The additional authenticated data (AAD) to bind to the ciphertext.</param>
    /// <returns>The encrypted bytes in the form [version | nonce | tag | ciphertext].</returns>
    byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData = default);

    /// <summary>Decrypts the ciphertext. Throws an exception if validation of the authentication tag or AAD fails.</summary>
    /// <param name="ciphertext">The ciphertext bytes to decrypt.</param>
    /// <param name="associatedData">The additional authenticated data (AAD) that was bound during encryption.</param>
    /// <returns>The decrypted plaintext bytes.</returns>
    byte[] Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> associatedData = default);

    /// <summary>Computes a keyed hash (HMAC) used for duplicate detection.</summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The keyed hash of the supplied data.</returns>
    ContentHash KeyedHash(ReadOnlySpan<byte> data);
}
