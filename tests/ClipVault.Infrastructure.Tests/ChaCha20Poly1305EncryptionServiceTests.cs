using System.Security.Cryptography;
using System.Text;
using ClipVault.Infrastructure.Security;

namespace ClipVault.Infrastructure.Tests;

public class ChaCha20Poly1305EncryptionServiceTests
{
    // Ciphertext layout: [version(1)=2 | nonce(12) | tag(16) | ciphertext]. Header is 29 bytes.
    private const int VersionOffset = 0;
    private const int NonceOffset = 1;
    private const int TagOffset = 1 + 12;
    private const int HeaderSize = 1 + 12 + 16;

    [Fact]
    public void Encrypt_then_Decrypt_round_trips()
    {
        using var service = NewService();
        var plaintext = Encoding.UTF8.GetBytes("秘密のテキスト 🔐");

        var ciphertext = service.Encrypt(plaintext);

        Assert.NotEqual(plaintext, ciphertext);
        Assert.Equal(plaintext, service.Decrypt(ciphertext));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(4096)]
    [InlineData(1_000_000)]
    public void Encrypt_then_Decrypt_round_trips_for_various_sizes(int size)
    {
        using var service = NewService();
        var plaintext = RandomBytes(size, seed: 1000 + size);

        Assert.Equal(plaintext, service.Decrypt(service.Encrypt(plaintext)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(4096)]
    public void Encrypt_then_Decrypt_round_trips_with_associated_data(int size)
    {
        using var service = NewService();
        var plaintext = RandomBytes(size, seed: 2000 + size);
        var aad = "entry:payload"u8.ToArray();

        Assert.Equal(plaintext, service.Decrypt(service.Encrypt(plaintext, aad), aad));
    }

    [Fact]
    public void Encrypt_empty_plaintext_produces_header_only_ciphertext()
    {
        using var service = NewService();

        var ciphertext = service.Encrypt([]);

        Assert.Equal(HeaderSize, ciphertext.Length);
        Assert.Empty(service.Decrypt(ciphertext));
    }

    [Fact]
    public void Encrypt_uses_a_fresh_nonce_each_call()
    {
        using var service = NewService();
        var plaintext = new byte[] { 1, 2, 3 };

        Assert.NotEqual(service.Encrypt(plaintext), service.Encrypt(plaintext));
    }

    [Fact]
    public void Encrypt_writes_a_distinct_nonce_region_each_call()
    {
        using var service = NewService();
        var plaintext = new byte[] { 1, 2, 3 };

        var first = service.Encrypt(plaintext).AsSpan(NonceOffset, 12).ToArray();
        var second = service.Encrypt(plaintext).AsSpan(NonceOffset, 12).ToArray();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Tampered_ciphertext_fails_authentication()
    {
        using var service = NewService();
        var ciphertext = service.Encrypt(new byte[] { 1, 2, 3 });
        ciphertext[^1] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() => service.Decrypt(ciphertext));
    }

    [Fact]
    public void Tampering_with_nonce_fails_authentication()
    {
        using var service = NewService();
        var ciphertext = service.Encrypt(new byte[] { 1, 2, 3 });
        ciphertext[NonceOffset] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() => service.Decrypt(ciphertext));
    }

    [Fact]
    public void Tampering_with_tag_fails_authentication()
    {
        using var service = NewService();
        var ciphertext = service.Encrypt(new byte[] { 1, 2, 3 });
        ciphertext[TagOffset] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() => service.Decrypt(ciphertext));
    }

    [Fact]
    public void Tampering_with_ciphertext_body_fails_authentication()
    {
        using var service = NewService();
        var ciphertext = service.Encrypt(new byte[] { 1, 2, 3 });
        ciphertext[HeaderSize] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() => service.Decrypt(ciphertext));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Decrypt_with_wrong_version_byte_throws_format_exception(int version)
    {
        using var service = NewService();
        var ciphertext = service.Encrypt(new byte[] { 1, 2, 3 });
        ciphertext[VersionOffset] = (byte)version;

        var ex = Assert.Throws<CryptographicException>(() => service.Decrypt(ciphertext));
        Assert.IsNotType<AuthenticationTagMismatchException>(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(28)]
    public void Decrypt_with_too_short_ciphertext_throws_format_exception(int length)
    {
        using var service = NewService();

        Assert.Throws<CryptographicException>(() => service.Decrypt(new byte[length]));
    }

    [Fact]
    public void Decrypt_with_different_key_fails_authentication()
    {
        using var encryptor = NewService(seed: 1);
        using var decryptor = NewService(seed: 2);
        var ciphertext = encryptor.Encrypt(new byte[] { 1, 2, 3 });

        Assert.Throws<AuthenticationTagMismatchException>(() => decryptor.Decrypt(ciphertext));
    }

    [Fact]
    public void Encrypt_with_matching_associated_data_round_trips()
    {
        using var service = NewService();
        var plaintext = new byte[] { 9, 8, 7 };

        var ciphertext = service.Encrypt(plaintext, "entry:payload"u8);

        Assert.Equal(plaintext, service.Decrypt(ciphertext, "entry:payload"u8));
    }

    [Fact]
    public void Decrypt_with_wrong_associated_data_fails()
    {
        using var service = NewService();
        var ciphertext = service.Encrypt(new byte[] { 1, 2, 3 }, "field:a"u8);

        Assert.Throws<AuthenticationTagMismatchException>(() => service.Decrypt(ciphertext, "field:b"u8));
    }

    [Fact]
    public void Decrypt_without_associated_data_when_encrypted_with_it_fails()
    {
        using var service = NewService();
        var ciphertext = service.Encrypt(new byte[] { 1, 2, 3 }, "field:a"u8);

        Assert.Throws<AuthenticationTagMismatchException>(() => service.Decrypt(ciphertext));
    }

    [Fact]
    public void Decrypt_with_associated_data_when_encrypted_without_it_fails()
    {
        using var service = NewService();
        var ciphertext = service.Encrypt(new byte[] { 1, 2, 3 });

        Assert.Throws<AuthenticationTagMismatchException>(() => service.Decrypt(ciphertext, "field:a"u8));
    }

    [Fact]
    public void KeyedHash_is_deterministic_and_distinct_per_input()
    {
        using var service = NewService();

        Assert.Equal(service.KeyedHash("abc"u8), service.KeyedHash("abc"u8));
        Assert.NotEqual(service.KeyedHash("abc"u8), service.KeyedHash("abd"u8));
    }

    [Fact]
    public void KeyedHash_differs_for_different_keys()
    {
        using var first = NewService(seed: 1);
        using var second = NewService(seed: 2);

        Assert.NotEqual(first.KeyedHash("abc"u8), second.KeyedHash("abc"u8));
    }

    [Fact]
    public void Encrypt_after_dispose_throws()
    {
        var service = NewService();
        service.Dispose();

        Assert.Throws<ObjectDisposedException>(() => service.Encrypt(new byte[] { 1 }));
    }

    [Theory]
    [MemberData(nameof(PayloadSeeds))]
    public void Round_trips_random_payloads_with_random_associated_data(int seed)
    {
        var rng = new Random(20240615 + seed);
        var data = new byte[rng.Next(0, 8193)];
        rng.NextBytes(data);
        var aad = new byte[rng.Next(0, 33)];
        rng.NextBytes(aad);
        using var service = NewService();

        Assert.Equal(data, service.Decrypt(service.Encrypt(data, aad), aad));
    }

    public static IEnumerable<object[]> PayloadSeeds()
    {
        for (var i = 0; i < 40; i++)
        {
            yield return [i];
        }
    }

    private static ChaCha20Poly1305EncryptionService NewService(byte seed = 0)
    {
        var key = new byte[32];
        for (var i = 0; i < key.Length; i++)
        {
            key[i] = (byte)(i + seed);
        }

        return new ChaCha20Poly1305EncryptionService(new FixedKeyVault(key));
    }

    private static byte[] RandomBytes(int size, int seed)
    {
        var data = new byte[size];
        new Random(seed).NextBytes(data);
        return data;
    }
}
