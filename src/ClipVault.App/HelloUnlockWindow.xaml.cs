using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ClipVault.Application.Abstractions;
using ClipVault.Infrastructure.Security;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace ClipVaultApp;

/// <summary>
/// The startup Windows Hello unlock gate. Because it is used before a XamlRoot exists (before the
/// host is built), it is implemented as a dedicated small window rather than a ContentDialog.
/// <see cref="KeyProtector.UnlockWithHelloAsync(IWindowsHello)"/> shows the OS Hello prompt
/// (face / fingerprint / PIN) and, on success, returns the decrypted DEK. Failure / cancellation can
/// be retried. On success the DEK (the caller stores it into <see cref="IResolvedMasterKey"/>), or on
/// exit / close null, can be received via <see cref="ResultTask"/>.
/// </summary>
public sealed partial class HelloUnlockWindow : Window
{
    private readonly KeyProtector _keyProtector;
    private readonly IWindowsHello _hello;
    private readonly TaskCompletionSource<byte[]?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool _isBusy;
    private bool _isCompleted;

    /// <summary>
    /// Initializes a new instance of the <see cref="HelloUnlockWindow"/> class.
    /// </summary>
    /// <param name="keyProtector">The key protector used to unlock the DEK with Hello.</param>
    /// <param name="hello">The Windows Hello service used to authenticate.</param>
    public HelloUnlockWindow(KeyProtector keyProtector, IWindowsHello hello)
    {
        _keyProtector = keyProtector ?? throw new ArgumentNullException(nameof(keyProtector));
        _hello = hello ?? throw new ArgumentNullException(nameof(hello));

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

        ResizeForDpi(440, 360);

        // "Close (X)" is treated as cancel. Return null so the app side exits.
        AppWindow.Closing += OnClosing;

        Activated += OnActivated;
    }

    /// <summary>
    /// Gets the resolution result: on success the decrypted DEK, or on exit / window close null.
    /// </summary>
    public Task<byte[]?> ResultTask => _completion.Task;

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
        // Right after startup, show the Hello prompt automatically once (afterwards retry manually on failure).
        Activated -= OnActivated;
        _ = DispatcherQueue.TryEnqueue(() => _ = TryUnlockAsync());
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

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Retry boundary for Hello unlock; catch all, show an error, allow retry.")]
    private async Task TryUnlockAsync()
    {
        if (_isBusy || _isCompleted)
        {
            return;
        }

        SetBusy(true);
        ErrorBar.IsOpen = false;

        byte[]? dek = null;
        try
        {
            // UnlockWithHelloAsync shows the OS Hello prompt and, on success, decrypts and returns the DEK.
            dek = await _keyProtector.UnlockWithHelloAsync(_hello);
        }
        catch (Exception)
        {
            // Authentication failure / cancellation / no credential, etc. Show only an error so it can be retried.
            dek = null;
        }

        SetBusy(false);

        if (dek is not null)
        {
            Complete(dek);
        }
        else
        {
            ShowError(App.Localization.GetString("Unlock.Hello.Failed"));
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
    }

    private void Complete(byte[]? dek)
    {
        if (_isCompleted)
        {
            return;
        }

        _isCompleted = true;
        _completion.TrySetResult(dek);
        AppWindow.Closing -= OnClosing;
        Close();
    }
}
