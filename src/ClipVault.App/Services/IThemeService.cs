using ClipVault.Application.Abstractions;
using Microsoft.UI.Xaml;

namespace ClipVaultApp.Services;

/// <summary>Applies the UI theme to every app window and switches it live.</summary>
public interface IThemeService
{
    /// <summary>Gets the theme currently applied.</summary>
    AppTheme Current { get; }

    /// <summary>Sets the initial theme before any window is registered (no UI work).</summary>
    /// <param name="theme">The theme loaded from settings.</param>
    void Initialize(AppTheme theme);

    /// <summary>Tracks a window and applies the current theme to it.</summary>
    /// <param name="window">The window to theme.</param>
    void Register(Window window);

    /// <summary>Switches the theme across all tracked windows and remembers it.</summary>
    /// <param name="theme">The theme to apply.</param>
    void Apply(AppTheme theme);
}
