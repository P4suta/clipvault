using ClipVault.Application.Abstractions;

namespace ClipVault.Application.Settings;

/// <summary>
/// The default implementation that holds settings in memory. Persistence will later be swapped in by an Infrastructure implementation.
/// </summary>
public sealed class InMemorySettingsService : ISettingsService
{
    private ClipVaultSettings _current = ClipVaultSettings.Default;

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    public ClipVaultSettings Current => _current;

    /// <inheritdoc/>
    public void Update(ClipVaultSettings settings)
    {
        _current = settings;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
