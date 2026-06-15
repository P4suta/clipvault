namespace ClipVault.Application.Abstractions;

/// <summary>
/// Supplies the master key (DEK) that was resolved (asynchronously) at startup. For modes where unlocking is
/// asynchronous and interactive, such as Windows Hello, the DEK is resolved before the host is built and handed in here.
/// </summary>
public interface IResolvedMasterKey
{
    /// <summary>Gets the resolved master key (DEK), or <see langword="null"/> when it has not been resolved.</summary>
    byte[]? Dek { get; }
}
