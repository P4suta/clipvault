using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ClipVaultApp.Platform;

/// <summary>
/// Brings the previously recorded paste-back target window to the foreground and synthesizes Ctrl+V.
/// It assumes the clipboard has already been set by <c>ClipboardActionService</c> before the call.
/// Even on failure (target=0 / failed to foreground), the clipboard is already set, so it gracefully
/// falls back to "copy only" and never throws.
/// </summary>
internal static class PasteService
{
    /// <summary>
    /// Brings <paramref name="target"/> to the foreground and sends Ctrl+V. Returns whether it
    /// succeeded (the caller may ignore it).
    /// </summary>
    /// <param name="target">The handle of the window to paste into.</param>
    /// <returns>True if the target was brought to the foreground and Ctrl+V was sent; otherwise false.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Auto paste-back is best-effort (never throws); falls back to copy-only on failure.")]
    public static bool PasteInto(nint target)
    {
        if (target == 0)
        {
            return false;
        }

        var targetHwnd = new HWND(target);

        // Temporarily attach the input thread to reliably hand foreground rights to another thread's window.
        var foreThread = PInvoke.GetWindowThreadProcessId(targetHwnd, out _);
        var thisThread = PInvoke.GetCurrentThreadId();
        var attached = false;

        try
        {
            if (foreThread != 0 && foreThread != thisThread)
            {
                attached = PInvoke.AttachThreadInput(thisThread, foreThread, true);
            }

            if (!PInvoke.SetForegroundWindow(targetHwnd))
            {
                // If it cannot be brought to the foreground, fall back to clipboard only (copy only).
                return false;
            }

            SendCtrlV();
            return true;
        }
        catch
        {
            // Automatic paste-back is not deterministic, so never throw on any failure.
            return false;
        }
        finally
        {
            if (attached)
            {
                PInvoke.AttachThreadInput(thisThread, foreThread, false);
            }
        }
    }

    private static unsafe void SendCtrlV()
    {
        // Four events: CONTROL down -> V down -> V up -> CONTROL up.
        var inputs = new INPUT[4];

        inputs[0] = KeyDown(VIRTUAL_KEY.VK_CONTROL);
        inputs[1] = KeyDown(VIRTUAL_KEY.VK_V);
        inputs[2] = KeyUp(VIRTUAL_KEY.VK_V);
        inputs[3] = KeyUp(VIRTUAL_KEY.VK_CONTROL);

        var inserted = PInvoke.SendInput(inputs.AsSpan(), sizeof(INPUT));
        if (inserted != (uint)inputs.Length)
        {
            // Input can be blocked (UIPI / another app holding the foreground); paste-back is best-effort.
            Debug.WriteLine($"SendInput inserted {inserted}/{inputs.Length} events; paste-back may be incomplete.");
        }
    }

    private static INPUT KeyDown(VIRTUAL_KEY key) => MakeKey(key, keyUp: false);

    private static INPUT KeyUp(VIRTUAL_KEY key) => MakeKey(key, keyUp: true);

    private static INPUT MakeKey(VIRTUAL_KEY key, bool keyUp) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous = new INPUT._Anonymous_e__Union
        {
            ki = new KEYBDINPUT
            {
                wVk = key,
                dwFlags = keyUp ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : default,
            },
        },
    };
}
