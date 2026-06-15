using System;
using Windows.Win32.Foundation;

namespace ClipVaultApp.Platform;

/// <summary>
/// Represents a single window message intercepted by the subclass. When a subscriber sets
/// <see cref="Handled"/> to <see langword="true"/>, the message is treated as terminal and default processing is skipped.
/// </summary>
/// <param name="message">The window message identifier.</param>
/// <param name="wParam">The WPARAM value of the window message.</param>
/// <param name="lParam">The LPARAM value of the window message.</param>
internal sealed class WindowMessageEventArgs(uint message, WPARAM wParam, LPARAM lParam) : EventArgs
{
    /// <summary>
    /// Gets the window message identifier.
    /// </summary>
    public uint Message { get; } = message;

    /// <summary>
    /// Gets the WPARAM value of the window message.
    /// </summary>
    public WPARAM WParam { get; } = wParam;

    /// <summary>
    /// Gets the LPARAM value of the window message.
    /// </summary>
    public LPARAM LParam { get; } = lParam;

    /// <summary>
    /// Gets or sets a value indicating whether a subscriber has handled the message, which suppresses default processing.
    /// </summary>
    public bool Handled { get; set; }
}
