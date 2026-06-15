using ClipVault.Application.Abstractions;
using ClipVault.Infrastructure.Persistence;
using ClipVault.Infrastructure.Security;
using NSubstitute;

namespace ClipVault.Infrastructure.Tests;

public sealed class KeyVaultsTests : IDisposable
{
    private readonly string _dir;

    public KeyVaultsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ClipVaultVaults_" + Guid.NewGuid().ToString("N"));
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
    public void Ephemeral_returns_a_stable_32_byte_key()
    {
        var vault = new EphemeralKeyVault();

        var first = vault.GetOrCreateMasterKey();
        var second = vault.GetOrCreateMasterKey();

        Assert.Equal(32, first.Length);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Ephemeral_returns_a_defensive_copy()
    {
        var vault = new EphemeralKeyVault();

        var key = vault.GetOrCreateMasterKey();
        key[0] ^= 0xFF; // Mutating the caller's copy must not affect the vault.

        Assert.NotEqual(key, vault.GetOrCreateMasterKey());
    }

    [Fact]
    public void Resolved_returns_the_resolved_dek()
    {
        var resolved = Substitute.For<IResolvedMasterKey>();
        resolved.Dek.Returns(new byte[] { 1, 2, 3, 4 });

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, new ResolvedKeyVault(resolved).GetOrCreateMasterKey());
    }

    [Fact]
    public void Resolved_throws_when_the_dek_is_not_resolved()
    {
        var resolved = Substitute.For<IResolvedMasterKey>();
        resolved.Dek.Returns((byte[]?)null);

        Assert.Throws<InvalidOperationException>(() => new ResolvedKeyVault(resolved).GetOrCreateMasterKey());
    }

    [Fact]
    public void Passphrase_creates_then_unlocks_the_same_key()
    {
        var options = new ClipVaultStorageOptions
        {
            Storage = StorageMode.EncryptedDisk,
            DatabasePath = ":memory:",
            KeyFilePath = Path.Combine(_dir, "dek.bin"),
        };
        var passphrases = Substitute.For<IPassphraseProvider>();
        passphrases.Passphrase.Returns("correct horse");
        var vault = new PassphraseKeyVault(options, passphrases);

        var created = vault.GetOrCreateMasterKey(); // File absent -> CreateNew.
        var unlocked = vault.GetOrCreateMasterKey(); // File now exists -> Unlock.

        Assert.Equal(created, unlocked);
    }
}
