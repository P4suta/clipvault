using System.Collections.Generic;
using ClipVault.Application.Abstractions;
using Microsoft.UI.Xaml;

namespace ClipVaultApp.Services;

/// <summary>
/// Default <see cref="IThemeService"/>. Drives each window's root <see cref="FrameworkElement.RequestedTheme"/>;
/// <see cref="ElementTheme.Default"/> follows the OS theme. Mica and the title bar follow the element theme.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly List<Window> _windows = [];
    private AppTheme _theme = AppTheme.System;

    /// <inheritdoc/>
    public AppTheme Current => _theme;

    /// <summary>Applies a theme to a single window's root element (used for transient startup windows too).</summary>
    /// <param name="window">The window to theme.</param>
    /// <param name="theme">The theme to apply.</param>
    public static void ApplyTo(Window window, AppTheme theme)
    {
        if (window.Content is FrameworkElement root)
        {
            root.RequestedTheme = ToElementTheme(theme);
        }
    }

    /// <inheritdoc/>
    public void Initialize(AppTheme theme) => _theme = theme;

    /// <inheritdoc/>
    public void Register(Window window)
    {
        _windows.Add(window);
        window.Closed += OnWindowClosed;
        ApplyTo(window, _theme);
    }

    /// <inheritdoc/>
    public void Apply(AppTheme theme)
    {
        _theme = theme;
        foreach (var window in _windows)
        {
            ApplyTo(window, theme);
        }
    }

    private static ElementTheme ToElementTheme(AppTheme theme) => theme switch
    {
        AppTheme.Light => ElementTheme.Light,
        AppTheme.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (sender is Window window)
        {
            window.Closed -= OnWindowClosed;
            _windows.Remove(window);
        }
    }
}
