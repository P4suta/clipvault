using ClipVault.Application.Abstractions;

namespace ClipVaultApp.Services;

/// <summary>
/// Mutable holder for the DEK resolved (asynchronously and interactively) at startup.
/// In disk mode with Windows Hello protection, the startup gate stores the unlocked DEK here before the host is built,
/// and <see cref="ClipVault.Infrastructure.Security.ResolvedKeyVault"/> (the key vault) reads it back lazily.
/// In other modes (DPAPI-only, passphrase, or volatile) it is unused and Dek remains null.
/// Registered as a singleton in DI so the same instance populated by the gate is shared.
/// </summary>
public sealed class ResolvedMasterKey : IResolvedMasterKey
{
    /// <inheritdoc/>
    public byte[]? Dek { get; set; }
}
