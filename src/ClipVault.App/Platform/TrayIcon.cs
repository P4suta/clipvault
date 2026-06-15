using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ClipVaultApp.Platform;

/// <summary>
/// Manages the notification-area (system tray) icon via the Win32 <c>Shell_NotifyIcon</c>.
/// Left and right clicks arrive at the window as <see cref="CallbackMessage"/> (picked up by the subclass).
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    /// <summary>The tray callback message (WM_APP+1). The subclass tests for this value.</summary>
    public const uint CallbackMessage = PInvoke.WM_APP + 1;

    // The ID that uniquely identifies the icon within this process.
    private const uint IconId = 1;

    private readonly HWND _hwnd;
    private HICON _icon;
    private bool _added;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIcon"/> class.
    /// </summary>
    /// <param name="hwnd">The window handle that owns the tray icon.</param>
    public TrayIcon(nint hwnd)
    {
        _hwnd = new HWND(hwnd);
    }

    /// <summary>Registers the tray icon. Loads the icon from a .ico file and sets the tooltip.</summary>
    /// <param name="iconPath">The path to the .ico file (absolute, or relative to the executable).</param>
    /// <param name="tooltip">The tooltip text shown for the icon.</param>
    /// <returns>True if the icon was added successfully; otherwise false.</returns>
    public unsafe bool Create(string iconPath, string tooltip)
    {
        _icon = LoadIcon(iconPath);

        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = _hwnd,
            uID = IconId,
            uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE
                | NOTIFY_ICON_DATA_FLAGS.NIF_ICON
                | NOTIFY_ICON_DATA_FLAGS.NIF_TIP
                | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP,
            uCallbackMessage = CallbackMessage,
            hIcon = _icon,
        };

        SetTooltip(ref data, tooltip);

        // Set the union member (uVersion) through the Anonymous field.
        data.Anonymous.uVersion = PInvoke.NOTIFYICON_VERSION_4;

        _added = PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, in data);
        if (_added)
        {
            // Select the Version 4 behavior (rich notifications, accurate click coordinates).
            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_SETVERSION, in data);
        }

        return _added;
    }

    /// <summary>
    /// Shows the right-click context menu and returns the selected command ID (0 if none selected).
    /// Follows the Win32 idiom of SetForegroundWindow -> TrackPopupMenu so the menu closes correctly.
    /// </summary>
    /// <param name="items">The menu items to display.</param>
    /// <returns>The selected command ID, or 0 if nothing was selected.</returns>
    public unsafe uint ShowContextMenu(TrayMenuItem[] items)
    {
        var menu = PInvoke.CreatePopupMenu();
        if (menu.IsNull)
        {
            return 0;
        }

        try
        {
            foreach (var item in items)
            {
                if (item.IsSeparator)
                {
                    PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, default(PCWSTR));
                    continue;
                }

                var flags = MENU_ITEM_FLAGS.MF_STRING
                    | (item.IsChecked ? MENU_ITEM_FLAGS.MF_CHECKED : MENU_ITEM_FLAGS.MF_UNCHECKED);

                // Pin to the raw HMENU overload and pass a PCWSTR (avoid ambiguity with the SafeHandle version).
                fixed (char* text = item.Text)
                {
                    PInvoke.AppendMenu(menu, flags, item.Id, new PCWSTR(text));
                }
            }

            // Show the menu at the click position.
            PInvoke.GetCursorPos(out var pt);

            // Foreground the window so the menu reliably closes on an outside click (the documented idiom).
            PInvoke.SetForegroundWindow(_hwnd);

            var command = PInvoke.TrackPopupMenuEx(
                menu,
                (uint)(TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON | TRACK_POPUP_MENU_FLAGS.TPM_LEFTALIGN | TRACK_POPUP_MENU_FLAGS.TPM_BOTTOMALIGN | TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD),
                pt.X,
                pt.Y,
                _hwnd,
                null);

            // Right after the menu closes, post a dummy message to advance default processing one step (the idiom).
            PInvoke.PostMessage(_hwnd, PInvoke.WM_NULL, default, default);

            // With TPM_RETURNCMD, the return value is the selected command ID (0 if none).
            return (uint)command.Value;
        }
        finally
        {
            PInvoke.DestroyMenu(menu);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_added)
        {
            var data = new NOTIFYICONDATAW
            {
                cbSize = GetSize(),
                hWnd = _hwnd,
                uID = IconId,
            };

            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in data);
            _added = false;
        }

        if (!_icon.IsNull)
        {
            PInvoke.DestroyIcon(_icon);
            _icon = default;
        }
    }

    private static unsafe void SetTooltip(ref NOTIFYICONDATAW data, string tooltip)
    {
        // szTip is a fixed-length buffer. Copy safely including the terminating NUL.
        var span = data.szTip.AsSpan();
        var max = span.Length - 1;
        var length = Math.Min(tooltip.Length, max);
        tooltip.AsSpan(0, length).CopyTo(span);
        span[length] = '\0';
    }

    private static unsafe HICON LoadIcon(string iconPath)
    {
        // Resolve a path relative to the executable into an absolute path (unpackaged deployment).
        var fullPath = System.IO.Path.IsPathRooted(iconPath)
            ? iconPath
            : System.IO.Path.Combine(AppContext.BaseDirectory, iconPath);

        fixed (char* path = fullPath)
        {
            // The overload that returns a raw handle (HANDLE). The icon is destroyed via DestroyIcon in Dispose.
            var handle = PInvoke.LoadImage(
                default,
                path,
                GDI_IMAGE_TYPE.IMAGE_ICON,
                0,
                0,
                IMAGE_FLAGS.LR_LOADFROMFILE | IMAGE_FLAGS.LR_DEFAULTSIZE);

            return new HICON(handle.Value);
        }
    }

    private static unsafe uint GetSize() => (uint)sizeof(NOTIFYICONDATAW);
}
