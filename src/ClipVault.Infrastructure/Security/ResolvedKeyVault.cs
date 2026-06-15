using ClipVault.Application.Abstractions;

namespace ClipVault.Infrastructure.Security;

/// <summary>
/// A key vault that returns a DEK already resolved (asynchronously) at startup. For modes where unlocking is
/// interactive and asynchronous, such as Windows Hello, it supplies the DEK resolved before the host is built
/// via <see cref="IResolvedMasterKey"/>.
/// </summary>
/// <param name="resolved">The resolved master key resolved during startup.</param>
public sealed class ResolvedKeyVault(IResolvedMasterKey resolved) : IKeyVault
{
    /// <inheritdoc/>
    public byte[] GetOrCreateMasterKey() =>
        (byte[]?)resolved.Dek?.Clone() ?? throw new InvalidOperationException("The master key has not been resolved.");
}
