using ClipVault.Application.Abstractions;

namespace ClipVault.Application.Capture;

/// <summary>The default implementation that holds the paused state in memory.</summary>
public sealed class CaptureStateService : ICaptureStateService
{
    private volatile bool _paused;

    /// <inheritdoc/>
    public event EventHandler? StateChanged;

    /// <inheritdoc/>
    public bool IsPaused => _paused;

    /// <inheritdoc/>
    public void Pause() => SetPaused(true);

    /// <inheritdoc/>
    public void Unpause() => SetPaused(false);

    /// <inheritdoc/>
    public void Toggle() => SetPaused(!_paused);

    private void SetPaused(bool value)
    {
        if (_paused == value)
        {
            return;
        }

        _paused = value;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
