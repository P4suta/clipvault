using System.Text;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;
using ClipVault.Infrastructure.Persistence;

namespace ClipVault.Infrastructure.Tests;

public class SqliteClipboardHistoryRepositoryTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch.AddDays(1);

    // Expected ordering of pinned-first plus last-used descending ("b" is pinned, the rest are newest first).
    private static readonly string[] ExpectedPinnedOrder = ["b", "c", "a"];

    [Fact]
    public async Task Add_then_find_by_hash_returns_entry()
    {
        using var repo = NewRepo();
        var entry = TextEntry("h1", "hello", T0);
        await repo.AddAsync(entry, Text("hello world"));

        var found = await repo.FindByHashAsync(new ContentHash("h1"));

        Assert.NotNull(found);
        Assert.Equal(entry.Id, found!.Id);
        Assert.Equal("hello", found.Preview);
    }

    [Fact]
    public async Task Find_by_hash_returns_null_when_absent()
    {
        using var repo = NewRepo();
        Assert.Null(await repo.FindByHashAsync(new ContentHash("missing")));
    }

    [Fact]
    public async Task Get_all_orders_pinned_first_then_most_recent()
    {
        using var repo = NewRepo();
        var a = TextEntry("a", "a", T0.AddMinutes(1));
        var b = TextEntry("b", "b", T0.AddMinutes(2));
        var c = TextEntry("c", "c", T0.AddMinutes(3));
        await repo.AddAsync(a, Text("a"));
        await repo.AddAsync(b, Text("b"));
        await repo.AddAsync(c, Text("c"));

        b.Pin();
        await repo.UpdateAsync(b);

        var all = await repo.GetAllAsync();

        Assert.Equal(ExpectedPinnedOrder, all.Select(e => e.Preview));
    }

    [Fact]
    public async Task Materialize_round_trips_full_payload()
    {
        using var repo = NewRepo();
        var entry = TextEntry("h", "preview", T0);
        await repo.AddAsync(entry, Text("the full clipboard payload"));

        var content = await repo.MaterializeAsync(entry.Id);

        Assert.NotNull(content);
        Assert.Equal("the full clipboard payload", Encoding.UTF8.GetString(content!.Payload));
    }

    [Fact]
    public async Task Update_persists_pin_and_last_used()
    {
        using var repo = NewRepo();
        var entry = TextEntry("h", "p", T0);
        await repo.AddAsync(entry, Text("p"));

        entry.Pin();
        entry.MarkUsed(T0.AddDays(2));
        await repo.UpdateAsync(entry);

        var reloaded = await repo.FindByHashAsync(new ContentHash("h"));
        Assert.True(reloaded!.IsPinned);
        Assert.Equal(T0.AddDays(2), reloaded.LastUsedAt);
    }

    [Fact]
    public async Task Remove_and_clear_delete_entries()
    {
        using var repo = NewRepo();
        var entry = TextEntry("h", "p", T0);
        await repo.AddAsync(entry, Text("p"));
        Assert.Equal(1, await repo.CountAsync());

        await repo.RemoveAsync(entry.Id);
        Assert.Equal(0, await repo.CountAsync());

        await repo.AddAsync(TextEntry("h2", "q", T0), Text("q"));
        await repo.ClearAsync();
        Assert.Equal(0, await repo.CountAsync());
    }

    [Fact]
    public async Task Image_entry_round_trips_thumbnail_and_dimensions()
    {
        using var repo = NewRepo();
        var image = new ImagePreview(new byte[] { 9, 8, 7 }, Width: 100, Height: 50);
        var entry = ClipboardEntry.Create(
            ClipContentType.Image,
            new ContentHash("img"),
            "Image 100x50",
            image,
            sizeInBytes: 4,
            SourceApplication.Unknown,
            capturedAt: T0);
        await repo.AddAsync(entry, new ClipContent(ClipContentType.Image, new byte[] { 1, 2, 3, 4 }));

        var got = Assert.Single(await repo.GetAllAsync());

        Assert.Equal(ClipContentType.Image, got.ContentType);
        Assert.NotNull(got.Image);
        Assert.Equal(new byte[] { 9, 8, 7 }, got.Image!.Thumbnail);
        Assert.Equal(100, got.Image.Width);
        Assert.Equal(50, got.Image.Height);
    }

    [Fact]
    public async Task Materialize_round_trips_image_payload()
    {
        using var repo = NewRepo();
        var payload = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };
        var entry = ClipboardEntry.Create(
            ClipContentType.Image,
            new ContentHash("imgp"),
            "Image 1x1",
            new ImagePreview(new byte[] { 1 }, Width: 1, Height: 1),
            sizeInBytes: payload.Length,
            SourceApplication.Unknown,
            capturedAt: T0);
        await repo.AddAsync(entry, new ClipContent(ClipContentType.Image, payload));

        var content = await repo.MaterializeAsync(entry.Id);

        Assert.NotNull(content);
        Assert.Equal(ClipContentType.Image, content!.Type);
        Assert.Equal(payload, content.Payload);
    }

    [Fact]
    public async Task Source_application_round_trips_including_nulls()
    {
        using var repo = NewRepo();
        var source = new SourceApplication("notepad.exe", WindowTitle: null, ExecutablePath: @"C:\Windows\notepad.exe");
        var entry = ClipboardEntry.Create(
            ClipContentType.Text,
            new ContentHash("s"),
            "x",
            image: null,
            sizeInBytes: 1,
            source,
            capturedAt: T0);
        await repo.AddAsync(entry, Text("x"));

        var got = Assert.Single(await repo.GetAllAsync());

        Assert.Equal("notepad.exe", got.Source.ProcessName);
        Assert.Null(got.Source.WindowTitle);
        Assert.Equal(@"C:\Windows\notepad.exe", got.Source.ExecutablePath);
    }

    [Fact]
    public async Task Clear_securely_removes_plaintext_from_the_database_files()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ClipVaultDb_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "history.db");
        const string marker = "TOPSECRET-MARKER-9c3f1a2b5d";
        try
        {
            using (var repo = new SqliteClipboardHistoryRepository(
                new ClipVaultStorageOptions { DatabasePath = dbPath, KeyFilePath = "unused" },
                new IdentityEncryptionService()))
            {
                await repo.AddAsync(TextEntry("h", "preview", T0), Text(marker));
                await repo.ClearAsync();
            }

            // IdentityEncryptionService stores plaintext, so the marker would survive only in a freed (non-zeroed) page.
            // secure_delete + wal_checkpoint(TRUNCATE) + VACUUM must leave no trace in any database file.
            var residue = new StringBuilder();
            foreach (var file in Directory.GetFiles(dir))
            {
                residue.Append(Encoding.Latin1.GetString(await File.ReadAllBytesAsync(file)));
            }

            Assert.DoesNotContain(marker, residue.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
                // Best effort.
            }
        }
    }

    private static SqliteClipboardHistoryRepository NewRepo() =>
        new(
            new ClipVaultStorageOptions { DatabasePath = ":memory:", KeyFilePath = "unused" },
            new IdentityEncryptionService());

    private static ClipboardEntry TextEntry(string hash, string preview, DateTimeOffset at) =>
        ClipboardEntry.Create(
            ClipContentType.Text,
            new ContentHash(hash),
            preview,
            image: null,
            sizeInBytes: 3,
            SourceApplication.Unknown,
            capturedAt: at);

    private static ClipContent Text(string s) => new(ClipContentType.Text, Encoding.UTF8.GetBytes(s));
}
