using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading.Tasks;
using ClipVault.Application.Abstractions;
using ClipVault.Application.Clipboard;
using ClipVault.Infrastructure.Persistence;
using ClipVault.Infrastructure.Security;
using ClipVault.Infrastructure.Settings;
using ClipVaultApp.Localization;
using ClipVaultApp.Platform;
using ClipVaultApp.Services;
using ClipVaultApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace ClipVaultApp;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application, IDisposable
{
    // The same instance shared with DI (ResolvedKeyVault): it holds the DEK resolved at the startup gate.
    // It is populated only in the Windows Hello-protected disk mode; otherwise Dek == null.
    private readonly ResolvedMasterKey _resolvedKey = new();

    private IHost? _host;

    // Controller for the tray icon, hotkeys, and context menu (created after the window HWND is known).
    private TrayHotkeyController? _trayController;

    // The HWND of the paste-back target window captured on summon (hotkey / tray left-click).
    private nint _pasteTargetHwnd;

    // Flag to prevent a duplicate exit.
    private bool _isExiting;

    // The settings window (created once on demand, then reactivated thereafter).
    private SettingsWindow? _settingsWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();

        // Last-chance exception sinks; log the type name only (never plaintext). Termination is unchanged.
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>
    /// Gets the main application window. Use <c>App.Window</c> from any class that needs
    /// the window reference (for dialogs, pickers, interop, etc.).
    /// </summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>
    /// Gets the UI thread dispatcher. Use <c>App.DispatcherQueue</c> to marshal calls
    /// to the UI thread. Fully qualified to avoid CS0104 ambiguity with
    /// <see cref="Windows.System.DispatcherQueue"/>.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>
    /// Gets the application-wide DI container. It is used to resolve windows and ViewModels.
    /// It becomes available after the host is built in <see cref="OnLaunched"/>.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Gets the UI localization service. It is set at the very start of <see cref="StartupAsync"/>,
    /// before any window is created, so XAML (<c>{loc:Str}</c>) and code-behind can resolve strings.
    /// </summary>
    public static ILocalizationService Localization { get; private set; } = null!;

    /// <summary>
    /// Gets the entry point for stopping the host and exiting the app safely, e.g. from the settings
    /// window. The actual implementation is <see cref="ExitAsync"/> (assigned in OnLaunched).
    /// </summary>
    public static Func<Task> RequestExitAsync { get; private set; } = () => Task.CompletedTask;

    /// <summary>Gets the entry point that opens (or reactivates) the settings window. Called from the tray or header.</summary>
    public static Action OpenSettings { get; private set; } = () => { };

    /// <summary>
    /// Gets the native window handle (HWND). Use for file pickers,
    /// <c>DataTransferManager</c>, and any WinRT interop that requires
    /// <c>InitializeWithWindow</c>.
    /// </summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>
    /// Cleans up the disposable fields (host and tray controller). It is normally idempotent because
    /// <see cref="ExitAsync"/> releases them first. WinUI does not dispose the Application, but this
    /// is implemented to make ownership explicit.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    [SuppressMessage(
        "Usage",
        "VSTHRD100:Avoid async void methods",
        Justification = "OnLaunched has a fixed async void signature; the body is wrapped in try/catch.")]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Top-level startup boundary: log and exit safely instead of crashing.")]
    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            await StartupAsync();
        }
        catch (Exception ex)
        {
            // Log and exit safely. Type + message only (never the full exception).
            Debug.WriteLine($"[ClipVault] Startup failed: {ex.GetType().Name}: {ex.Message}");
            await ExitAsync();
        }
    }

    /// <summary>Releases the managed disposable fields (standard Dispose pattern).</summary>
    /// <param name="disposing">True to release managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _host?.Dispose();
        _host = null;

        _trayController?.Dispose();
        _trayController = null;
    }

    // Subscribed in the constructor; type name only.
    private static void OnUnhandledException(object sender, System.UnhandledExceptionEventArgs e) =>
        Debug.WriteLine($"[ClipVault] Fatal: {(e.ExceptionObject as Exception)?.GetType().Name ?? "unknown"}");

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Mark observed so it is not re-raised; we deliberately keep only the type name.
        e.SetObserved();
        Debug.WriteLine($"[ClipVault] Unobserved task exception: {e.Exception.GetType().Name}");
    }

    /// <summary>
    /// Applies the UI language process-wide before any window is created. The app is unpackaged, so the
    /// platform resource system cannot override the OS language in-process; a custom provider plus
    /// CultureInfo drives every surface (XAML, code-behind, tray, and date/number formatting).
    /// </summary>
    /// <param name="language">The configured language (<see cref="AppLanguage.System"/> follows the OS).</param>
    private static void ApplyUiLanguage(AppLanguage language)
    {
        Localization = new LocalizationService(language);
        var culture = CultureInfo.GetCultureInfo(Localization.CurrentCultureTag);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>Loads settings, runs the unlock gate, builds the host, shows the window, and starts monitoring, in order.</summary>
    private async Task StartupAsync()
    {
        // Capture the UI thread's DispatcherQueue (the foundation for IUiDispatcher).
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        // Load settings once (the implementation falls back to defaults if they are corrupt).
        var settings = JsonSettingsService.Load(JsonSettingsService.DefaultPath());

        // Apply the UI language before any window (including the unlock gate) is created.
        ApplyUiLanguage(settings.Current.Language);

        // Startup unlock gate: resolve the three protection modes of encrypted disk (DPAPI only /
        // passphrase / Windows Hello) before building the host (the volatile mode needs no gate
        // because the key is not on disk). Passphrase returns the validated string; Hello stores the
        // decrypted DEK into _resolvedKey and returns it.
        var unlock = await TryUnlockAtStartupAsync(settings.Current);

        // If the user abandons unlocking (chose to exit / pressed X), exit as-is.
        // This is before the host is built, but ExitAsync cleans up each resource null-safely, so await it and finish.
        if (unlock is UnlockResult.Cancelled)
        {
            await ExitAsync();
            return;
        }

        var validatedPassphrase = (unlock as UnlockResult.Unlocked)?.Passphrase;

        // Build the generic host and register the services of each layer.
        // In Hello mode, _resolvedKey.Dek is already populated and fixed before host.StartAsync
        // (ResolvedKeyVault reads the DEK lazily after the host has started).
        _host = Host.CreateApplicationBuilder()
            .ConfigureClipVault(DispatcherQueue, settings, validatedPassphrase, _resolvedKey)
            .Build();
        Services = _host.Services;

        // Fix the entry points for exit / open-settings from the outside (e.g. the settings window).
        RequestExitAsync = ExitAsync;
        OpenSettings = ShowSettingsWindow;

        // Resolve the main window from DI and show it.
        var window = Services.GetRequiredService<MainWindow>();
        Window = window;

        // Subscribe to the paste-back request (right after the VM writes to the clipboard).
        window.ViewModel.PasteRequested += OnPasteRequested;

        // Open the settings window from the header's "settings" button (without hiding the popup).
        window.SettingsRequested += OnSettingsRequested;

        Window.Activate();

        // Now that the HWND is known, install the tray, hotkeys, and subclass.
        InitializeTray(window);

        // Start clipboard monitoring (IHostedService).
        // RunAsync is not called because WinUI owns the message loop.
        await _host.StartAsync();
    }

    /// <summary>
    /// Startup unlock gate. Branches according to the protection mode of the encrypted-disk key file:.
    /// <list type="bullet">
    /// <item>DPAPI only (including when the key is not yet created): continue without a prompt.</item>
    /// <item>Passphrase: enter and validate in a dedicated window (<see cref="KeyProtector.Unlock(string?)"/>),
    /// and return the validated passphrase via <see cref="UnlockResult.Unlocked"/>.</item>
    /// <item>Windows Hello: decrypt the DEK via the OS Hello prompt (<see cref="KeyProtector.UnlockWithHelloAsync"/>),
    /// store it into <see cref="_resolvedKey"/>, and continue (retryable on failure).</item>
    /// </list>
    /// The volatile mode needs no gate because the key is not on disk.
    /// </summary>
    /// <param name="settings">The current settings used to decide the unlock path.</param>
    /// <returns>A task whose result is the unlock outcome.</returns>
    private async Task<UnlockResult> TryUnlockAtStartupAsync(ClipVaultSettings settings)
    {
        if (settings.Storage != StorageMode.EncryptedDisk)
        {
            // The volatile mode needs no gate because the key is not on disk.
            return new UnlockResult.Unlocked(null);
        }

        var keyProtector = new KeyProtector(ClipVaultStorageOptions.Default().KeyFilePath);

        if (keyProtector.Exists() && keyProtector.RequiresHello())
        {
            // Windows Hello protection: decrypt the DEK via the OS prompt and store it into the holder (with retry / exit).
            var helloPrompt = new HelloUnlockWindow(keyProtector, new WindowsHelloService());
            helloPrompt.Activate();

            var dek = await helloPrompt.ResultTask;
            if (dek is null)
            {
                return new UnlockResult.Cancelled();
            }

            _resolvedKey.Dek = dek;
            return new UnlockResult.Unlocked(null);
        }

        if (keyProtector.Exists() && keyProtector.RequiresPassphrase())
        {
            // Passphrase protection: pass the entered, validated passphrase to the provider.
            var prompt = new PassphrasePromptWindow(keyProtector);
            prompt.Activate();

            var entered = await prompt.ResultTask;
            return entered is null
                ? new UnlockResult.Cancelled()
                : new UnlockResult.Unlocked(entered);
        }

        // Key not yet created (first run) or DPAPI only => no prompt needed.
        return new UnlockResult.Unlocked(null);
    }

    /// <summary>Initializes tray residency and the global hotkey after the HWND is known.</summary>
    /// <param name="window">The main window whose HWND is used.</param>
    private void InitializeTray(MainWindow window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        var captureState = Services.GetRequiredService<ICaptureStateService>();
        var actionService = Services.GetRequiredService<ClipboardActionService>();

        var actions = new TrayActions
        {
            ShowWindow = window.ShowAndActivate,
            HideWindow = window.HideToTray,
            IsWindowVisible = () => window.IsWindowVisible,
            CaptureTarget = target => _pasteTargetHwnd = target,
            IsPaused = () => captureState.IsPaused,
            TogglePause = captureState.Toggle,
            ClearAllAsync = () => actionService.ClearAllAsync(),
            OpenSettings = ShowSettingsWindow,
            ExitAsync = ExitAsync,
        };

        _trayController = new TrayHotkeyController(hwnd, actions);
    }

    /// <summary>Header "settings" button request. Opens the settings window without hiding the popup.</summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event data.</param>
    private void OnSettingsRequested(object? sender, EventArgs e) => ShowSettingsWindow();

    /// <summary>Opens the settings window (reactivates it if already created). Does not hide the popup.</summary>
    private void ShowSettingsWindow()
    {
        // The main window is an always-on-top popup, so hide it before opening settings (so settings is not hidden behind it).
        if (Window is MainWindow main && main.IsWindowVisible)
        {
            main.HideToTray();
        }

        if (_settingsWindow is null)
        {
            _settingsWindow = Services.GetRequiredService<SettingsWindow>();
            _settingsWindow.Closed += OnSettingsWindowClosed;
        }

        _settingsWindow.ShowAndActivate();
    }

    private void OnSettingsWindowClosed(object sender, WindowEventArgs args)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Closed -= OnSettingsWindowClosed;
            _settingsWindow = null;
        }
    }

    /// <summary>
    /// Handles a paste-back request: hide the window, then send Ctrl+V to the captured target.
    /// If there is no target or bringing it to the foreground fails, finish quietly with the
    /// clipboard already set.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event data.</param>
    private void OnPasteRequested(object? sender, EventArgs e)
    {
        var target = _pasteTargetHwnd;

        if (Window is MainWindow window)
        {
            window.HideToTray();
        }

        // Right after hiding, the transfer of foreground rights may not be in time, so enqueue a beat on the UI queue.
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            PasteService.PasteInto(target);
            _pasteTargetHwnd = 0;
        });
    }

    /// <summary>
    /// Explicit app exit path (tray "exit" / after a panic wipe). Stops the host, then cleans up the
    /// Win32 resources, allows the window to actually close, and exits the app. It is not called for
    /// the window "close" action.
    /// </summary>
    private async Task ExitAsync()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;

        // Stop the monitoring service (IHostedService).
        if (_host is not null)
        {
            try
            {
                await _host.StopAsync();
            }
            finally
            {
                _host.Dispose();
                _host = null;
            }
        }

        // Zero the resolved DEK (Hello mode; null otherwise). The encryption service already zeroed
        // its own keys when the host was disposed above.
        if (_resolvedKey.Dek is not null)
        {
            CryptographicOperations.ZeroMemory(_resolvedKey.Dek);
        }

        // Clean up the tray icon, hotkeys, and subclass.
        _trayController?.Dispose();
        _trayController = null;

        // Close the settings window if it is open.
        if (_settingsWindow is not null)
        {
            _settingsWindow.Closed -= OnSettingsWindowClosed;
            _settingsWindow.Close();
            _settingsWindow = null;
        }

        // Cancel the "close -> hide" redirection of the popup and actually exit.
        if (Window is MainWindow window)
        {
            window.ViewModel.PasteRequested -= OnPasteRequested;
            window.SettingsRequested -= OnSettingsRequested;
            window.AllowClose();
        }

        Exit();
    }
}
