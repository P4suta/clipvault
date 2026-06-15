using System.Security.Cryptography;
using ClipVault.Application.Abstractions;
using ClipVault.Infrastructure.Security;

namespace ClipVault.Infrastructure.Tests;

public sealed class KeyProtectorTests : IDisposable
{
    // Use minimal Argon2 cost to speed up tests (it is saved to the key file and that value is used when decrypting).
    private static readonly Argon2Parameters Fast = new(MemoryKiB: 256, Iterations: 1, Parallelism: 1);

    private readonly string _dir;
    private readonly string _path;

    public KeyProtectorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ClipVaultKey_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "dek.bin");
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
    public void Dpapi_only_create_then_unlock_returns_same_key()
    {
        var protector = new KeyProtector(_path);

        var created = protector.CreateNew(passphrase: null);
        var unlocked = protector.Unlock(passphrase: null);

        Assert.Equal(created, unlocked);
        Assert.False(protector.RequiresPassphrase());
    }

    [Fact]
    public void Passphrase_create_then_unlock_with_correct_passphrase()
    {
        var protector = new KeyProtector(_path, Fast);

        var created = protector.CreateNew("correct horse battery staple");

        Assert.True(protector.RequiresPassphrase());
        Assert.Equal(created, protector.Unlock("correct horse battery staple"));
    }

    [Fact]
    public void Wrong_passphrase_fails()
    {
        var protector = new KeyProtector(_path, Fast);
        protector.CreateNew("right");

        Assert.ThrowsAny<CryptographicException>(() => protector.Unlock("wrong"));
    }

    [Fact]
    public void Missing_passphrase_on_protected_vault_fails()
    {
        var protector = new KeyProtector(_path, Fast);
        protector.CreateNew("pw");

        Assert.ThrowsAny<CryptographicException>(() => protector.Unlock(passphrase: null));
    }

    [Fact]
    public void Change_passphrase_rewraps_same_key()
    {
        var protector = new KeyProtector(_path, Fast);
        var dek = protector.CreateNew("old");

        protector.ChangePassphrase("old", "new");

        Assert.Equal(dek, protector.Unlock("new"));
        Assert.ThrowsAny<CryptographicException>(() => protector.Unlock("old"));
    }

    [Fact]
    public void Can_switch_dpapi_to_passphrase_and_back_preserving_key()
    {
        var protector = new KeyProtector(_path, Fast);
        var dek = protector.CreateNew(passphrase: null);

        protector.ChangePassphrase(currentPassphrase: null, newPassphrase: "secret");
        Assert.True(protector.RequiresPassphrase());
        Assert.Equal(dek, protector.Unlock("secret"));

        protector.ChangePassphrase("secret", newPassphrase: null);
        Assert.False(protector.RequiresPassphrase());
        Assert.Equal(dek, protector.Unlock(passphrase: null));
    }

    [Fact]
    public void Crypto_erase_deletes_key_file()
    {
        var protector = new KeyProtector(_path);
        protector.CreateNew(passphrase: null);
        Assert.True(protector.Exists());

        protector.CryptoErase();

        Assert.False(protector.Exists());
    }

    [Fact]
    public async Task Hello_create_then_unlock_with_same_credential_returns_same_key()
    {
        var hello = new FakeWindowsHello([1, 2, 3]);
        var protector = new KeyProtector(_path);

        var created = await protector.CreateNewWithHelloAsync(hello);

        Assert.True(protector.RequiresHello());
        Assert.Equal(created, await protector.UnlockWithHelloAsync(hello));
    }

    [Fact]
    public async Task Hello_unlock_with_different_credential_fails()
    {
        var protector = new KeyProtector(_path);
        await protector.CreateNewWithHelloAsync(new FakeWindowsHello([1, 2, 3]));

        await Assert.ThrowsAnyAsync<CryptographicException>(
            () => protector.UnlockWithHelloAsync(new FakeWindowsHello([9, 9, 9])));
    }

    // --- Mode reporting across the three protection modes ---
    [Fact]
    public void Dpapi_only_requires_neither_passphrase_nor_hello()
    {
        var protector = new KeyProtector(_path);
        protector.CreateNew(passphrase: null);

        Assert.False(protector.RequiresPassphrase());
        Assert.False(protector.RequiresHello());
    }

    [Fact]
    public void Passphrase_vault_requires_passphrase_not_hello()
    {
        var protector = new KeyProtector(_path, Fast);
        protector.CreateNew("pw");

        Assert.True(protector.RequiresPassphrase());
        Assert.False(protector.RequiresHello());
    }

    [Fact]
    public async Task Hello_vault_requires_hello_not_passphrase()
    {
        var protector = new KeyProtector(_path);
        await protector.CreateNewWithHelloAsync(new FakeWindowsHello([1, 2, 3]));

        Assert.True(protector.RequiresHello());
        Assert.False(protector.RequiresPassphrase());
    }

    // --- Cross-mode unlock attempts ---
    [Fact]
    public void Dpapi_only_unlock_ignores_supplied_passphrase()
    {
        var protector = new KeyProtector(_path);
        var created = protector.CreateNew(passphrase: null);

        Assert.Equal(created, protector.Unlock("this is ignored on a dpapi-only vault"));
    }

    [Fact]
    public async Task Unlock_with_passphrase_on_hello_vault_fails()
    {
        var protector = new KeyProtector(_path);
        await protector.CreateNewWithHelloAsync(new FakeWindowsHello([1, 2, 3]));

        Assert.ThrowsAny<CryptographicException>(() => protector.Unlock("pw"));
    }

    [Fact]
    public async Task Unlock_with_hello_on_passphrase_vault_fails()
    {
        var protector = new KeyProtector(_path, Fast);
        protector.CreateNew("pw");

        await Assert.ThrowsAnyAsync<CryptographicException>(
            () => protector.UnlockWithHelloAsync(new FakeWindowsHello([1, 2, 3])));
    }

    // --- File integrity / format tampering ---
    [Fact]
    public void Corrupt_magic_makes_unlock_fail()
    {
        var protector = new KeyProtector(_path);
        protector.CreateNew(passphrase: null);
        var bytes = File.ReadAllBytes(_path);
        bytes[0] ^= 0xFF; // Break the "CVK1" magic.
        File.WriteAllBytes(_path, bytes);

        Assert.ThrowsAny<CryptographicException>(() => protector.Unlock(passphrase: null));
    }

    [Fact]
    public void Unknown_mode_byte_makes_unlock_fail()
    {
        var protector = new KeyProtector(_path);
        protector.CreateNew(passphrase: null);
        var bytes = File.ReadAllBytes(_path);
        bytes[5] = 0x09; // Mode byte lives at offset Magic.Length(4) + 1; it is outside the DPAPI-protected body.
        File.WriteAllBytes(_path, bytes);

        Assert.ThrowsAny<CryptographicException>(() => protector.Unlock(passphrase: null));
    }

    [Fact]
    public void Truncated_key_file_makes_read_mode_fail()
    {
        File.WriteAllBytes(_path, [0x43, 0x56, 0x4B]); // 3 bytes: shorter than the 6-byte header.
        var protector = new KeyProtector(_path);

        Assert.Throws<EndOfStreamException>(() => protector.RequiresPassphrase());
    }

    [Fact]
    public void Truncated_key_file_makes_unlock_fail()
    {
        File.WriteAllBytes(_path, [0x43, 0x56, 0x4B]); // 3 bytes.
        var protector = new KeyProtector(_path);

        Assert.ThrowsAny<CryptographicException>(() => protector.Unlock(passphrase: null));
    }

    // --- Change passphrase atomicity ---
    [Fact]
    public void Change_passphrase_with_wrong_current_leaves_vault_intact()
    {
        var protector = new KeyProtector(_path, Fast);
        var dek = protector.CreateNew("right");

        Assert.ThrowsAny<CryptographicException>(() => protector.ChangePassphrase("wrong", "new"));

        // The rewrap never happened: the old passphrase still unlocks and "new" is not accepted.
        Assert.Equal(dek, protector.Unlock("right"));
        Assert.ThrowsAny<CryptographicException>(() => protector.Unlock("new"));
    }

    // --- Crypto-erase ---
    [Fact]
    public void Crypto_erase_is_idempotent()
    {
        var protector = new KeyProtector(_path);
        protector.CryptoErase(); // No file yet -> no throw.
        protector.CreateNew(passphrase: null);
        protector.CryptoErase();
        protector.CryptoErase(); // Already gone -> no throw.

        Assert.False(protector.Exists());
    }

    [Fact]
    public void Unlock_after_crypto_erase_fails()
    {
        var protector = new KeyProtector(_path);
        protector.CreateNew(passphrase: null);
        protector.CryptoErase();

        Assert.ThrowsAny<IOException>(() => protector.Unlock(passphrase: null));
    }

    // --- Argon parameters are read from the file, not from the unlocking instance ---
    [Fact]
    public void Unlock_uses_argon_parameters_stored_in_file()
    {
        var writer = new KeyProtector(_path, Fast);
        var dek = writer.CreateNew("pw");

        // A fresh protector configured with the (slow) secure defaults must still unlock
        // using the Fast parameters persisted in the file; otherwise the derived key would differ.
        var reader = new KeyProtector(_path);

        Assert.Equal(dek, reader.Unlock("pw"));
    }
}
