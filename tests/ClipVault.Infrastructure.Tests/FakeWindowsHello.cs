using System.Security.Cryptography;
using ClipVault.Application.Abstractions;

namespace ClipVault.Infrastructure.Tests;

/// <summary>
/// Mimics real Windows Hello with a deterministic signature (HMAC). A difference in credential equals a difference in secret.
/// </summary>
internal sealed class FakeWindowsHello(byte[] secret) : IWindowsHello
{
    public Task<bool> IsAvailableAsync() => Task.FromResult(true);

    public Task<byte[]?> SignChallengeAsync(byte[] challenge, bool createIfMissing) =>
        Task.FromResult<byte[]?>(HMACSHA256.HashData(secret, challenge));
}
