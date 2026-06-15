using ClipVault.Application.Abstractions;
using ClipVault.Domain.Abstractions;
using ClipVault.Infrastructure.Clipboard;
using ClipVault.Infrastructure.Hosting;
using ClipVault.Infrastructure.Persistence;
using ClipVault.Infrastructure.Security;
using ClipVault.Infrastructure.Startup;
using ClipVault.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;

namespace ClipVault.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering the Infrastructure services in a dependency injection container.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Infrastructure services, such as encryption, persistence, and clipboard monitoring.
    /// </summary>
    /// <param name="services">The service collection to add the registrations to.</param>
    /// <param name="options">The storage options to use, or <see langword="null"/> to use the defaults.</param>
    /// <returns>The same service collection so that calls can be chained.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, ClipVaultStorageOptions? options = null)
    {
        var storageOptions = options ?? ClipVaultStorageOptions.Default();
        services.AddSingleton(storageOptions);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IEncryptionService, ChaCha20Poly1305EncryptionService>();

        if (storageOptions.Storage == StorageMode.VolatileMemory)
        {
            // Fully volatile: never touch the disk. Both the key and the history stay in RAM only (no dek.bin and no SQLite).
            services.AddSingleton<IKeyVault, EphemeralKeyVault>();
            services.AddSingleton<IClipboardHistoryRepository, InMemoryClipboardHistoryRepository>();
        }
        else
        {
            // Encrypted disk persistence: a DPAPI-sealed key (two-factor when a passphrase or Hello is set) plus SQLite.
            var keyFile = new KeyProtector(storageOptions.KeyFilePath);
            if (keyFile.Exists() && keyFile.RequiresHello())
            {
                // Because Hello is interactive and asynchronous, supply the DEK unlocked at startup via IResolvedMasterKey.
                services.AddSingleton<IKeyVault, ResolvedKeyVault>();
            }
            else if (keyFile.Exists() && keyFile.RequiresPassphrase())
            {
                services.AddSingleton<IKeyVault, PassphraseKeyVault>();
            }
            else
            {
                services.AddSingleton<IKeyVault, DpapiKeyVault>();
            }

            services.AddSingleton<IClipboardHistoryRepository, SqliteClipboardHistoryRepository>();
        }

        // Clipboard monitoring and reading/writing (WinRT / Win32).
        // SourceAppResolver and WinRtClipboardReader are stateless pure helpers, so they are called
        // statically rather than registered in DI.
        services.AddSingleton<IClipboardMonitor, WinRtClipboardMonitor>();
        services.AddSingleton<IClipboardWriter, WinRtClipboardWriter>();
        services.AddHostedService<ClipboardMonitorHostedService>();

        // Periodic cleanup for the retention limits.
        services.AddHostedService<RetentionHostedService>();

        // Vault operations (passphrase / Hello / crypto-erase), Windows Hello, and run-at-startup.
        services.AddSingleton<IWindowsHello, WindowsHelloService>();
        services.AddSingleton<IVaultManagement, VaultManagement>();
        services.AddSingleton<IStartupService, RegistryStartupService>();

        return services;
    }
}
