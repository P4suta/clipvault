using System.Security.Cryptography;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Tests;

/// <summary>A fake implementation with identity encryption plus a deterministic hash (to test the ingestion logic independently of cryptography).</summary>
internal sealed class FakeEncryptionService : IEncryptionService
{
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData = default) => plaintext.ToArray();

    public byte[] Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> associatedData = default) => ciphertext.ToArray();

    public ContentHash KeyedHash(ReadOnlySpan<byte> data) => new(Convert.ToHexString(SHA256.HashData(data)));
}
