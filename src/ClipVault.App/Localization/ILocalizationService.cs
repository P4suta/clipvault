using ClipVault.Application.Abstractions;

namespace ClipVaultApp.Localization;

/// <summary>
/// Resolves UI strings for the active language. A single instance is created at startup (before any
/// window) and shared via <see cref="App.Localization"/> and DI. Because the app is unpackaged, the
/// language is chosen in-process here rather than through the platform resource system.
/// </summary>
public interface ILocalizationService
{
    /// <summary>Gets the concrete language in effect (<see cref="AppLanguage.System"/> already resolved).</summary>
    AppLanguage Current { get; }

    /// <summary>Gets the BCP-47 tag in effect (for example "ja", "en", or "zh-Hans").</summary>
    string CurrentCultureTag { get; }

    /// <summary>Gets the localized string for a key, falling back to English then to the key itself.</summary>
    /// <param name="key">The dotted string-table key (for example "Main.ClearAll").</param>
    /// <returns>The localized string, or the key when it is not found in any table.</returns>
    string GetString(string key);
}
