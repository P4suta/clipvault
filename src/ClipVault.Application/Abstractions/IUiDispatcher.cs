namespace ClipVault.Application.Abstractions;

/// <summary>
/// Abstraction for marshalling work onto the UI thread. Because the WinRT clipboard APIs must be used on the UI
/// thread, Infrastructure reaches the UI thread through this seam. The implementation lives in App (a DispatcherQueue wrapper).
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Gets a value indicating whether the current thread is the UI thread.</summary>
    bool HasThreadAccess { get; }

    /// <summary>Posts an action to run on the UI thread without waiting for it to complete.</summary>
    /// <param name="action">The action to run on the UI thread.</param>
    void Post(Action action);

    /// <summary>Enqueues an asynchronous action on the UI thread and awaits its completion.</summary>
    /// <param name="action">The asynchronous action to run on the UI thread.</param>
    /// <returns>A task that completes when the action has run.</returns>
    Task EnqueueAsync(Func<Task> action);

    /// <summary>Enqueues an asynchronous action on the UI thread and awaits its result.</summary>
    /// <typeparam name="T">The type of the result produced by the action.</typeparam>
    /// <param name="action">The asynchronous action to run on the UI thread.</param>
    /// <returns>A task that produces the result of the action.</returns>
    Task<T> EnqueueAsync<T>(Func<Task<T>> action);
}
