using System;
using System.Threading.Tasks;

namespace ClipVaultApp.Platform;

/// <summary>
/// Bundles the callbacks from the controller (<see cref="TrayHotkeyController"/>) to the App.
/// Exposes the presentation state (window visibility and pause) and operations as a seam.
/// </summary>
internal sealed class TrayActions
{
    /// <summary>
    /// Gets the callback that shows the main window.
    /// </summary>
    public required Action ShowWindow { get; init; }

    /// <summary>
    /// Gets the callback that hides the main window.
    /// </summary>
    public required Action HideWindow { get; init; }

    /// <summary>
    /// Gets the callback that returns a value indicating whether the main window is currently visible.
    /// </summary>
    public required Func<bool> IsWindowVisible { get; init; }

    /// <summary>
    /// Gets the callback that records the foreground window handle as the capture target.
    /// </summary>
    public required Action<nint> CaptureTarget { get; init; }

    /// <summary>
    /// Gets the callback that returns a value indicating whether clipboard capture is currently paused.
    /// </summary>
    public required Func<bool> IsPaused { get; init; }

    /// <summary>
    /// Gets the callback that toggles whether clipboard capture is paused.
    /// </summary>
    public required Action TogglePause { get; init; }

    /// <summary>
    /// Gets the callback that clears all clipboard history asynchronously.
    /// </summary>
    public required Func<Task> ClearAllAsync { get; init; }

    /// <summary>
    /// Gets the callback that opens the settings.
    /// </summary>
    public required Action OpenSettings { get; init; }

    /// <summary>
    /// Gets the callback that exits the application asynchronously.
    /// </summary>
    public required Func<Task> ExitAsync { get; init; }
}
