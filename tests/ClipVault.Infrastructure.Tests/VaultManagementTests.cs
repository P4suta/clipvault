using System.Security.Cryptography;
using ClipVault.Application.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;
using ClipVault.Infrastructure.Persistence;
using ClipVault.Infrastructure.Security;

namespace ClipVault.Infrastructure.Tests;

public sealed class VaultManagementTests : IDisposable
{
    private readonly string _dir;

    public VaultManagementTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ClipVaultVault_" + Guid.NewGuid().ToString("N"));
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
    public async Task Panic_wipe_clears_history_and_destroys_key()
    {
        var keyPath = Path.Combine(_dir, "dek.bin");
        var options = new ClipVaultStorageOptions
        {
            Storage = StorageMode.EncryptedDisk,
            DatabasePath = ":memory:",
            KeyFilePath = keyPath,
        };
        new KeyProtector(keyPath).CreateNew(passphrase: null);

        var repository = new InMemoryClipboardHistoryRepository();
        await repository.AddAsync(
            ClipboardEntry.Create(
                ClipContentType.Text,
                new ContentHash("h"),
                "p",
                image: null,
                sizeInBytes: 1,
                SourceApplication.Unknown,
                DateTimeOffset.UnixEpoch),
            new ClipContent(ClipContentType.Text, [1]));

        var vault = new VaultManagement(options, repository, new FakeWindowsHello([7, 7, 7]));
        Assert.Equal(VaultProtection.DpapiOnly, vault.Protection);

        await vault.PanicWipeAsync();

        Assert.Equal(0, await repository.CountAsync());
        Assert.False(File.Exists(keyPath));
    }

    [Fact]
    public void Volatile_mode_reports_volatile_protection()
    {
        var options = new ClipVaultStorageOptions
        {
            Storage = StorageMode.VolatileMemory,
            DatabasePath = ":memory:",
            KeyFilePath = "unused",
        };

        var vault = new VaultManagement(options, new InMemoryClipboardHistoryRepository(), new FakeWindowsHello([7, 7, 7]));

        Assert.Equal(VaultProtection.Volatile, vault.Protection);
    }

    [Fact]
    public async Task Volatile_mode_rejects_set_passphrase()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => VolatileVault().SetOrChangePassphraseAsync(null, "pw"));
    }

    [Fact]
    public async Task Volatile_mode_rejects_remove_passphrase() => await Assert.ThrowsAsync<InvalidOperationException>(() => VolatileVault().RemovePassphraseAsync("pw"));

    [Fact]
    public async Task Volatile_mode_rejects_enable_hello() => await Assert.ThrowsAsync<InvalidOperationException>(() => VolatileVault().EnableHelloAsync());

    [Fact]
    public async Task Volatile_mode_rejects_disable_hello() => await Assert.ThrowsAsync<InvalidOperationException>(() => VolatileVault().DisableHelloAsync());

    [Fact]
    public async Task Set_passphrase_rejects_an_empty_new_passphrase()
    {
        var keyPath = Path.Combine(_dir, "dek.bin");
        new KeyProtector(keyPath).CreateNew(passphrase: null);

        await Assert.ThrowsAsync<ArgumentException>(() => DiskVault(keyPath).SetOrChangePassphraseAsync(null, string.Empty));
    }

    [Fact]
    public async Task Remove_passphrase_with_wrong_current_fails()
    {
        var keyPath = Path.Combine(_dir, "dek.bin");
        new KeyProtector(keyPath, new Argon2Parameters(MemoryKiB: 256, Iterations: 1, Parallelism: 1)).CreateNew("right");

        await Assert.ThrowsAnyAsync<CryptographicException>(() => DiskVault(keyPath).RemovePassphraseAsync("wrong"));
    }

    [Fact]
    public async Task Panic_wipe_in_volatile_mode_clears_history_without_touching_a_key_file()
    {
        var options = new ClipVaultStorageOptions
        {
            Storage = StorageMode.VolatileMemory,
            DatabasePath = ":memory:",
            KeyFilePath = Path.Combine(_dir, "does", "not", "exist.bin"),
        };
        var repo = new InMemoryClipboardHistoryRepository();
        await repo.AddAsync(
            ClipboardEntry.Create(
                ClipContentType.Text, new ContentHash("h"), "p", image: null, sizeInBytes: 1, SourceApplication.Unknown, DateTimeOffset.UnixEpoch),
            new ClipContent(ClipContentType.Text, [1]));
        var vault = new VaultManagement(options, repo, new FakeWindowsHello([7, 7, 7]));

        await vault.PanicWipeAsync();

        Assert.Equal(0, await repo.CountAsync());
    }

    private static VaultManagement VolatileVault() =>
        new(
            new ClipVaultStorageOptions { Storage = StorageMode.VolatileMemory, DatabasePath = ":memory:", KeyFilePath = "unused" },
            new InMemoryClipboardHistoryRepository(),
            new FakeWindowsHello([7, 7, 7]));

    private static VaultManagement DiskVault(string keyPath) =>
        new(
            new ClipVaultStorageOptions { Storage = StorageMode.EncryptedDisk, DatabasePath = ":memory:", KeyFilePath = keyPath },
            new InMemoryClipboardHistoryRepository(),
            new FakeWindowsHello([7, 7, 7]));
}
