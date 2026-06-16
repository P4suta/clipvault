using System.Security.Cryptography;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Infrastructure.Security;

/// <summary>
/// Encryption at rest using ChaCha20-Poly1305 (a modern AEAD adopted by WireGuard, TLS 1.3, age, and others).
/// The encryption key and the HMAC key are derived separately from the master key with HKDF (key separation by
/// purpose). The Poly1305 authentication tag and the AAD cause tampering or misuse to be detected as a decryption
/// failure.
/// Ciphertext format: [version(1)=2 | nonce(12) | tag(16) | ciphertext] (the version supports crypto-agility).
/// Note: ChaCha20-Poly1305 requires Windows 10 2004 or later (the Windows 11 environment here supports it).
/// </summary>
public sealed class ChaCha20Poly1305EncryptionService : IEncryptionService, IDisposable
{
    private const byte Version = 2;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int HeaderSize = 1 + NonceSize + TagSize;

    private readonly ChaCha20Poly1305 _aead;
    private readonly byte[] _hmacKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChaCha20Poly1305EncryptionService"/> class.
    /// </summary>
    /// <param name="keyVault">The key vault that supplies the master key from which the AEAD and HMAC keys are derived.</param>
    public ChaCha20Poly1305EncryptionService(IKeyVault keyVault)
    {
        if (!ChaCha20Poly1305.IsSupported)
        {
            throw new PlatformNotSupportedException(
                "ChaCha20-Poly1305 is not supported on this OS (Windows 10 2004 or later is required).");
        }

        var master = keyVault.GetOrCreateMasterKey();
        byte[]? aeadKey = null;
        try
        {
            aeadKey = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                master,
                outputLength: 32,
                salt: null,
                info: "ClipVault.chacha20poly1305.v1"u8.ToArray());
            _hmacKey = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                master,
                outputLength: 32,
                salt: null,
                info: "ClipVault.hmac.v1"u8.ToArray());
            _aead = new ChaCha20Poly1305(aeadKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(master);
            if (aeadKey is not null)
            {
                CryptographicOperations.ZeroMemory(aeadKey);
            }
        }
    }

    /// <inheritdoc/>
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData = default)
    {
        var result = new byte[HeaderSize + plaintext.Length];
        result[0] = Version;
        var nonce = result.AsSpan(1, NonceSize);
        var tag = result.AsSpan(1 + NonceSize, TagSize);
        var ciphertext = result.AsSpan(HeaderSize);

        RandomNumberGenerator.Fill(nonce);
        _aead.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        return result;
    }

    /// <inheritdoc/>
    public byte[] Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> associatedData = default)
    {
        if (ciphertext.Length < HeaderSize || ciphertext[0] != Version)
        {
            throw new CryptographicException("The ciphertext format is invalid.");
        }

        var nonce = ciphertext.Slice(1, NonceSize);
        var tag = ciphertext.Slice(1 + NonceSize, TagSize);
        var payload = ciphertext[HeaderSize..];

        var plaintext = new byte[payload.Length];
        var ok = false;
        try
        {
            _aead.Decrypt(nonce, payload, tag, plaintext, associatedData);
            ok = true;
            return plaintext;
        }
        finally
        {
            if (!ok)
            {
                // On a tampered/invalid ciphertext, never leave a partial plaintext in the abandoned buffer.
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    /// <inheritdoc/>
    public ContentHash KeyedHash(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(_hmacKey, data, hash);
        return new ContentHash(Convert.ToHexString(hash));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _aead.Dispose();
        CryptographicOperations.ZeroMemory(_hmacKey);
    }
}
