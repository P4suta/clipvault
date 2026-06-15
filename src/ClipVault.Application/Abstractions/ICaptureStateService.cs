namespace ClipVault.Application.Abstractions;

/// <summary>The paused (secret) state of capture. Toggled from the tray or a hotkey.</summary>
public interface ICaptureStateService
{
    /// <summary>Occurs when the paused state changes.</summary>
    event EventHandler? StateChanged;

    /// <summary>Gets a value indicating whether capture is currently paused.</summary>
    bool IsPaused { get; }

    /// <summary>Pauses capture.</summary>
    void Pause();

    /// <summary>Resumes capture.</summary>
    void Unpause();

    /// <summary>Toggles the paused state.</summary>
    void Toggle();
}
