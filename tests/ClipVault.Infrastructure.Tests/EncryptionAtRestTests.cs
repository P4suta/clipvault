using System.Security.Cryptography;
using System.Text;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;
using ClipVault.Infrastructure.Persistence;
using ClipVault.Infrastructure.Security;

namespace ClipVault.Infrastructure.Tests;

/// <summary>
/// Verifies the "encryption at rest" invariant at the repository layer with the real ChaCha20-Poly1305 service and a
/// real on-disk SQLite database (a <c>:memory:</c> database cannot be byte-scanned). Whatever the repository is asked
/// to store, none of the sensitive fields (payload, preview, source, thumbnail) may appear as plaintext in any database
/// file, and the content hash must be HMAC-keyed rather than a plain SHA-256 of the plaintext.
/// </summary>
public sealed class EncryptionAtRestTests : IDisposable
{
    private readonly string _dir;
    private readonly ClipVaultStorageOptions _options;

    public EncryptionAtRestTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ClipVaultAtRest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _options = new ClipVaultStorageOptions
        {
            DatabasePath = Path.Combine(_dir, "history.db"),
            KeyFilePath = Path.Combine(_dir, "unused.bin"),
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
    public async Task Payload_and_preview_are_encrypted_at_rest()
    {
        const string payloadSecret = "PayloadCanary_7Yq2Aa";
        const string previewSecret = "PreviewCanary_9Zx4Bb";

        using (var encryption = NewEncryption())
        using (var repo = new SqliteClipboardHistoryRepository(_options, encryption))
        {
            var entry = ClipboardEntry.Create(
                ClipContentType.Text,
                new ContentHash("h"),
                previewSecret,
                image: null,
                sizeInBytes: payloadSecret.Length,
                SourceApplication.Unknown,
                capturedAt: DateTimeOffset.UnixEpoch);
            await repo.AddAsync(entry, new ClipContent(ClipContentType.Text, Encoding.UTF8.GetBytes(payloadSecret)));
        }

        AssertAbsentFromAllFiles(payloadSecret);
        AssertAbsentFromAllFiles(previewSecret);
    }

    [Fact]
    public async Task Source_application_is_encrypted_at_rest()
    {
        using (var encryption = NewEncryption())
        using (var repo = new SqliteClipboardHistoryRepository(_options, encryption))
        {
            var source = new SourceApplication("SourceCanary_Proc55", "WindowCanary_W77", @"C:\PathCanary_P88\app.exe");
            var entry = ClipboardEntry.Create(
                ClipContentType.Text,
                new ContentHash("h"),
                "preview",
                image: null,
                sizeInBytes: 1,
                source,
                capturedAt: DateTimeOffset.UnixEpoch);
            await repo.AddAsync(entry, new ClipContent(ClipContentType.Text, [1]));
        }

        AssertAbsentFromAllFiles("SourceCanary_Proc55");
        AssertAbsentFromAllFiles("WindowCanary_W77");
        AssertAbsentFromAllFiles("PathCanary_P88");
    }

    [Fact]
    public async Task Thumbnail_is_encrypted_at_rest()
    {
        var marker = Encoding.UTF8.GetBytes("ThumbCanary_T99Cc");

        using (var encryption = NewEncryption())
        using (var repo = new SqliteClipboardHistoryRepository(_options, encryption))
        {
            var entry = ClipboardEntry.Create(
                ClipContentType.Image,
                new ContentHash("img"),
                "Image",
                new ImagePreview(marker, Width: 1, Height: 1),
                sizeInBytes: marker.Length,
                SourceApplication.Unknown,
                capturedAt: DateTimeOffset.UnixEpoch);
            await repo.AddAsync(entry, new ClipContent(ClipContentType.Image, marker));
        }

        AssertAbsentFromAllFiles("ThumbCanary_T99Cc");
    }

    [Fact]
    public async Task Stored_content_hash_is_keyed_not_plain_sha256()
    {
        const string secret = "HashCanary_H123Dd";
        var plaintext = Encoding.UTF8.GetBytes(secret);
        var plainSha256 = Convert.ToHexString(SHA256.HashData(plaintext));
        string keyedHashHex;

        using (var encryption = NewEncryption())
        using (var repo = new SqliteClipboardHistoryRepository(_options, encryption))
        {
            var keyedHash = encryption.KeyedHash(plaintext);
            keyedHashHex = keyedHash.Value;
            var entry = ClipboardEntry.Create(
                ClipContentType.Text,
                keyedHash,
                "preview",
                image: null,
                sizeInBytes: plaintext.Length,
                SourceApplication.Unknown,
                capturedAt: DateTimeOffset.UnixEpoch);
            await repo.AddAsync(entry, new ClipContent(ClipContentType.Text, plaintext));
        }

        // The content hash is stored in the clear (it is non-reversible) but it must be the keyed HMAC,
        // not a plain SHA-256 that would let an attacker correlate known plaintexts across vaults.
        Assert.NotEqual(plainSha256, keyedHashHex);
        AssertAbsentFromAllFiles(plainSha256);
        AssertAbsentFromAllFiles(secret);
        AssertPresentInSomeFile(keyedHashHex);
    }

    private static ChaCha20Poly1305EncryptionService NewEncryption()
    {
        var key = new byte[32];
        for (var i = 0; i < key.Length; i++)
        {
            key[i] = (byte)((i * 7) + 1);
        }

        return new ChaCha20Poly1305EncryptionService(new FixedKeyVault(key));
    }

    private void AssertAbsentFromAllFiles(string needle)
    {
        foreach (var file in Directory.GetFiles(_dir))
        {
            var content = Encoding.Latin1.GetString(File.ReadAllBytes(file));
            Assert.DoesNotContain(needle, content, StringComparison.Ordinal);
        }
    }

    private void AssertPresentInSomeFile(string needle)
    {
        var present = Directory.GetFiles(_dir)
            .Any(file => Encoding.Latin1.GetString(File.ReadAllBytes(file)).Contains(needle, StringComparison.Ordinal));

        Assert.True(present, $"Expected '{needle}' to be present in a database file.");
    }
}
