using System.Text;
using ClipVault.Application.Abstractions;
using ClipVault.Application.Capture;
using ClipVault.Application.Capture.Classifiers;
using ClipVault.Application.Capture.Rules;
using ClipVault.Application.Clipboard;
using ClipVault.Application.Settings;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.ValueObjects;
using ClipVault.Infrastructure.Persistence;
using ClipVault.Infrastructure.Security;
using CommunityToolkit.Mvvm.Messaging;

namespace ClipVault.Infrastructure.Tests;

/// <summary>
/// An end-to-end test wiring up the real implementations (DPAPI key + ChaCha20-Poly1305 + SQLite + the ingestion
/// pipeline). It verifies the entire backend except WinRT/UI and the "ciphertext at rest (no plaintext left on
/// disk)" property.
/// </summary>
public sealed class CaptureToStorageIntegrationTests : IDisposable
{
    private readonly string _dir;
    private readonly ClipVaultStorageOptions _options;

    public CaptureToStorageIntegrationTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ClipVaultTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _options = new ClipVaultStorageOptions
        {
            DatabasePath = Path.Combine(_dir, "history.db"),
            KeyFilePath = Path.Combine(_dir, "dek.bin"),
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
    public async Task Full_pipeline_stores_dedups_and_blocks_secrets()
    {
        using var encryption = new ChaCha20Poly1305EncryptionService(new DpapiKeyVault(_options));
        using var repository = new SqliteClipboardHistoryRepository(_options, encryption);
        var ingestion = new ClipboardIngestionService(
            BuildRealGate(),
            encryption,
            repository,
            new TestClock(DateTimeOffset.UtcNow),
            new WeakReferenceMessenger());

        // New save -> decryption round trip.
        Assert.Equal(IngestionStatus.Added, (await ingestion.IngestAsync(Text("hello world"))).Status);
        var entry = Assert.Single(await repository.GetAllAsync());
        var materialized = await repository.MaterializeAsync(entry.Id);
        Assert.Equal("hello world", Encoding.UTF8.GetString(materialized!.Payload));

        // Duplicate -> promotion (the row count does not increase).
        Assert.Equal(IngestionStatus.Promoted, (await ingestion.IngestAsync(Text("hello world"))).Status);
        Assert.Single(await repository.GetAllAsync());

        // OS sensitive flag -> not saved.
        var blocked = await ingestion.IngestAsync(Text("top secret", new ClipboardPrivacySignals(true, null)));
        Assert.Equal(IngestionStatus.Rejected, blocked.Status);
        Assert.Single(await repository.GetAllAsync());

        // API key -> rejected.
        var apiKey = await ingestion.IngestAsync(Text("token sk-abcdefghijklmnopqrstuvwxyz0123"));
        Assert.Equal(IngestionStatus.Rejected, apiKey.Status);
        Assert.Single(await repository.GetAllAsync());
    }

    [Fact]
    public async Task Credit_card_is_masked_before_storage_and_at_rest()
    {
        using (var encryption = new ChaCha20Poly1305EncryptionService(new DpapiKeyVault(_options)))
        using (var repository = new SqliteClipboardHistoryRepository(_options, encryption))
        {
            var ingestion = new ClipboardIngestionService(
                BuildRealGate(),
                encryption,
                repository,
                new TestClock(DateTimeOffset.UtcNow),
                new WeakReferenceMessenger());

            Assert.Equal(IngestionStatus.Added, (await ingestion.IngestAsync(Text("card 4111 1111 1111 1111 ok"))).Status);

            var entry = Assert.Single(await repository.GetAllAsync());
            var materialized = await repository.MaterializeAsync(entry.Id);
            var text = Encoding.UTF8.GetString(materialized!.Payload);

            // The full card number never reaches storage; only the masked form (last four kept) does.
            Assert.Contains("1111", text, StringComparison.Ordinal);
            Assert.Contains("•", text, StringComparison.Ordinal);
            Assert.DoesNotContain("4111 1111 1111 1111", text, StringComparison.Ordinal);
            Assert.DoesNotContain("4111111111111111", text, StringComparison.Ordinal);
        }

        // The original digits must not survive anywhere on disk either.
        foreach (var file in Directory.GetFiles(_dir))
        {
            var content = Encoding.Latin1.GetString(await File.ReadAllBytesAsync(file));
            Assert.DoesNotContain("4111111111111111", content, StringComparison.Ordinal);
            Assert.DoesNotContain("4111 1111 1111 1111", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Stored_database_contains_no_plaintext()
    {
        const string secret = "PlaintextLeakCanary42";

        using (var encryption = new ChaCha20Poly1305EncryptionService(new DpapiKeyVault(_options)))
        using (var repository = new SqliteClipboardHistoryRepository(_options, encryption))
        {
            var ingestion = new ClipboardIngestionService(
                BuildRealGate(),
                encryption,
                repository,
                new TestClock(DateTimeOffset.UtcNow),
                new WeakReferenceMessenger());
            Assert.Equal(IngestionStatus.Added, (await ingestion.IngestAsync(Text(secret))).Status);
        }

        // After the connection is closed (the WAL is checkpointed), confirm that no plaintext remains in any related file.
        foreach (var file in Directory.GetFiles(_dir))
        {
            var content = Encoding.Latin1.GetString(await File.ReadAllBytesAsync(file));
            var index = content.IndexOf(secret, StringComparison.Ordinal);
            var context = index < 0
                ? string.Empty
                : content.Substring(Math.Max(0, index - 30), Math.Min(90, content.Length - Math.Max(0, index - 30)));
            Assert.True(index < 0, $"Plaintext present in {Path.GetFileName(file)} at offset {index}: …{context}…");
        }
    }

    private static ClipboardSnapshot Text(string text, ClipboardPrivacySignals? signals = null) =>
        new(
            ClipContentType.Text,
            Encoding.UTF8.GetBytes(text),
            text,
            Image: null,
            SourceApplication.Unknown,
            signals ?? ClipboardPrivacySignals.None);

    private static CaptureGate BuildRealGate()
    {
        var settings = new InMemorySettingsService();
        var classifiers = new IClipboardContentClassifier[]
        {
            new ApiKeyClassifier(), new PemPrivateKeyClassifier(), new JwtClassifier(),
            new CreditCardClassifier(), new GenericPasswordClassifier(settings),
        };
        return new CaptureGate(
        [
            new PrivacySignalRule(),
            new SourceAppRule(settings),
            new CaptureStateRule(new CaptureStateService()),
            new ContentClassificationRule(classifiers),
            new SizeRule(settings),
        ]);
    }

    private sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
