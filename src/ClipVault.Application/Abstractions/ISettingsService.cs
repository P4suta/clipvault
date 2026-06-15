namespace ClipVault.Application.Abstractions;

/// <summary>Supplies the current settings and applies updates to them.</summary>
public interface ISettingsService
{
    /// <summary>Occurs when the settings change.</summary>
    event EventHandler? Changed;

    /// <summary>Gets the current settings.</summary>
    ClipVaultSettings Current { get; }

    /// <summary>Updates the current settings.</summary>
    /// <param name="settings">The new settings to apply.</param>
    void Update(ClipVaultSettings settings);
}
