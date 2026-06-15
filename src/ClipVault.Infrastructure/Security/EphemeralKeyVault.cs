using System.Security.Cryptography;

namespace ClipVault.Infrastructure.Security;

/// <summary>
/// The key vault for the volatile-memory mode. The master key is randomly generated in RAM and is never
/// written to disk. It disappears together with the key when the process exits.
/// </summary>
public sealed class EphemeralKeyVault : IKeyVault
{
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

    /// <inheritdoc/>
    public byte[] GetOrCreateMasterKey() => (byte[])_key.Clone(); // The caller zeroes the array it receives, so return a fresh copy each time.
}
