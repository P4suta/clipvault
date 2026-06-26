using System;
using ClipVault.Application.Abstractions;
using ClipVault.Application.DependencyInjection;
using ClipVault.Domain.Policies;
using ClipVault.Infrastructure.DependencyInjection;
using ClipVault.Infrastructure.Persistence;
using ClipVault.Infrastructure.Security;
using ClipVault.Infrastructure.Settings;
using ClipVaultApp.Localization;
using ClipVaultApp.Services;
using ClipVaultApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClipVaultApp;

/// <summary>
/// Extension methods that group the DI registrations for the presentation layer and the host as a whole.
/// </summary>
internal static class HostBuilderExtensions
{
    // RAM budget for the volatile (in-memory) ring. Volatile mode keeps content in RAM, so its bound is memory,
    // not disk; persistent mode uses the larger on-disk quota (ClipVaultSettings.MaxHistoryBytes) instead.
    private const long VolatileMemoryBudgetBytes = 256L * 1024 * 1024;

    /// <summary>
    /// Registers the services for each layer (presentation, application, and infrastructure) with the host.
    /// Uses the loaded settings to swap in the storage backend, retention cap, settings service, and passphrase provider.
    /// </summary>
    /// <param name="builder">The host application builder to configure.</param>
    /// <param name="dispatcherQueue">The UI thread dispatcher queue used to build the UI dispatcher.</param>
    /// <param name="settings">The loaded settings service that drives the storage and retention configuration.</param>
    /// <param name="validatedPassphrase">The passphrase validated at the startup gate, or null when none is used.</param>
    /// <param name="resolvedKey">The holder for the DEK resolved at the startup gate, shared with the key vault.</param>
    /// <param name="themeService">The theme service to register so view models can switch the theme live.</param>
    /// <returns>The same <see cref="HostApplicationBuilder"/> instance so calls can be chained.</returns>
    public static HostApplicationBuilder ConfigureClipVault(
        this HostApplicationBuilder builder,
        Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue,
        JsonSettingsService settings,
        string? validatedPassphrase,
        IResolvedMasterKey resolvedKey,
        IThemeService themeService)
    {
        // Privacy / no footprint: route logs only to the debugger output (OutputDebugString) — never the
        // console, Event Log, or a file. Clear the providers Host.CreateApplicationBuilder adds by default.
        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();

        builder.Services.AddPresentation(dispatcherQueue);
        builder.Services.AddApplication();

        // Storage backend follows the loaded settings (default disk). In Hello mode AddInfrastructure
        // selects ResolvedKeyVault, fed by the IResolvedMasterKey holder supplied below.
        builder.Services.AddInfrastructure(
            ClipVaultStorageOptions.Default() with { Storage = settings.Current.Storage });

        // Supplier of the validated passphrase (consumed by the key vault in disk mode with passphrase protection).
        builder.Services.AddSingleton<IPassphraseProvider>(new PassphraseProvider(validatedPassphrase));

        // DEK resolved at the startup gate (consumed by ResolvedKeyVault in Hello disk mode); same instance the gate populated.
        builder.Services.AddSingleton(resolvedKey);

        // Theme service (same instance that themed the startup windows), shared with the settings view model.
        builder.Services.AddSingleton<IThemeService>(themeService);

        // Replace the default in-memory settings service with the persisted JSON implementation (the already-loaded instance).
        builder.Services.Replace(ServiceDescriptor.Singleton<ISettingsService>(settings));

        // Build the retention policy from the configured values and swap it in. The byte budget differs by mode:
        // volatile mode is a bounded RAM ring, persistent mode is bounded by the on-disk quota. A non-positive
        // entry cap means "no count limit", leaving the byte quota and age as the bounds.
        var maxBytes = settings.Current.Storage == StorageMode.VolatileMemory
            ? VolatileMemoryBudgetBytes
            : settings.Current.MaxHistoryBytes;
        builder.Services.Replace(ServiceDescriptor.Singleton(new RetentionSettings
        {
            MaxAge = TimeSpan.FromDays(settings.Current.MaxAgeDays),
            MaxEntries = settings.Current.MaxEntries <= 0 ? int.MaxValue : settings.Current.MaxEntries,
            MaxTotalBytes = maxBytes,
        }));

        return builder;
    }

    /// <summary>
    /// Registers the presentation layer (UI dispatcher, view models, and windows).
    /// </summary>
    /// <param name="services">The service collection to add the presentation registrations to.</param>
    /// <param name="dispatcherQueue">The UI thread dispatcher queue used to build the UI dispatcher.</param>
    private static void AddPresentation(
        this IServiceCollection services,
        Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue)
    {
        // Singleton over the UI thread's DispatcherQueue; consumed by clipboard monitoring/writing in Infrastructure.
        services.AddSingleton<IUiDispatcher>(new UiDispatcher(dispatcherQueue));

        // Share the localization service (already created at startup) with the view models.
        services.AddSingleton<ILocalizationService>(_ => App.Localization);

        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<MainWindow>();

        // Settings are created on demand (each new instance loads the backend's current values).
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsWindow>();
    }
}
