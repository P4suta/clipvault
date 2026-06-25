using ClipVault.Application.Abstractions;

namespace ClipVault.App.Tests;

/// <summary>A synchronous <see cref="IUiDispatcher"/> that runs work inline (no real UI thread needed in tests).</summary>
internal sealed class FakeUiDispatcher : IUiDispatcher
{
    public bool HasThreadAccess => true;

    public void Post(Action action) => action();

    public Task EnqueueAsync(Func<Task> action) => action();

    public Task<T> EnqueueAsync<T>(Func<Task<T>> action) => action();
}
