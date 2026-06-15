using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using ClipVaultApp.Platform;
using ClipVaultApp.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace ClipVaultApp;

/// <summary>
/// The main window that displays the clipboard history. It does not use the template's Frame/MainPage
/// and hosts the history UI directly. It keeps the TitleBar and MicaBackdrop while behaving as a
/// tray-resident "popup" (always on top, hidden from switchers, closes on Esc / deactivation).
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// Reference used to reach the row commands from x:Bind static methods inside a DataTemplate.
    /// MainWindow and HistoryViewModel are both DI singletons, so a single instance is safe.
    /// </summary>
    private static HistoryViewModel? _sharedViewModel;

    /// <summary>Flag to perform the initial load only on the first activation.</summary>
    private bool _isFirstActivationDone;

    /// <summary>
    /// Flag to reliably redirect "close" to hiding into the tray. Only on a real exit
    /// (tray "exit") is it set to false to allow the default close.
    /// </summary>
    private bool _hideInsteadOfClose = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The history ViewModel that backs this window.</param>
    public MainWindow(HistoryViewModel viewModel)
    {
        ViewModel = viewModel;
        ShareViewModel(viewModel);

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        // Apply the clipboard-popup chrome (always on top, hidden from switchers, etc.).
        PopupWindowChrome.Apply(AppWindow);

        // "Close" hides into the tray (does not stop the host). A real exit happens only via the App path.
        AppWindow.Closing += OnAppWindowClosing;

        // Load on show and focus the search box (keyboard first).
        Activated += OnActivated;
    }

    /// <summary>Occurs when the header's "settings" button is pressed (the App opens the settings window).</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>Gets the command used by the row's pin button.</summary>
    public static ICommand? TogglePinCommand => _sharedViewModel?.TogglePinCommand;

    /// <summary>Gets the command used by the row's delete button.</summary>
    public static ICommand? DeleteCommand => _sharedViewModel?.DeleteCommand;

    /// <summary>Gets the command used by the row's view (full-content) button.</summary>
    public static ICommand? ViewDetailCommand => _sharedViewModel?.ViewDetailCommand;

    /// <summary>Gets the history ViewModel that backs this window.</summary>
    public HistoryViewModel ViewModel { get; }

    /// <summary>Gets a value indicating whether the window is currently visible (used for the tray left-click toggle).</summary>
    public bool IsWindowVisible => AppWindow.IsVisible;

    /// <summary>Converts a bool to a Visibility (for x:Bind function binding).</summary>
    /// <param name="value">The boolean value to convert.</param>
    /// <returns>Visible when the value is true; otherwise Collapsed.</returns>
    public static Visibility BoolToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Converts a bool to a Visibility, inverted (for x:Bind function binding).</summary>
    /// <param name="value">The boolean value to convert.</param>
    /// <returns>Collapsed when the value is true; otherwise Visible.</returns>
    public static Visibility BoolToVisibilityInverse(bool value) =>
        value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Returns the glyph for the pin state (pinned = Unpin / unpinned = Pin, Segoe Fluent Icons).</summary>
    /// <param name="isPinned">Whether the entry is pinned.</param>
    /// <returns>The glyph string corresponding to the pin state.</returns>
    public static string PinGlyph(bool isPinned) =>
        isPinned ? char.ConvertFromUtf32(0xE77A) : char.ConvertFromUtf32(0xE718);

    /// <summary>
    /// Called by the App when it is truly safe to close the window (on app exit).
    /// After this, Close() disposes the window as usual.
    /// </summary>
    public void AllowClose() => _hideInsteadOfClose = false;

    /// <summary>Shows and brings the window to the foreground, and focuses the search box (called on each summon).</summary>
    public void ShowAndActivate()
    {
        AppWindow.Show();

        // Ensure it is brought to the foreground and activated even if already shown.
        Activate();

        // Return focus to the search box on every summon (so filtering can start immediately).
        FocusSearchBox();
    }

    /// <summary>Hides the window into the tray (does not dispose it) and resets the full-content view to the list.</summary>
    public void HideToTray()
    {
        // Reset to the list so the next summon does not reopen on a stale detail view.
        ViewModel.CloseDetailCommand.Execute(null);
        AppWindow.Hide();
    }

    /// <summary>
    /// Shares the constructed window's VM into the static field (for the DataTemplate static binding).
    /// Concentrates the static-field write into a dedicated place outside the constructor.
    /// </summary>
    /// <param name="viewModel">The ViewModel to share.</param>
    private static void ShareViewModel(HistoryViewModel viewModel) => _sharedViewModel = viewModel;

    [SuppressMessage(
        "Usage",
        "VSTHRD100:Avoid async void methods",
        Justification = "Activated has a fixed async void signature; the await is wrapped in try/catch.")]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort UI handler; an initial-load failure is logged, not thrown.")]
    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        // When deactivated (lost focus), close as a popup (= hide into the tray).
        // However, suppress this while the settings window is open (so it does not hide during settings operations).
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            if (_hideInsteadOfClose && AppWindow.IsVisible)
            {
                HideToTray();
            }

            return;
        }

        // Load history only on the first activation.
        if (!_isFirstActivationDone)
        {
            _isFirstActivationDone = true;
            try
            {
                await ViewModel.LoadCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                // Do not crash the UI if the initial load fails (continue with an empty list).
                // Type + message only — never the full exception object.
                Debug.WriteLine($"[ClipVault] Failed to load the initial history: {ex.GetType().Name}: {ex.Message}");
            }

            FocusSearchBox();
        }
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // By default, cancel "close" and hide into the tray (stay resident and keep the host alive).
        if (_hideInsteadOfClose)
        {
            args.Cancel = true;
            HideToTray();
        }
    }

    private void FocusSearchBox()
    {
        // Right after showing, focus may not land, so enqueue it on the UI queue to set it reliably.
        _ = DispatcherQueue.TryEnqueue(() => SearchBox.Focus(FocusState.Programmatic));
    }

    /// <summary>Header "settings" button: requests the App to open the settings window (does not hide the popup).</summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event data.</param>
    private void OnSettingsClick(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Closes the popup (hides into the tray) on Esc anywhere in the window.</summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The key event data.</param>
    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            HideToTray();
            e.Handled = true;
        }
    }

    /// <summary>Performs paste-back on item click / Enter / Space.</summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The item click event data.</param>
    private void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is EntryViewModel item && ViewModel.PasteCommand.CanExecute(item))
        {
            ViewModel.PasteCommand.Execute(item);
        }
    }

    /// <summary>Lazily decodes the thumbnail when each row is loaded (via the VM).</summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event data.</param>
    private void OnEntryRowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EntryViewModel item })
        {
            // Fire and forget is fine (EnsureThumbnailAsync handles exceptions internally and updates display on the UI thread).
            _ = ViewModel.EnsureThumbnailForAsync(item);
        }
    }

    /// <summary>Deletes the selected entry on the Delete key over the list.</summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The key event data.</param>
    private void OnHistoryListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Delete
            && HistoryList.SelectedItem is EntryViewModel item
            && ViewModel.DeleteCommand.CanExecute(item))
        {
            ViewModel.DeleteCommand.Execute(item);
            e.Handled = true;
        }
    }
}
