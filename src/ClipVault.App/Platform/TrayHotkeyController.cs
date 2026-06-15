using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ClipVaultApp.Platform;

/// <summary>
/// Controller that bundles tray residency, the global summon hotkey, and the right-click menu. The summon
/// chord is Win+V, which the OS shell reserves and <c>RegisterHotKey</c> cannot acquire; it is therefore
/// captured with a <see cref="LowLevelKeyboardHook"/> that intercepts Win+V before the shell. Tray clicks
/// arrive via a window subclass. It bridges tray operations / summon / menu selections to the upper layer's
/// (App's) actions and cleans up the Win32 resources.
/// </summary>
internal sealed class TrayHotkeyController : IDisposable
{
    /// <summary>
    /// The summon chord, kept in a single place so it is easy to change (Win+V). The Win modifier is implied
    /// by the hook, so only <see cref="HotKeyChord.Key"/> is matched against the pressed key.
    /// </summary>
    public static readonly HotKeyChord SummonChord = new(
        HOT_KEY_MODIFIERS.MOD_WIN,
        VIRTUAL_KEY.VK_V);

    // The message the hook posts to run the summon on the UI thread (decoupled from the hook proc).
    private const uint WmSummon = PInvoke.WM_APP + 2;

    // Command IDs for the right-click menu.
    private const uint MenuShow = 101;
    private const uint MenuPause = 102;
    private const uint MenuClear = 103;
    private const uint MenuSettings = 104;
    private const uint MenuExit = 105;

    private readonly HWND _hwnd;
    private readonly TrayActions _actions;
    private readonly WindowSubclass _subclass;
    private readonly TrayIcon _trayIcon;
    private readonly LowLevelKeyboardHook _summonHook;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayHotkeyController"/> class.
    /// </summary>
    /// <param name="hwnd">The window handle that hosts the tray icon and receives the summon message.</param>
    /// <param name="actions">The callbacks bridged to the App layer.</param>
    public TrayHotkeyController(nint hwnd, TrayActions actions)
    {
        _hwnd = new HWND(hwnd);
        _actions = actions;

        // 1) Install a subclass to intercept messages (tray clicks and the posted summon message).
        _subclass = new WindowSubclass(hwnd);
        _subclass.MessageReceived += OnMessage;

        // 2) Register the tray icon.
        _trayIcon = new TrayIcon(hwnd);
        _trayIcon.Create("Assets/AppIcon.ico", "ClipVault");

        // 3) Install the low-level hook that claims Win+V (RegisterHotKey cannot take an OS-reserved chord).
        //    The hook runs on this (UI) thread and only posts a message, so the summon work stays off the hook proc.
        _summonHook = new LowLevelKeyboardHook(SummonChord.Key, OnSummonRequested);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Cleanup: remove the hook -> remove subclass -> delete tray icon.
        _summonHook.Dispose();
        _subclass.MessageReceived -= OnMessage;
        _subclass.Dispose();
        _trayIcon.Dispose();
    }

    // Gets the HWND of the foreground window as nint (CsWin32's HWND wraps a void*).
    private static unsafe nint GetForegroundHwnd() => (nint)PInvoke.GetForegroundWindow().Value;

    // Called from the hook proc when Win+V is pressed: hand off to the UI thread without blocking the hook.
    private void OnSummonRequested() => PInvoke.PostMessage(_hwnd, WmSummon, default, default);

    // Handles messages delivered from the subclass. When handled, set Handled=true (suppress default processing).
    private void OnMessage(object? sender, WindowMessageEventArgs e)
    {
        switch (e.Message)
        {
            case WmSummon:
                OnSummonHotkey();
                e.Handled = true;
                break;

            case TrayIcon.CallbackMessage:
                // In Version 4 the low word of LPARAM represents the mouse event.
                var mouseEvent = (uint)(e.LParam.Value & 0xFFFF);
                if (mouseEvent == PInvoke.WM_LBUTTONUP)
                {
                    OnTrayLeftClick();
                    e.Handled = true;
                }
                else if (mouseEvent == PInvoke.WM_RBUTTONUP)
                {
                    OnTrayRightClick();
                    e.Handled = true;
                }

                break;

            default:
                break;
        }
    }

    // Summon: capture the paste-back target first, then show and foreground the window.
    private void OnSummonHotkey()
    {
        // 1) Capture the previous foreground window (= the paste-back target) with top priority.
        _actions.CaptureTarget(GetForegroundHwnd());

        // 2) Show and foreground the window, and focus the search box.
        _actions.ShowWindow();
    }

    // Tray left-click: hide if visible, or capture the paste-back target and show if hidden.
    private void OnTrayLeftClick()
    {
        if (_actions.IsWindowVisible())
        {
            _actions.HideWindow();
            return;
        }

        _actions.CaptureTarget(GetForegroundHwnd());
        _actions.ShowWindow();
    }

    // Tray right-click: show the context menu and run the selected command.
    private void OnTrayRightClick()
    {
        var items = new[]
        {
            TrayMenuItem.Command(MenuShow, App.Localization.GetString("Tray.Show")),
            TrayMenuItem.Command(MenuPause, App.Localization.GetString("Tray.Pause"), _actions.IsPaused()),
            TrayMenuItem.Command(MenuClear, App.Localization.GetString("Tray.ClearAll")),
            TrayMenuItem.Separator(),
            TrayMenuItem.Command(MenuSettings, App.Localization.GetString("Tray.Settings")),
            TrayMenuItem.Command(MenuExit, App.Localization.GetString("Tray.Exit")),
        };

        var command = _trayIcon.ShowContextMenu(items);
        switch (command)
        {
            case MenuShow:
                _actions.ShowWindow();
                break;
            case MenuPause:
                _actions.TogglePause();
                break;
            case MenuClear:
                _ = _actions.ClearAllAsync();
                break;
            case MenuSettings:
                _actions.OpenSettings();
                break;
            case MenuExit:
                _ = _actions.ExitAsync();
                break;
        }
    }
}
