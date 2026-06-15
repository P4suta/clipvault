using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ClipVault.Application.Retention;
using ClipVault.Domain.Abstractions;
using Microsoft.Extensions.Hosting;

namespace ClipVault.Infrastructure.Hosting;

/// <summary>
/// A cleanup service that periodically applies the retention policy (limits on age, count, and total size,
/// excluding pinned entries). It runs once shortly after startup and at a fixed interval thereafter.
/// </summary>
/// <param name="retention">The retention service that enforces the policy.</param>
/// <param name="clock">The clock used to determine the current time when enforcing the policy.</param>
public sealed class RetentionHostedService(RetentionService retention, IClock clock) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    /// <inheritdoc/>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort boundary: a single cleanup failure must not stop the periodic service.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await retention.EnforceAsync(clock.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Log type + message only; never the full exception object.
                Debug.WriteLine($"[ClipVault] Retention cleanup failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
