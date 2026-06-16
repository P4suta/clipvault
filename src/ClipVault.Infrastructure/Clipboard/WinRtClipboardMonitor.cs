using System.Diagnostics.CodeAnalysis;
using ClipVault.Application.Abstractions;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.ValueObjects;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace ClipVault.Infrastructure.Clipboard;

/// <summary>
/// Monitors clipboard changes via the WinRT <c>Clipboard.ContentChanged</c> event: debounces rapid
/// firings, reads on the UI thread, and raises a domain snapshot.
/// </summary>
/// <param name="dispatcher">The UI dispatcher used to subscribe to and read the clipboard on the UI thread.</param>
public sealed class WinRtClipboardMonitor(IUiDispatcher dispatcher) : IClipboardMonitor
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan SuppressWindow = TimeSpan.FromMilliseconds(1200);

    private int _changeSequence;
    private long _suppressUntilTicks;

    /// <inheritdoc/>
    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default) =>
        dispatcher.EnqueueAsync(() =>
        {
            WinClipboard.ContentChanged += OnContentChanged;
            return Task.CompletedTask;
        });

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken = default) =>
        dispatcher.EnqueueAsync(() =>
        {
            WinClipboard.ContentChanged -= OnContentChanged;
            return Task.CompletedTask;
        });

    /// <inheritdoc/>
    public IDisposable SuppressNextCapture()
    {
        Interlocked.Exchange(ref _suppressUntilTicks, (DateTimeOffset.UtcNow + SuppressWindow).UtcTicks);
        return NoopDisposable.Instance;
    }

    [SuppressMessage(
        "Usage",
        "VSTHRD100:Avoid async void methods",
        Justification = "ContentChanged has a fixed async void signature; the body catches all exceptions.")]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort monitoring; a single read failure must not stop the loop.")]
    private async void OnContentChanged(object? sender, object e)
    {
        var sequence = Interlocked.Increment(ref _changeSequence);
        try
        {
            await Task.Delay(DebounceDelay);
            if (sequence != Volatile.Read(ref _changeSequence) || IsSuppressed())
            {
                return;
            }

            var snapshot = await WinRtClipboardReader.ReadAsync();
            if (snapshot is not null)
            {
                ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs(snapshot));
            }
        }
        catch
        {
            // Do not stop monitoring because of a single read failure.
        }
    }

    private bool IsSuppressed() => DateTimeOffset.UtcNow.UtcTicks < Interlocked.Read(ref _suppressUntilTicks);

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
