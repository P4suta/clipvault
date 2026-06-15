using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace ClipVaultApp.Platform;

/// <summary>
/// Provides a window subclass used to intercept window messages (COMCTL32 SetWindowSubclass).
/// Forwards the tray callback message and the posted summon message to higher layers.
/// The SUBCLASSPROC delegate crashes immediately if collected by the GC, so it must always be held in a field.
/// </summary>
internal sealed class WindowSubclass : IDisposable
{
    // Identifies the subclass (distinguishes between multiple subclasses attached to the same window).
    private const uint SubclassId = 1;

    private readonly HWND _hwnd;

    // Field that roots the delegate. Removing it could let WndProc be collected during a callback.
    private readonly SUBCLASSPROC _proc;

    private bool _installed;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowSubclass"/> class.
    /// </summary>
    /// <param name="hwnd">The handle of the window to subclass.</param>
    public WindowSubclass(nint hwnd)
    {
        _hwnd = new HWND(hwnd);

        // Create the delegate and pin it to a field (binds its lifetime to this instance).
        _proc = SubclassProc;

        // The fourth argument dwRefData is unused (state is kept on the managed side).
        if (PInvoke.SetWindowSubclass(_hwnd, _proc, SubclassId, 0))
        {
            _installed = true;
        }
    }

    /// <summary>
    /// Occurs when an intercepted message is received. A message for which a subscriber sets
    /// <see cref="WindowMessageEventArgs.Handled"/> to <see langword="true"/> is treated as handled,
    /// skips default processing, and returns 0.
    /// </summary>
    public event EventHandler<WindowMessageEventArgs>? MessageReceived;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_installed)
        {
            // The field holds the delegate, so it is safe to pass it through until removal.
            PInvoke.RemoveWindowSubclass(_hwnd, _proc, SubclassId);
            _installed = false;
        }
    }

    private LRESULT SubclassProc(
        HWND hWnd,
        uint uMsg,
        WPARAM wParam,
        LPARAM lParam,
        nuint uIdSubclass,
        nuint dwRefData)
    {
        // A message that the higher-level handler marked as handled (Handled=true) terminates here.
        var args = new WindowMessageEventArgs(uMsg, wParam, lParam);
        MessageReceived?.Invoke(this, args);
        if (args.Handled)
        {
            return new LRESULT(0);
        }

        return PInvoke.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }
}
