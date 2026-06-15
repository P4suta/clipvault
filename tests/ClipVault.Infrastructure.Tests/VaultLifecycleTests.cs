using ClipVault.Application.Abstractions;
using ClipVault.Infrastructure.Persistence;
using ClipVault.Infrastructure.Security;

namespace ClipVault.Infrastructure.Tests;

/// <summary>
/// Exercises the full vault protection lifecycle through <see cref="VaultManagement"/>: the data-encryption key
/// must survive every transition (DPAPI -> passphrase -> Hello -> DPAPI) because the DEK is immutable and only
/// the wrapping changes.
/// </summary>
public sealed class VaultLifecycleTests : IDisposable
{
    private readonly string _dir;
    private readonly string _keyPath;
    private readonly ClipVaultStorageOptions _options;

    public VaultLifecycleTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ClipVaultLifecycle_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _keyPath = Path.Combine(_dir, "dek.bin");
        _options = new ClipVaultStorageOptions
        {
            Storage = StorageMode.EncryptedDisk,
            DatabasePath = ":memory:",
            KeyFilePath = _keyPath,
        };
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
    public async Task Dek_is_preserved_across_protection_transitions()
    {
        var hello = new FakeWindowsHello([4, 2]);
        var dek = new KeyProtector(_keyPath).CreateNew(passphrase: null);
        var vault = new VaultManagement(_options, new InMemoryClipboardHistoryRepository(), hello);
        Assert.Equal(VaultProtection.DpapiOnly, vault.Protection);

        // DPAPI -> passphrase.
        await vault.SetOrChangePassphraseAsync(currentPassphrase: null, "correct horse");
        Assert.Equal(VaultProtection.Passphrase, vault.Protection);
        Assert.Equal(dek, new KeyProtector(_keyPath).Unlock("correct horse"));

        // passphrase -> Hello.
        await vault.EnableHelloAsync("correct horse");
        Assert.Equal(VaultProtection.Hello, vault.Protection);
        Assert.Equal(dek, await new KeyProtector(_keyPath).UnlockWithHelloAsync(hello));

        // Hello -> DPAPI only.
        await vault.DisableHelloAsync();
        Assert.Equal(VaultProtection.DpapiOnly, vault.Protection);
        Assert.Equal(dek, new KeyProtector(_keyPath).Unlock(passphrase: null));
    }
}
