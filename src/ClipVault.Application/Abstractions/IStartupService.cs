namespace ClipVault.Application.Abstractions;

/// <summary>Automatic startup when logging on to Windows (uses the registry Run key because the app is unpackaged).</summary>
public interface IStartupService
{
    /// <summary>Determines whether automatic startup is currently enabled.</summary>
    /// <returns><see langword="true"/> when automatic startup is enabled; otherwise, <see langword="false"/>.</returns>
    bool IsEnabled();

    /// <summary>Enables or disables automatic startup.</summary>
    /// <param name="enabled"><see langword="true"/> to enable automatic startup; <see langword="false"/> to disable it.</param>
    void SetEnabled(bool enabled);
}
