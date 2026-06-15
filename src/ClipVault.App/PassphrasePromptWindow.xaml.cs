using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using ClipVault.Infrastructure.Security;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;

namespace ClipVaultApp;

/// <summary>
/// The startup passphrase entry gate. Because it is used before a XamlRoot exists (before the host
/// is built), it is implemented as a dedicated small window rather than a ContentDialog. Each entry
/// is validated with <see cref="KeyProtector.Unlock(string?)"/>, and on a mismatch the user is
/// prompted to re-enter. The validated passphrase (on success) or null (on exit / cancel) can be
/// received via <see cref="ResultTask"/>.
/// </summary>
public sealed partial class PassphrasePromptWindow : Window
{
    private readonly KeyProtector _keyProtector;
    private readonly TaskCompletionSource<string?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool _isBusy;
    private bool _isCompleted;

    /// <summary>
    /// Initializes a new instance of the <see cref="PassphrasePromptWindow"/> class.
    /// </summary>
    /// <param name="keyProtector">The key protector used to validate the entered passphrase.</param>
    public PassphrasePromptWindow(KeyProtector keyProtector)
    {
        _keyProtector = keyProtector ?? throw new ArgumentNullException(nameof(keyProtector));

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        Title = App.Localization.GetString("Unlock.WindowTitle");

        // Chrome of a compact unlock dialog (always on top, not resizable / minimizable).
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.IsShownInSwitchers = true;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        ResizeForDpi(440, 420);

        // "Close (X)" is treated as cancel. Return null so the app side exits.
        AppWindow.Closing += OnClosing;

        Activated += OnActivated;
    }

    /// <summary>
    /// Gets the resolution result: on success the validated passphrase, or on exit / window close null.
    /// </summary>
    public Task<string?> ResultTask => _completion.Task;

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    private void ResizeForDpi(int widthDip, int heightDip)
    {
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(widthDip * scale), (int)(heightDip * scale)));
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        // Right after startup, focus the input field once.
        Activated -= OnActivated;
        _ = DispatcherQueue.TryEnqueue(() => PassphraseBox.Focus(FocusState.Programmatic));
    }

    private void OnPassphraseKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            _ = TryUnlockAsync();
        }
    }

    private void OnUnlockClick(object sender, RoutedEventArgs e) => _ = TryUnlockAsync();

    private void OnQuitClick(object sender, RoutedEventArgs e) => Complete(null);

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // If closed while still incomplete, resolve as cancel (null).
        if (!_isCompleted)
        {
            _completion.TrySetResult(null);
            _isCompleted = true;
        }
    }

    private async Task TryUnlockAsync()
    {
        if (_isBusy || _isCompleted)
        {
            return;
        }

        var entered = PassphraseBox.Password;
        if (string.IsNullOrEmpty(entered))
        {
            ShowError(App.Localization.GetString("Unlock.Passphrase.Required"));
            return;
        }

        SetBusy(true);
        ErrorBar.IsOpen = false;

        // Argon2id is heavy, so validate on a separate thread to avoid blocking the UI.
        var ok = await Task.Run(() =>
        {
            try
            {
                var dek = _keyProtector.Unlock(entered);
                CryptographicOperations.ZeroMemory(dek);
                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
        });

        SetBusy(false);

        if (ok)
        {
            Complete(entered);
        }
        else
        {
            ShowError(App.Localization.GetString("Unlock.Passphrase.Wrong"));
            PassphraseBox.Password = string.Empty;
            PassphraseBox.Focus(FocusState.Programmatic);
        }
    }

    private void ShowError(string message)
    {
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        BusyRing.IsActive = busy;
        UnlockButton.IsEnabled = !busy;
        PassphraseBox.IsEnabled = !busy;
    }

    private void Complete(string? passphrase)
    {
        if (_isCompleted)
        {
            return;
        }

        _isCompleted = true;
        _completion.TrySetResult(passphrase);
        AppWindow.Closing -= OnClosing;
        Close();
    }
}
