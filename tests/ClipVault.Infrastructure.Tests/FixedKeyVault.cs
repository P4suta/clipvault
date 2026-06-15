using ClipVault.Infrastructure.Security;

namespace ClipVault.Infrastructure.Tests;

/// <summary>
/// A key vault that returns a fixed key (to unit-test the encryption service without going through DPAPI).
/// </summary>
internal sealed class FixedKeyVault(byte[] key) : IKeyVault
{
    public byte[] GetOrCreateMasterKey() => (byte[])key.Clone();
}
