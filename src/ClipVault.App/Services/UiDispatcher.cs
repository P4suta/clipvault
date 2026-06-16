using System.Diagnostics.CodeAnalysis;
using ClipVault.Application.Abstractions;
using Microsoft.UI.Dispatching;

namespace ClipVaultApp.Services;

/// <summary>
/// WinUI implementation of <see cref="IUiDispatcher"/> that wraps the UI thread's <see cref="DispatcherQueue"/>.
/// Because the WinRT clipboard APIs must run on the UI thread, Infrastructure calls into this seam.
/// Construct it as a singleton, passing the DispatcherQueue obtained on the UI thread.
/// </summary>
public sealed class UiDispatcher(DispatcherQueue dispatcherQueue) : IUiDispatcher
{
    private readonly DispatcherQueue _dispatcherQueue = dispatcherQueue
        ?? throw new ArgumentNullException(nameof(dispatcherQueue));

    /// <inheritdoc/>
    public bool HasThreadAccess => _dispatcherQueue.HasThreadAccess;

    /// <inheritdoc/>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!_dispatcherQueue.TryEnqueue(() => action()))
        {
            // The queue has stopped (for example, during shutdown). Silently ignore.
        }
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // TryEnqueue takes a void delegate; fire a task from a sync lambda and funnel exceptions into the TCS (no async void, so failures are observed).
        var enqueued = _dispatcherQueue.TryEnqueue(() => _ = RunAndSignalAsync(action, tcs));

        if (!enqueued)
        {
            tcs.SetException(new InvalidOperationException("Could not enqueue the work onto the UI thread queue."));
        }

        return tcs.Task;
    }

    /// <inheritdoc/>
    public Task<T> EnqueueAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        // As above, avoid an async lambda and funnel the result and exceptions into the TCS.
        var enqueued = _dispatcherQueue.TryEnqueue(() => _ = RunAndSignalAsync(action, tcs));

        if (!enqueued)
        {
            tcs.SetException(new InvalidOperationException("Could not enqueue the work onto the UI thread queue."));
        }

        return tcs.Task;
    }

    // All exceptions are funneled into the TCS, so the returned Task never faults (safe to fire and forget).
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Forwards the exception to the awaiter via tcs.SetException (not swallowed).")]
    private static async Task RunAndSignalAsync(Func<Task> action, TaskCompletionSource tcs)
    {
        try
        {
            await action();
            tcs.SetResult();
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Forwards the exception to the awaiter via tcs.SetException (not swallowed).")]
    private static async Task RunAndSignalAsync<T>(Func<Task<T>> action, TaskCompletionSource<T> tcs)
    {
        try
        {
            var result = await action();
            tcs.SetResult(result);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }
    }
}
