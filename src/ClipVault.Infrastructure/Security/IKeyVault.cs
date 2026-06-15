namespace ClipVault.Infrastructure.Security;

/// <summary>
/// Supplies the master key used for encryption. Implementations manage a file sealed with DPAPI.
/// In tests this is replaced with a fixed-key implementation.
/// </summary>
public interface IKeyVault
{
    /// <summary>
    /// Gets the master key (32 bytes). On first use it is generated and sealed; afterwards it is decrypted and returned.
    /// </summary>
    /// <returns>The 32-byte master key.</returns>
    byte[] GetOrCreateMasterKey();
}
