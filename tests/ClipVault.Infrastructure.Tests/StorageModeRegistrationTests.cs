using ClipVault.Application.Abstractions;
using ClipVault.Domain.Abstractions;
using ClipVault.Infrastructure.DependencyInjection;
using ClipVault.Infrastructure.Persistence;
using ClipVault.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace ClipVault.Infrastructure.Tests;

/// <summary>
/// Verifies that the correct implementations are registered depending on the storage mode and the key file's
/// protection state. In volatile mode, no disk-related implementations (the DPAPI key vault and SQLite) are
/// registered at all, which structurally guarantees that the disk is never touched.
/// </summary>
public sealed class StorageModeRegistrationTests : IDisposable
{
    private readonly string _dir;

    public StorageModeRegistrationTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ClipVaultReg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // Best effort.
        }
    }

    [Fact]
    public void Volatile_mode_uses_in_memory_repository_and_ephemeral_key_no_disk_services()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(Options(StorageMode.VolatileMemory));

        Assert.Equal(typeof(InMemoryClipboardHistoryRepository), ImplementationOf<IClipboardHistoryRepository>(services));
        Assert.Equal(typeof(EphemeralKeyVault), ImplementationOf<IKeyVault>(services));

        // Ensure no disk-only implementations have crept in.
        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(SqliteClipboardHistoryRepository));
        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(DpapiKeyVault));
    }

    [Fact]
    public void Encrypted_disk_mode_without_key_file_uses_sqlite_and_dpapi()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(DiskOptions(Path.Combine(_dir, "missing.bin")));

        Assert.Equal(typeof(SqliteClipboardHistoryRepository), ImplementationOf<IClipboardHistoryRepository>(services));
        Assert.Equal(typeof(DpapiKeyVault), ImplementationOf<IKeyVault>(services));
    }

    [Fact]
    public void Encrypted_disk_mode_with_passphrase_key_uses_passphrase_vault()
    {
        var keyPath = Path.Combine(_dir, "dek.bin");
        new KeyProtector(keyPath, new Argon2Parameters(MemoryKiB: 256, Iterations: 1, Parallelism: 1)).CreateNew("pw");

        var services = new ServiceCollection();
        services.AddInfrastructure(DiskOptions(keyPath));

        Assert.Equal(typeof(PassphraseKeyVault), ImplementationOf<IKeyVault>(services));
    }

    [Fact]
    public async Task Encrypted_disk_mode_with_hello_key_uses_resolved_vault()
    {
        var keyPath = Path.Combine(_dir, "dek.bin");
        await new KeyProtector(keyPath).CreateNewWithHelloAsync(new FakeWindowsHello([1, 2, 3]));

        var services = new ServiceCollection();
        services.AddInfrastructure(DiskOptions(keyPath));

        Assert.Equal(typeof(ResolvedKeyVault), ImplementationOf<IKeyVault>(services));
    }

    private static Type ImplementationOf<TService>(IServiceCollection services) =>
        services.Single(d => d.ServiceType == typeof(TService)).ImplementationType!;

    private static ClipVaultStorageOptions Options(StorageMode mode) => new()
    {
        Storage = mode,
        DatabasePath = ":memory:",
        KeyFilePath = "unused",
    };

    private static ClipVaultStorageOptions DiskOptions(string keyPath) => new()
    {
        Storage = StorageMode.EncryptedDisk,
        DatabasePath = ":memory:",
        KeyFilePath = keyPath,
    };
}
