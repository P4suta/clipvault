using System.Security.Cryptography;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Infrastructure.Tests;

/// <summary>
/// An identity encryption service (to verify the repository's storage logic in isolation from cryptography).
/// </summary>
internal sealed class IdentityEncryptionService : IEncryptionService
{
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData = default) => plaintext.ToArray();

    public byte[] Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> associatedData = default) => ciphertext.ToArray();

    public ContentHash KeyedHash(ReadOnlySpan<byte> data) => new(Convert.ToHexString(SHA256.HashData(data)));
}
