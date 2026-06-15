using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ClipVaultApp.Platform;

/// <summary>
/// A global low-level keyboard hook (WH_KEYBOARD_LL) that fires a callback when "Win + &lt;key&gt;" is pressed,
/// swallowing the keystroke so the OS shell never sees it. This lets ClipVault claim a chord the shell reserves
/// (Win+V), which <c>RegisterHotKey</c> cannot acquire. The hook is purely a filter: it inspects only the target
/// chord and passes every other key straight through, recording nothing. Lifetime is runtime-only — installed on
/// construction and removed on <see cref="Dispose"/> (no persistent system change, no admin rights).
/// </summary>
internal sealed class LowLevelKeyboardHook : IDisposable
{
    private readonly VIRTUAL_KEY _key;
    private readonly Action _onChord;

    // Keep the delegate referenced for the hook's lifetime so it is not collected while native code holds it.
    private readonly HOOKPROC _proc;
    private readonly HHOOK _hook;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LowLevelKeyboardHook"/> class and installs the hook.
    /// </summary>
    /// <param name="key">The virtual key that, together with the Windows key, triggers the callback.</param>
    /// <param name="onChord">Invoked on the hook thread when "Win + <paramref name="key"/>" is pressed.</param>
    public LowLevelKeyboardHook(VIRTUAL_KEY key, Action onChord)
    {
        _key = key;
        _onChord = onChord;
        _proc = HookProc;

        // WH_KEYBOARD_LL is a global hook whose callback runs on the installing (UI) thread's message loop.
        var moduleHandle = new HINSTANCE(Marshal.GetHINSTANCE(typeof(LowLevelKeyboardHook).Module));
        _hook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _proc, moduleHandle, 0);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // The hook exists only while we run; remove it on exit (no persistent system change).
        if (!_hook.IsNull)
        {
            PInvoke.UnhookWindowsHookEx(_hook);
        }

        // Keep the delegate rooted until after the hook is removed.
        GC.KeepAlive(_proc);
    }

    private static bool IsWinDown()
        => (PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_LWIN) & 0x8000) != 0
            || (PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_RWIN) & 0x8000) != 0;

    private static unsafe void MaskWinKey()
    {
        // A lone Ctrl tap counts as activity during the Win hold (suppresses the Start menu) but is otherwise inert.
        var inputs = new INPUT[2];
        inputs[0] = MakeKey(VIRTUAL_KEY.VK_LCONTROL, keyUp: false);
        inputs[1] = MakeKey(VIRTUAL_KEY.VK_LCONTROL, keyUp: true);
        PInvoke.SendInput(inputs.AsSpan(), sizeof(INPUT));
    }

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

    private unsafe LRESULT HookProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0)
        {
            var data = *(KBDLLHOOKSTRUCT*)lParam.Value;
            var injected = (data.flags & KBDLLHOOKSTRUCT_FLAGS.LLKHF_INJECTED) == KBDLLHOOKSTRUCT_FLAGS.LLKHF_INJECTED;

            if (!injected && data.vkCode == (uint)_key && IsWinDown())
            {
                var message = (uint)wParam.Value;
                if (message == PInvoke.WM_KEYDOWN || message == PInvoke.WM_SYSKEYDOWN)
                {
                    // Mask the Win key so its release does not pop the Start menu, then signal the summon.
                    MaskWinKey();
                    _onChord();
                }

                // Swallow the event (down and up) so the shell never sees the reserved chord.
                return new LRESULT(1);
            }
        }

        return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);
    }
}
