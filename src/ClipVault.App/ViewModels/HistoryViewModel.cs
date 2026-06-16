using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using ClipVault.Application.Abstractions;
using ClipVault.Application.Clipboard;
using ClipVault.Application.History;
using ClipVault.Application.Insights;
using ClipVault.Application.Messages;
using ClipVault.Domain.ValueObjects;
using ClipVaultApp.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace ClipVaultApp.ViewModels;

/// <summary>
/// Main ViewModel for the history list. It bundles search, filtering, paste-back, pin,
/// delete, and clear-all, and reloads when a history-changed message arrives. Marshalling
/// onto the UI thread is done through <see cref="IUiDispatcher"/>.
/// </summary>
public sealed partial class HistoryViewModel : ObservableObject, IRecipient<HistoryChangedMessage>, IDisposable
{
    /// <summary>Maximum number of characters shown in the full-content view (the full payload is still copied).</summary>
    private const int MaxDetailTextLength = 1_000_000;

    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(150);

    private readonly HistoryQueryService _queryService;
    private readonly ClipboardActionService _actionService;
    private readonly ICaptureStateService _captureState;
    private readonly IMessenger _messenger;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly ILocalizationService _loc;

    /// <summary>The stable "all kinds" option, always present at the top of the kind filter.</summary>
    private readonly KindFilterOption _allKindsOption;

    /// <summary>The stable "all apps" option, always present at the top of the app filter.</summary>
    private readonly AppFilterOption _allAppsOption;

    /// <summary>Used for search debounce; cancels the previous wait when typing continuously.</summary>
    private CancellationTokenSource? _searchDebounceCts;

    /// <summary>Flag to avoid redundant concurrent reloads.</summary>
    private bool _isLoading;

    /// <summary>Suppresses the reload triggered by filter-selection changes made while rebuilding the option lists.</summary>
    private bool _suppressFilterReload;

    /// <summary>The content shown in the full-content view, kept so the copy action can use the full payload.</summary>
    private ClipContent? _detailContent;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryViewModel"/> class.
    /// </summary>
    /// <param name="queryService">The service used to query history entries.</param>
    /// <param name="actionService">The service used to perform clipboard actions.</param>
    /// <param name="captureState">The service that tracks the clipboard capture state.</param>
    /// <param name="messenger">The messenger used to receive history-changed messages.</param>
    /// <param name="uiDispatcher">The dispatcher used to marshal work onto the UI thread.</param>
    /// <param name="loc">The localization service used for detail-view fallback strings.</param>
    public HistoryViewModel(
        HistoryQueryService queryService,
        ClipboardActionService actionService,
        ICaptureStateService captureState,
        IMessenger messenger,
        IUiDispatcher uiDispatcher,
        ILocalizationService loc)
    {
        _queryService = queryService;
        _actionService = actionService;
        _captureState = captureState;
        _messenger = messenger;
        _uiDispatcher = uiDispatcher;
        _loc = loc;

        // Filter options start with only the "all" entries; the concrete kinds and apps are filled
        // in on each reload from the facets that actually exist in the history.
        _allKindsOption = new KindFilterOption(_loc.GetString("Main.Filter.All"), null);
        _allAppsOption = new AppFilterOption(_loc.GetString("Main.Filter.AllApps"), null);
        KindFilters.Add(_allKindsOption);
        AppFilters.Add(_allAppsOption);
        SelectedKindFilter = _allKindsOption;
        SelectedAppFilter = _allAppsOption;

        IsPaused = _captureState.IsPaused;
        _captureState.StateChanged += OnCaptureStateChanged;

        // Subscribe to HistoryChangedMessage.
        _messenger.RegisterAll(this);
    }

    /// <summary>
    /// Occurs right after an entry has been pasted back (written to the clipboard) successfully.
    /// The presentation layer (App) handles this to hide the window and automatically paste
    /// (synthesize Ctrl+V) into the captured target. The ViewModel itself does not know the HWND
    /// (separation of concerns).
    /// </summary>
    public event EventHandler? PasteRequested;

    /// <summary>Gets the collection of entries shown in the list.</summary>
    public ObservableCollection<EntryViewModel> Entries { get; } = [];

    /// <summary>Gets or sets the current search text.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether clipboard capture is paused.</summary>
    [ObservableProperty]
    public partial bool IsPaused { get; set; }

    /// <summary>Gets a value indicating whether the list is currently empty.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <summary>Gets a value indicating whether the full-content view is shown instead of the list.</summary>
    [ObservableProperty]
    public partial bool IsDetailOpen { get; private set; }

    /// <summary>Gets the full text shown in the full-content view (<see langword="null"/> for image entries).</summary>
    [ObservableProperty]
    public partial string? DetailText { get; private set; }

    /// <summary>Gets the full-size image shown in the full-content view (<see langword="null"/> for text entries).</summary>
    [ObservableProperty]
    public partial ImageSource? DetailImageSource { get; private set; }

    /// <summary>Gets a value indicating whether the full-content view is showing an image.</summary>
    [ObservableProperty]
    public partial bool DetailIsImage { get; private set; }

    /// <summary>Gets the source-application title shown in the full-content view header.</summary>
    [ObservableProperty]
    public partial string? DetailTitle { get; private set; }

    /// <summary>Gets the available content-kind filter options (only the kinds that exist in the history).</summary>
    public ObservableCollection<KindFilterOption> KindFilters { get; } = [];

    /// <summary>Gets the available source-application filter options (only the apps that exist in the history).</summary>
    public ObservableCollection<AppFilterOption> AppFilters { get; } = [];

    /// <summary>Gets or sets the selected content-kind filter (the "all" option carries a null kind).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilterActive))]
    public partial KindFilterOption? SelectedKindFilter { get; set; }

    /// <summary>Gets or sets the selected source-application filter (the "all" option carries a null app).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilterActive))]
    public partial AppFilterOption? SelectedAppFilter { get; set; }

    /// <summary>Gets a value indicating whether any filter is narrowing the list (used to mark the filter button).</summary>
    public bool IsFilterActive => SelectedKindFilter?.Kind is not null || SelectedAppFilter?.App is not null;

    /// <summary>Gets a value indicating whether the detail view content is a URL with strippable tracking parameters.</summary>
    [ObservableProperty]
    public partial bool CanCleanUrl { get; private set; }

    /// <summary>Gets a value indicating whether the detail view content is valid JSON that can be reformatted.</summary>
    [ObservableProperty]
    public partial bool CanFormatJson { get; private set; }

    /// <summary>
    /// Receives a history-changed message. Because it can arrive on any thread, it marshals
    /// onto the UI thread before reloading.
    /// </summary>
    /// <param name="message">The history-changed message.</param>
    public void Receive(HistoryChangedMessage message)
    {
        _uiDispatcher.Post(() => _ = ReloadAsync());
    }

    /// <summary>
    /// Decodes the thumbnail for an item when its row becomes visible. Only items present in the
    /// list are targeted (to avoid wasteful decoding of items removed by filtering or deletion).
    /// </summary>
    /// <param name="entry">The entry whose thumbnail should be ensured.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task EnsureThumbnailForAsync(EntryViewModel entry) =>
        entry is not null && Entries.Contains(entry) ? entry.EnsureThumbnailAsync() : Task.CompletedTask;

    /// <inheritdoc/>
    public void Dispose()
    {
        _captureState.StateChanged -= OnCaptureStateChanged;
        _messenger.UnregisterAll(this);
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();

        // Zero any plaintext still held by an open detail view.
        _detailContent?.Dispose();
        _detailContent = null;
    }

    /// <summary>Initial load. Call once when the window is shown.</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        await ReloadAsync();
    }

    /// <summary>Writes the selected entry back to the clipboard (Enter / double-click / default action).</summary>
    [RelayCommand]
    private async Task PasteAsync(EntryViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var copied = await _actionService.CopyToClipboardAsync(item.Entry);

        // The reload is triggered by receiving HistoryChangedMessage.

        // Request automatic paste-back only when the write succeeded (otherwise finish with copy only).
        if (copied)
        {
            PasteRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Toggles the pinned state.</summary>
    [RelayCommand]
    private async Task TogglePinAsync(EntryViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        await _actionService.TogglePinAsync(item.Entry);
    }

    /// <summary>Deletes the entry.</summary>
    [RelayCommand]
    private async Task DeleteAsync(EntryViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        await _actionService.DeleteAsync(item.Entry);
    }

    /// <summary>Clears the entire history.</summary>
    [RelayCommand]
    private async Task ClearAllAsync()
    {
        await _actionService.ClearAllAsync();
    }

    /// <summary>Opens the read-only full-content view for the selected entry (full text, or the full-size image).</summary>
    /// <param name="item">The entry to show, or <see langword="null"/> when invoked without a selection.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort full-size image decode; shows a fallback message on failure.")]
    [RelayCommand]
    private async Task ViewDetailAsync(EntryViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var content = await _actionService.MaterializeForViewAsync(item.Entry);
        if (content is null)
        {
            return;
        }

        _detailContent?.Dispose();
        _detailContent = content;
        DetailTitle = item.SourceName;
        CanCleanUrl = false;
        CanFormatJson = false;

        if (content.Type == ClipContentType.Image)
        {
            DetailText = null;
            DetailIsImage = true;
            try
            {
                var bitmap = new BitmapImage();
                using var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream))
                {
                    writer.WriteBytes(content.Payload);
                    await writer.StoreAsync();
                    await writer.FlushAsync();
                    writer.DetachStream();
                }

                stream.Seek(0);
                await bitmap.SetSourceAsync(stream);
                DetailImageSource = bitmap;
            }
            catch
            {
                DetailImageSource = null;
                DetailIsImage = false;
                DetailText = _loc.GetString("History.ImageRenderFailed");
            }
        }
        else
        {
            DetailImageSource = null;
            DetailIsImage = false;
            var text = Encoding.UTF8.GetString(content.Payload);
            DetailText = text.Length > MaxDetailTextLength
                ? string.Concat(text.AsSpan(0, MaxDetailTextLength), _loc.GetString("History.Truncated"))
                : text;

            // Quick-action availability is computed precisely from the full payload, not the preview.
            CanCleanUrl = UrlTrackingStripper.TryStrip(text, out _);
            CanFormatJson = JsonReformatter.TryFormat(text, indented: true, out _);
        }

        IsDetailOpen = true;
    }

    /// <summary>Copies the detail-view URL back to the clipboard with tracking parameters removed.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [RelayCommand]
    private async Task CopyCleanedUrlAsync()
    {
        if (_detailContent is null)
        {
            return;
        }

        var text = Encoding.UTF8.GetString(_detailContent.Payload);
        if (!UrlTrackingStripper.TryStrip(text, out var cleaned))
        {
            return;
        }

        using var content = new ClipContent(ClipContentType.Text, Encoding.UTF8.GetBytes(cleaned));
        await _actionService.CopyForViewAsync(content);
    }

    /// <summary>Copies the detail-view JSON back to the clipboard, pretty-printed.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [RelayCommand]
    private async Task CopyFormattedJsonAsync()
    {
        if (_detailContent is null)
        {
            return;
        }

        var text = Encoding.UTF8.GetString(_detailContent.Payload);
        if (!JsonReformatter.TryFormat(text, indented: true, out var formatted))
        {
            return;
        }

        using var content = new ClipContent(ClipContentType.Text, Encoding.UTF8.GetBytes(formatted));
        await _actionService.CopyForViewAsync(content);
    }

    /// <summary>Copies the content currently shown in the full-content view back to the clipboard.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [RelayCommand]
    private async Task CopyDetailAsync()
    {
        if (_detailContent is not null)
        {
            await _actionService.CopyForViewAsync(_detailContent);
        }
    }

    /// <summary>Closes the full-content view and releases the materialized content from memory.</summary>
    [RelayCommand]
    private void CloseDetail()
    {
        IsDetailOpen = false;
        DetailText = null;
        DetailImageSource = null;
        DetailTitle = null;
        CanCleanUrl = false;
        CanFormatJson = false;
        _detailContent?.Dispose();
        _detailContent = null;
    }

    /// <summary>Toggles pausing/resuming capture.</summary>
    [RelayCommand]
    private void TogglePause()
    {
        _captureState.Toggle();

        // IsPaused is synchronized through the StateChanged handler.
    }

    /// <summary>Debounced reload when SearchText changes (auto-generated hook from CommunityToolkit).</summary>
    /// <param name="value">The new search text value.</param>
    partial void OnSearchTextChanged(string value)
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;

        _ = DebounceReloadAsync(cts.Token);
    }

    /// <summary>Reloads when the content-kind filter changes, unless the change came from rebuilding the option lists.</summary>
    /// <param name="value">The newly selected filter option.</param>
    partial void OnSelectedKindFilterChanged(KindFilterOption? value)
    {
        if (!_suppressFilterReload)
        {
            _ = ReloadAsync();
        }
    }

    /// <summary>Reloads when the source-application filter changes, unless the change came from rebuilding the option lists.</summary>
    /// <param name="value">The newly selected filter option.</param>
    partial void OnSelectedAppFilterChanged(AppFilterOption? value)
    {
        if (!_suppressFilterReload)
        {
            _ = ReloadAsync();
        }
    }

    /// <summary>Resolves the localized label for a content kind.</summary>
    /// <param name="kind">The content kind to label.</param>
    /// <returns>The localized label.</returns>
    private string KindLabelFor(ContentKind kind) => _loc.GetString(kind switch
    {
        ContentKind.Url => "Kind.Url",
        ContentKind.Email => "Kind.Email",
        ContentKind.Color => "Kind.Color",
        ContentKind.Json => "Kind.Json",
        ContentKind.Number => "Kind.Number",
        ContentKind.Image => "Kind.Image",
        _ => "Kind.Text",
    });

    /// <summary>Keeps IsPaused in sync with the service state when toggled from the toggle button.</summary>
    /// <param name="value">The new paused value.</param>
    partial void OnIsPausedChanged(bool value)
    {
        if (_captureState.IsPaused == value)
        {
            return;
        }

        if (value)
        {
            _captureState.Pause();
        }
        else
        {
            _captureState.Unpause();
        }
    }

    private async Task DebounceReloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SearchDebounce, cancellationToken);
            await ReloadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cancelled by a subsequent keystroke. Ignore.
        }
    }

    private async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        try
        {
            var facets = await _queryService.GetFacetsAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            SyncFilterOptions(facets);

            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;
            var entries = await _queryService.QueryAsync(
                search,
                kindFilter: SelectedKindFilter?.Kind,
                sourceApp: SelectedAppFilter?.App,
                cancellationToken: cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            Entries.Clear();
            foreach (var entry in entries)
            {
                var kind = ContentInsightService.Classify(entry);
                Entries.Add(new EntryViewModel(entry, kind, KindLabelFor(kind)));
            }

            IsEmpty = Entries.Count == 0;
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Rebuilds the filter selectors so they offer only the kinds and apps that actually exist, while
    /// preserving the current selection (falling back to the "all" option when the selected facet disappears).
    /// </summary>
    /// <param name="facets">The facets present across the whole history.</param>
    private void SyncFilterOptions(HistoryFacets facets)
    {
        _suppressFilterReload = true;
        try
        {
            if (!KindFiltersMatch(facets.Kinds))
            {
                var previous = SelectedKindFilter?.Kind;
                KindFilters.Clear();
                KindFilters.Add(_allKindsOption);
                foreach (var kind in facets.Kinds)
                {
                    KindFilters.Add(new KindFilterOption(KindLabelFor(kind), kind));
                }

                SelectedKindFilter = KindFilters.FirstOrDefault(option => option.Kind == previous) ?? _allKindsOption;
            }

            if (!AppFiltersMatch(facets.SourceApps))
            {
                var previous = SelectedAppFilter?.App;
                AppFilters.Clear();
                AppFilters.Add(_allAppsOption);
                foreach (var app in facets.SourceApps)
                {
                    AppFilters.Add(new AppFilterOption(app, app));
                }

                SelectedAppFilter = AppFilters.FirstOrDefault(option =>
                    string.Equals(option.App, previous, StringComparison.OrdinalIgnoreCase)) ?? _allAppsOption;
            }
        }
        finally
        {
            _suppressFilterReload = false;
        }
    }

    /// <summary>Determines whether the kind filter already offers exactly the given kinds (plus the "all" option).</summary>
    /// <param name="present">The content kinds present in the history, in enum order.</param>
    /// <returns><see langword="true"/> when no rebuild is needed.</returns>
    private bool KindFiltersMatch(IReadOnlyList<ContentKind> present)
    {
        if (KindFilters.Count != present.Count + 1)
        {
            return false;
        }

        for (var i = 0; i < present.Count; i++)
        {
            if (KindFilters[i + 1].Kind != present[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether the app filter already offers exactly the given apps (plus the "all" option).</summary>
    /// <param name="present">The source-application names present in the history, in display order.</param>
    /// <returns><see langword="true"/> when no rebuild is needed.</returns>
    private bool AppFiltersMatch(IReadOnlyList<string> present)
    {
        if (AppFilters.Count != present.Count + 1)
        {
            return false;
        }

        for (var i = 0; i < present.Count; i++)
        {
            if (!string.Equals(AppFilters[i + 1].App, present[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void OnCaptureStateChanged(object? sender, EventArgs e)
    {
        // StateChanged may fire on any thread, so marshal onto the UI thread.
        _uiDispatcher.Post(() => IsPaused = _captureState.IsPaused);
    }
}
