namespace ClipVault.Domain.Abstractions;

/// <summary>
/// A port that monitors changes to the OS clipboard and reports them as decrypted snapshots.
/// The implementation is the Win32 clipboard monitor (Infrastructure). The event fires only after
/// reading has completed and after debouncing.
/// </summary>
public interface IClipboardMonitor
{
    /// <summary>Occurs when the clipboard content changes and a decrypted snapshot is available.</summary>
    event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    /// <summary>Starts monitoring the OS clipboard.</summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops monitoring the OS clipboard.</summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Excludes the immediately following self-write (paste-back) from ingestion exactly once.
    /// Disposing the return value releases the guard.
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> that releases the suppression guard when disposed.</returns>
    IDisposable SuppressNextCapture();
}
