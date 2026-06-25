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

namespace ClipVaultApp;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application, IDisposable
{
    // DEK resolved at the startup gate; populated only in Windows Hello disk mode (otherwise null). Shared with DI via ResolvedKeyVault.
    private readonly ResolvedMasterKey _resolvedKey = new();

    private IHost? _host;

    // Tray icon, hotkeys, and context menu; created once the window HWND is known.
    private TrayHotkeyController? _trayController;

    // Paste-back target window captured on summon (hotkey / tray left-click).
    private nint _pasteTargetHwnd;

    private bool _isExiting;

    // Created on demand; reactivated thereafter.
    private SettingsWindow? _settingsWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    public App()
    {
        InitializeComponent();

        // Custom High Contrast brushes already pair correctly, so suppress the platform's extra adjustment.
        HighContrastAdjustment = ApplicationHighContrastAdjustment.None;

        // Last-chance exception sinks; log the type name only (never plaintext).
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>
    /// Gets the main application window. Use <c>App.Window</c> from any class that needs
    /// the window reference (for dialogs, pickers, interop, etc.).
    /// </summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>
    /// Gets the UI thread dispatcher. Fully qualified to avoid CS0104 ambiguity with
    /// <see cref="Windows.System.DispatcherQueue"/>.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>
    /// Gets the application-wide DI container, used to resolve windows and ViewModels.
    /// Available after the host is built in <see cref="OnLaunched"/>.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Gets the UI localization service. Set at the start of <see cref="StartupAsync"/>,
    /// before any window is created, so XAML (<c>{loc:Str}</c>) and code-behind can resolve strings.
    /// </summary>
    public static ILocalizationService Localization { get; private set; } = null!;

    /// <summary>
    /// Gets the entry point for stopping the host and exiting safely (e.g. from the settings
    /// window). Backed by <see cref="ExitAsync"/>, assigned in <see cref="OnLaunched"/>.
    /// </summary>
    public static Func<Task> RequestExitAsync { get; private set; } = () => Task.CompletedTask;

    /// <summary>Gets the entry point that opens (or reactivates) the settings window.</summary>
    public static Action OpenSettings { get; private set; } = () => { };

    /// <summary>
    /// Gets the native window handle (HWND) for file pickers, <c>DataTransferManager</c>,
    /// and WinRT interop that requires <c>InitializeWithWindow</c>.
    /// </summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>
    /// Releases the disposable fields (host and tray controller). Idempotent because
    /// <see cref="ExitAsync"/> releases them first; WinUI does not dispose the Application.
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
            // Type + message only (never the full exception).
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

    private static void OnUnhandledException(object sender, System.UnhandledExceptionEventArgs e) =>
        Debug.WriteLine($"[ClipVault] Fatal: {(e.ExceptionObject as Exception)?.GetType().Name ?? "unknown"}");

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
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
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        // Falls back to defaults if settings are corrupt.
        var settings = JsonSettingsService.Load(JsonSettingsService.DefaultPath());

        // Apply the UI language before any window (including the unlock gate) is created.
        ApplyUiLanguage(settings.Current.Language);

        // Theme service is created before the host so the startup unlock windows are themed too.
        var themeService = new ThemeService();
        themeService.Initialize(settings.Current.Theme);

        // Resolve the disk-encryption unlock gate before building the host.
        var unlock = await TryUnlockAtStartupAsync(settings.Current);

        // User abandoned unlock: ExitAsync cleans up null-safely even pre-host.
        if (unlock is UnlockResult.Cancelled)
        {
            await ExitAsync();
            return;
        }

        var validatedPassphrase = (unlock as UnlockResult.Unlocked)?.Passphrase;

        // In Hello mode _resolvedKey.Dek is already populated before StartAsync (ResolvedKeyVault reads it lazily).
        _host = Host.CreateApplicationBuilder()
            .ConfigureClipVault(DispatcherQueue, settings, validatedPassphrase, _resolvedKey, themeService)
            .Build();
        Services = _host.Services;

        // Entry points used from outside (e.g. the settings window).
        RequestExitAsync = ExitAsync;
        OpenSettings = ShowSettingsWindow;

        var window = Services.GetRequiredService<MainWindow>();
        Window = window;

        // Track the main window for live theme switching (also applies the current theme now).
        themeService.Register(window);

        // Paste-back after the VM writes to the clipboard.
        window.ViewModel.PasteRequested += OnPasteRequested;
        window.SettingsRequested += OnSettingsRequested;

        Window.Activate();

        // HWND is known now: install tray, hotkeys, and subclass.
        InitializeTray(window);

        // StartAsync (not RunAsync): WinUI owns the message loop.
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
            return new UnlockResult.Unlocked(null);
        }

        var keyProtector = new KeyProtector(ClipVaultStorageOptions.Default().KeyFilePath);

        if (keyProtector.Exists() && keyProtector.RequiresHello())
        {
            // Hello: decrypt the DEK via the OS prompt and store it into the holder (retryable / exit).
            var helloPrompt = new HelloUnlockWindow(keyProtector, new WindowsHelloService());
            ThemeService.ApplyTo(helloPrompt, settings.Theme);
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
            // Passphrase: validate in a dedicated window, then hand the string to the provider.
            var prompt = new PassphrasePromptWindow(keyProtector);
            ThemeService.ApplyTo(prompt, settings.Theme);
            prompt.Activate();

            var entered = await prompt.ResultTask;
            return entered is null
                ? new UnlockResult.Cancelled()
                : new UnlockResult.Unlocked(entered);
        }

        // First run (key not yet created) or DPAPI only: no prompt needed.
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
        // Main window is an always-on-top popup; hide it so settings is not occluded.
        if (Window is MainWindow main && main.IsWindowVisible)
        {
            main.HideToTray();
        }

        if (_settingsWindow is null)
        {
            _settingsWindow = Services.GetRequiredService<SettingsWindow>();
            _settingsWindow.Closed += OnSettingsWindowClosed;
            Services.GetRequiredService<IThemeService>().Register(_settingsWindow);
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

        // Foreground transfer may lag the hide; defer a beat on the UI queue.
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            PasteService.PasteInto(target);
            _pasteTargetHwnd = 0;
        });
    }

    /// <summary>
    /// Explicit app exit path (tray "exit" / after a panic wipe). Stops the host, cleans up the
    /// Win32 resources, allows the window to actually close, and exits. Not called for the
    /// window "close" action.
    /// </summary>
    private async Task ExitAsync()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;

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

        // Zero the resolved DEK (Hello mode; null otherwise). The encryption service zeroed its own keys on host dispose.
        if (_resolvedKey.Dek is not null)
        {
            CryptographicOperations.ZeroMemory(_resolvedKey.Dek);
        }

        _trayController?.Dispose();
        _trayController = null;

        if (_settingsWindow is not null)
        {
            _settingsWindow.Closed -= OnSettingsWindowClosed;
            _settingsWindow.Close();
            _settingsWindow = null;
        }

        // Cancel the popup's close→hide redirection, then exit.
        if (Window is MainWindow window)
        {
            window.ViewModel.PasteRequested -= OnPasteRequested;
            window.SettingsRequested -= OnSettingsRequested;
            window.AllowClose();
        }

        Exit();
    }
}
