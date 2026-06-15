using Microsoft.UI.Windowing;

namespace ClipVaultApp.Platform;

/// <summary>
/// Provides the <see cref="AppWindow"/> configuration that makes the main window look like a "clipboard popup".
/// Keeps it always on top, hidden from the taskbar, and hidden from Alt+Tab, while preserving the title bar and Mica.
/// </summary>
internal static class PopupWindowChrome
{
    /// <summary>
    /// Applies the popup-style appearance to the <see cref="OverlappedPresenter"/>.
    /// </summary>
    /// <param name="appWindow">The application window to configure.</param>
    public static void Apply(AppWindow appWindow)
    {
        // Hide it from the Alt+Tab and taskbar switchers (property directly on AppWindow).
        appWindow.IsShownInSwitchers = false;

        if (appWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        // Always show it on top (the clipboard history should be picked quickly in the foreground).
        presenter.IsAlwaysOnTop = true;

        // Maximize and minimize are not needed (this is a compact popup).
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
    }
}
