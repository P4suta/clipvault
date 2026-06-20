using System.Diagnostics.CodeAnalysis;
using ClipVault.Application.Clipboard;
using ClipVault.Domain.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClipVault.Infrastructure.Hosting;

/// <summary>
/// Keeps clipboard monitoring running for the entire app lifetime and bridges changes into the ingestion
/// pipeline. Ingestion (encryption and database writes) runs on a separate thread so it does not block the UI thread.
/// </summary>
/// <param name="monitor">The clipboard monitor that raises change notifications.</param>
/// <param name="ingestion">The ingestion service that processes each clipboard snapshot.</param>
/// <param name="logger">Logs best-effort ingestion failures (type and message only).</param>
public sealed class ClipboardMonitorHostedService(
    IClipboardMonitor monitor,
    ClipboardIngestionService ingestion,
    ILogger<ClipboardMonitorHostedService> logger) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        monitor.ClipboardChanged += OnClipboardChanged;
        await monitor.StartAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        monitor.ClipboardChanged -= OnClipboardChanged;
        await monitor.StopAsync(cancellationToken);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort boundary: a single ingestion failure must not stop the resident service.")]
    private void OnClipboardChanged(object? sender, ClipboardChangedEventArgs e)
    {
        var snapshot = e.Snapshot;
        _ = Task.Run(async () =>
        {
            try
            {
                await ingestion.IngestAsync(snapshot);
            }
            catch (Exception ex)
            {
                // Type and message only; never the full exception (avoid leaking clipboard content).
                logger.LogError("Ingestion failed: {ExceptionType}: {ExceptionMessage}", ex.GetType().Name, ex.Message);
            }
        });
    }
}
