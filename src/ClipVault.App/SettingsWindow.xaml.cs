using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using ClipVaultApp.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace ClipVaultApp;

/// <summary>
/// The settings window. It does not apply the popup chrome and is shown as a normal window.
/// The left navigation switches between the "settings" and "privacy" panes. Because a PasswordBox
/// is unsuited to two-way binding of dependency properties, it flows to the ViewModel via
/// PasswordChanged. The panic-wipe confirmation (ContentDialog) and the actual exit are handled by
/// this window (because a XamlRoot is required).
/// </summary>
public sealed partial class SettingsWindow : Window
{
    /// <summary>
    /// Reference used to reach the remove-exclusion command from x:Bind static methods inside a
    /// DataTemplate. SettingsWindow and SettingsViewModel are a single display instance, so it is safe.
    /// </summary>
    private static SettingsViewModel? _sharedViewModel;

    private bool _isClosed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The settings ViewModel that backs this window.</param>
    public SettingsWindow(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        ShareViewModel(viewModel);

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");

        // Window.Title / TitleBar.Subtitle are not dependency properties, so they are set here, not via {loc:Str}.
        Title = App.Localization.GetString("Settings.WindowTitle");
        AppTitleBar.Subtitle = App.Localization.GetString("Settings.Subtitle");

        ViewModel.PanicWipeRequested += OnPanicWipeRequested;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        AppWindow.Closing += OnClosing;
    }

    /// <summary>Gets the command used by each row's "delete" button in the exclusion list.</summary>
    public static ICommand? RemoveExcludedAppCommand => _sharedViewModel?.RemoveExcludedAppCommand;

    /// <summary>Gets the settings ViewModel that backs this window.</summary>
    public SettingsViewModel ViewModel { get; }

    /// <summary>Converts a bool to a Visibility (for x:Bind function binding).</summary>
    /// <param name="value">The boolean value to convert.</param>
    /// <returns>Visible when the value is true; otherwise Collapsed.</returns>
    public static Visibility BoolToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Inverts a bool (for negated IsEnabled binding).</summary>
    /// <param name="value">The boolean value to invert.</param>
    /// <returns>The logical negation of the value.</returns>
    public static bool IsNot(bool value) => !value;

    /// <summary>Brings the window to the foreground (reactivates it if already open).</summary>
    public void ShowAndActivate()
    {
        AppWindow.Show();
        Activate();
    }

    /// <summary>
    /// Shares the constructed window's VM into the static field (for the DataTemplate static binding).
    /// Concentrates the static-field write into a dedicated place outside the constructor.
    /// </summary>
    /// <param name="viewModel">The ViewModel to share.</param>
    private static void ShareViewModel(SettingsViewModel viewModel) => _sharedViewModel = viewModel;

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // Clean up and then allow the default disposal (the settings window is not kept resident).
        _isClosed = true;
        ViewModel.PanicWipeRequested -= OnPanicWipeRequested;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        AppWindow.Closing -= OnClosing;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isClosed)
        {
            return;
        }

        // When the VM clears the passphrase fields, sync the PasswordBox display to empty as well.
        if (e.PropertyName is nameof(SettingsViewModel.CurrentPassphrase)
            or nameof(SettingsViewModel.NewPassphrase)
            or nameof(SettingsViewModel.ConfirmPassphrase))
        {
            SyncPassphraseBoxesIfCleared();
        }
    }

    // --- Navigation ---
    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        var showPrivacy = string.Equals(tag, "privacy", StringComparison.Ordinal);

        SettingsPane.Visibility = showPrivacy ? Visibility.Collapsed : Visibility.Visible;
        PrivacyPane.Visibility = showPrivacy ? Visibility.Visible : Visibility.Collapsed;
    }

    // --- PasswordBox -> ViewModel ---
    private void OnCurrentPassphraseChanged(object sender, RoutedEventArgs e) =>
        ViewModel.CurrentPassphrase = CurrentPassphraseBox.Password;

    private void OnNewPassphraseChanged(object sender, RoutedEventArgs e) =>
        ViewModel.NewPassphrase = NewPassphraseBox.Password;

    private void OnConfirmPassphraseChanged(object sender, RoutedEventArgs e) =>
        ViewModel.ConfirmPassphrase = ConfirmPassphraseBox.Password;

    // PasswordBox flows one way, so on a VM-side clear, reset its display to empty explicitly.
    private void SyncPassphraseBoxesIfCleared()
    {
        if (string.IsNullOrEmpty(ViewModel.CurrentPassphrase) && CurrentPassphraseBox.Password.Length > 0)
        {
            CurrentPassphraseBox.Password = string.Empty;
        }

        if (string.IsNullOrEmpty(ViewModel.NewPassphrase) && NewPassphraseBox.Password.Length > 0)
        {
            NewPassphraseBox.Password = string.Empty;
        }

        if (string.IsNullOrEmpty(ViewModel.ConfirmPassphrase) && ConfirmPassphraseBox.Password.Length > 0)
        {
            ConfirmPassphraseBox.Password = string.Empty;
        }
    }

    // --- Excluded apps ---
    private void OnNewExcludedAppKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.AddExcludedAppCommand.CanExecute(null))
        {
            ViewModel.AddExcludedAppCommand.Execute(null);
            e.Handled = true;
        }
    }

    // --- Panic wipe ---
    [SuppressMessage(
        "Usage",
        "VSTHRD100:Avoid async void methods",
        Justification = "VM event handler on the window side (needs ContentDialog/XamlRoot); wrapped in try/catch.")]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort UI handler; a wipe/exit failure is logged, not thrown.")]
    private async void OnPanicWipeRequested(object? sender, EventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = App.Localization.GetString("Settings.Panic.Dialog.Title"),
            Content = App.Localization.GetString("Settings.Panic.Dialog.Body"),
            PrimaryButtonText = App.Localization.GetString("Settings.Panic.Dialog.Confirm"),
            CloseButtonText = App.Localization.GetString("Common.Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await ViewModel.ExecutePanicWipeAsync();

            // Stop the host, clean up the tray, etc., and then exit the app (use the App exit path).
            await App.RequestExitAsync();
        }
        catch (Exception ex)
        {
            // Type + message only — never the full exception object.
            Debug.WriteLine($"[ClipVault] Panic wipe failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
