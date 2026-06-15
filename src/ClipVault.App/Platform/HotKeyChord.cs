using System.Runtime.InteropServices;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ClipVaultApp.Platform;

/// <summary>
/// Represents the pairing of a hot key's modifier keys and virtual key. Kept in a single place so it is easy to change later.
/// </summary>
/// <param name="Modifiers">The modifier keys that must be held for the hot key.</param>
/// <param name="Key">The virtual key that triggers the hot key.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct HotKeyChord(HOT_KEY_MODIFIERS Modifiers, VIRTUAL_KEY Key);
