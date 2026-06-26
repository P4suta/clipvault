using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using ClipVault.Application.Abstractions;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using Microsoft.UI.Xaml.Data;
using Windows.Foundation;

namespace ClipVaultApp.ViewModels;

/// <summary>
/// An observable collection that loads history one keyset page at a time as the ListView scrolls
/// (<see cref="ISupportIncrementalLoading"/>). Only the pages actually scrolled into view are materialized, so the
/// list's memory stays bounded no matter how large the history is. Appending is marshalled onto the UI thread.
/// </summary>
public sealed class IncrementalHistoryCollection : ObservableCollection<EntryViewModel>, ISupportIncrementalLoading, IDisposable
{
    private readonly int _pageSize;
    private readonly Func<HistoryCursor?, int, CancellationToken, Task<HistoryPage>> _fetch;
    private readonly Func<ClipboardEntry, EntryViewModel> _project;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly CancellationTokenSource _cts = new();

    private HistoryCursor? _cursor;
    private bool _hasMore = true;
    private bool _isLoading;

    /// <summary>Initializes a new instance of the <see cref="IncrementalHistoryCollection"/> class.</summary>
    /// <param name="pageSize">The number of entries to request per page.</param>
    /// <param name="fetch">Fetches one page after a cursor (the streaming, filtered query).</param>
    /// <param name="project">Projects a domain entry into its row view-model.</param>
    /// <param name="uiDispatcher">Marshals the appends onto the UI thread.</param>
    public IncrementalHistoryCollection(
        int pageSize,
        Func<HistoryCursor?, int, CancellationToken, Task<HistoryPage>> fetch,
        Func<ClipboardEntry, EntryViewModel> project,
        IUiDispatcher uiDispatcher)
    {
        _pageSize = pageSize;
        _fetch = fetch;
        _project = project;
        _uiDispatcher = uiDispatcher;
    }

    /// <inheritdoc/>
    public bool HasMoreItems => _hasMore;

    /// <inheritdoc/>
    /// <param name="count">The number of items the host requests; treated as a hint (a page is loaded instead).</param>
    /// <returns>An async operation producing the number of items actually loaded.</returns>
    public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count) =>
        AsyncInfo.Run(token => LoadCoreAsync(token));

    /// <summary>Eagerly loads the first page (primes the initial display and makes tests deterministic).</summary>
    /// <param name="cancellationToken">A token to cancel the load (e.g. when a newer query supersedes it).</param>
    /// <returns>A task that completes when the first page has loaded.</returns>
    public Task LoadFirstPageAsync(CancellationToken cancellationToken = default) => LoadCoreAsync(cancellationToken);

    /// <inheritdoc/>
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task<LoadMoreItemsResult> LoadCoreAsync(CancellationToken external)
    {
        if (_isLoading || !_hasMore || _cts.IsCancellationRequested)
        {
            return new LoadMoreItemsResult { Count = 0 };
        }

        _isLoading = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(external, _cts.Token);
        try
        {
            var page = await _fetch(_cursor, _pageSize, linked.Token).ConfigureAwait(false);
            await _uiDispatcher.EnqueueAsync(() =>
            {
                foreach (var entry in page.Entries)
                {
                    Add(_project(entry));
                }

                return Task.CompletedTask;
            });

            _cursor = page.NextCursor;
            _hasMore = page.NextCursor is not null;
            return new LoadMoreItemsResult { Count = (uint)page.Entries.Count };
        }
        catch (OperationCanceledException)
        {
            return new LoadMoreItemsResult { Count = 0 };
        }
        finally
        {
            _isLoading = false;
        }
    }
}
