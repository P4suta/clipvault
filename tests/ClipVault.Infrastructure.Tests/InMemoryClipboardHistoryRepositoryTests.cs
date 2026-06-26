using System.Text;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.Policies;
using ClipVault.Domain.ValueObjects;
using ClipVault.Infrastructure.Persistence;

namespace ClipVault.Infrastructure.Tests;

public class InMemoryClipboardHistoryRepositoryTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch.AddDays(1);

    // Expected ordering of pinned-first plus last-used descending ("b" is pinned, the rest are newest first).
    private static readonly string[] ExpectedPinnedOrder = ["b", "c", "a"];

    [Fact]
    public async Task Add_find_materialize_round_trip()
    {
        var repo = new InMemoryClipboardHistoryRepository();
        var entry = TextEntry("h1", "hello", T0);
        await repo.AddAsync(entry, Text("hello world"));

        var found = await repo.FindByHashAsync(new ContentHash("h1"));
        Assert.Equal(entry.Id, found!.Id);

        var content = await repo.MaterializeAsync(entry.Id);
        Assert.Equal("hello world", Encoding.UTF8.GetString(content!.Payload));
    }

    [Fact]
    public async Task Materialize_returns_a_caller_owned_copy_so_disposal_does_not_corrupt_the_store()
    {
        var repo = new InMemoryClipboardHistoryRepository();
        var entry = TextEntry("h1", "hello", T0);
        await repo.AddAsync(entry, Text("hello world"));

        // Disposing the materialized instance zeroes its payload; it must be a copy, not the stored one.
        var first = await repo.MaterializeAsync(entry.Id);
        first!.Dispose();

        var second = await repo.MaterializeAsync(entry.Id);
        Assert.Equal("hello world", Encoding.UTF8.GetString(second!.Payload));
    }

    [Fact]
    public async Task Get_all_orders_pinned_first_then_recent()
    {
        var repo = new InMemoryClipboardHistoryRepository();
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
    public async Task Remove_and_clear()
    {
        var repo = new InMemoryClipboardHistoryRepository();
        var entry = TextEntry("h", "p", T0);
        await repo.AddAsync(entry, Text("p"));
        Assert.Equal(1, await repo.CountAsync());

        await repo.RemoveAsync(entry.Id);
        Assert.Equal(0, await repo.CountAsync());
        Assert.Null(await repo.FindByHashAsync(new ContentHash("h")));

        await repo.AddAsync(TextEntry("h2", "q", T0), Text("q"));
        await repo.ClearAsync();
        Assert.Equal(0, await repo.CountAsync());
    }

    [Fact]
    public async Task Materialize_round_trips_image_payload()
    {
        var repo = new InMemoryClipboardHistoryRepository();
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
    public async Task Concurrent_adds_are_thread_safe()
    {
        var repo = new InMemoryClipboardHistoryRepository();

        var adds = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() => repo.AddAsync(TextEntry($"h{i}", $"p{i}", T0), Text($"p{i}"))));
        await Task.WhenAll(adds);

        Assert.Equal(100, await repo.CountAsync());
    }

    [Fact]
    public async Task Concurrent_reads_and_writes_do_not_throw()
    {
        var repo = new InMemoryClipboardHistoryRepository();

        var tasks = new List<Task>();
        tasks.AddRange(Enumerable.Range(0, 50)
            .Select(i => Task.Run(() => repo.AddAsync(TextEntry($"h{i}", $"p{i}", T0), Text($"p{i}")))));
        tasks.AddRange(Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => repo.GetAllAsync())));

        await Task.WhenAll(tasks);

        Assert.Equal(50, await repo.CountAsync());
    }

    [Fact]
    public async Task Add_evicts_oldest_unpinned_when_over_count_budget()
    {
        var repo = new InMemoryClipboardHistoryRepository(
            new RetentionSettings { MaxEntries = 2, MaxTotalBytes = long.MaxValue });

        await repo.AddAsync(TextEntry("h1", "a", T0.AddMinutes(1)), Text("a"));
        await repo.AddAsync(TextEntry("h2", "b", T0.AddMinutes(2)), Text("b"));
        await repo.AddAsync(TextEntry("h3", "c", T0.AddMinutes(3)), Text("c"));

        // The oldest unpinned entry ("a") is evicted immediately to keep the ring at the 2-entry budget.
        Assert.Equal(2, await repo.CountAsync());
        string[] expected = ["c", "b"];
        Assert.Equal(expected, (await repo.GetAllAsync()).Select(e => e.Preview));
        Assert.Null(await repo.FindByHashAsync(new ContentHash("h1")));
    }

    [Fact]
    public async Task Add_evicts_until_under_byte_budget()
    {
        // Each TextEntry reports sizeInBytes = 3, so a 7-byte budget admits at most two entries.
        var repo = new InMemoryClipboardHistoryRepository(
            new RetentionSettings { MaxEntries = int.MaxValue, MaxTotalBytes = 7 });

        await repo.AddAsync(TextEntry("h1", "a", T0.AddMinutes(1)), Text("a"));
        await repo.AddAsync(TextEntry("h2", "b", T0.AddMinutes(2)), Text("b"));
        await repo.AddAsync(TextEntry("h3", "c", T0.AddMinutes(3)), Text("c"));

        Assert.Equal(2, await repo.CountAsync());
    }

    [Fact]
    public async Task Pinned_entries_are_exempt_from_ring_eviction()
    {
        var repo = new InMemoryClipboardHistoryRepository(
            new RetentionSettings { MaxEntries = 2, MaxTotalBytes = long.MaxValue });

        var pinned = TextEntry("h1", "a", T0.AddMinutes(1));
        await repo.AddAsync(pinned, Text("a"));
        pinned.Pin();
        await repo.UpdateAsync(pinned);

        await repo.AddAsync(TextEntry("h2", "b", T0.AddMinutes(2)), Text("b"));
        await repo.AddAsync(TextEntry("h3", "c", T0.AddMinutes(3)), Text("c"));

        // Budget is 2: the pinned "a" is always kept; among unpinned, only the newest ("c") survives.
        string[] expected = ["a", "c"];
        Assert.Equal(expected, (await repo.GetAllAsync()).Select(e => e.Preview));
    }

    [Fact]
    public async Task DeleteExpired_removes_unpinned_older_than_cutoff_and_keeps_pinned()
    {
        var repo = new InMemoryClipboardHistoryRepository();
        var pinnedOld = TextEntry("p", "p", T0.AddMinutes(1));
        await repo.AddAsync(pinnedOld, Text("p"));
        pinnedOld.Pin();
        await repo.UpdateAsync(pinnedOld);
        await repo.AddAsync(TextEntry("o", "o", T0.AddMinutes(2)), Text("o"));
        await repo.AddAsync(TextEntry("r", "r", T0.AddMinutes(10)), Text("r"));

        var removed = await repo.DeleteExpiredAsync(T0.AddMinutes(5));

        Assert.Equal(1, removed); // only the unpinned "o" captured before the cutoff
        string[] survivors = ["p", "r"];
        Assert.Equal(survivors, (await repo.GetAllAsync()).Select(e => e.Preview));
    }

    [Fact]
    public async Task Trim_keeps_the_most_recent_within_the_count_budget()
    {
        var repo = new InMemoryClipboardHistoryRepository();
        await repo.AddAsync(TextEntry("a", "a", T0.AddMinutes(1)), Text("a"));
        await repo.AddAsync(TextEntry("b", "b", T0.AddMinutes(2)), Text("b"));
        await repo.AddAsync(TextEntry("c", "c", T0.AddMinutes(3)), Text("c"));

        var removed = await repo.TrimAsync(maxEntries: 1, maxTotalBytes: long.MaxValue);

        Assert.Equal(2, removed);
        string[] survivors = ["c"];
        Assert.Equal(survivors, (await repo.GetAllAsync()).Select(e => e.Preview));
    }

    [Fact]
    public async Task Trim_keeps_the_single_newest_even_when_it_exceeds_the_byte_budget()
    {
        var repo = new InMemoryClipboardHistoryRepository();
        await repo.AddAsync(Sized("only", 1000, T0.AddMinutes(1)), Text("x"));

        Assert.Equal(0, await repo.TrimAsync(maxEntries: 100, maxTotalBytes: 10)); // rank 1 is always kept
    }

    [Fact]
    public async Task Trim_running_sum_evicts_everything_after_the_budget_is_first_exceeded()
    {
        // Sizes most-recent-first: 100, 200, 50. Running sums: 100, 300, 350. A 200-byte budget keeps only the first,
        // so the trailing small entry cannot sneak back under the quota after the large one is evicted.
        var repo = new InMemoryClipboardHistoryRepository();
        await repo.AddAsync(Sized("newest", 100, T0.AddMinutes(3)), Text("n"));
        await repo.AddAsync(Sized("middle", 200, T0.AddMinutes(2)), Text("m"));
        await repo.AddAsync(Sized("small", 50, T0.AddMinutes(1)), Text("s"));

        var removed = await repo.TrimAsync(maxEntries: 100, maxTotalBytes: 200);

        Assert.Equal(2, removed);
        string[] survivors = ["newest"];
        Assert.Equal(survivors, (await repo.GetAllAsync()).Select(e => e.Preview));
    }

    [Fact]
    public async Task Trim_exempts_pinned_entries_from_both_budgets()
    {
        var repo = new InMemoryClipboardHistoryRepository();
        var pinned = Sized("pin", 1000, T0.AddMinutes(1));
        await repo.AddAsync(pinned, Text("p"));
        pinned.Pin();
        await repo.UpdateAsync(pinned);
        await repo.AddAsync(Sized("u", 10, T0.AddMinutes(2)), Text("u"));

        // The pinned entry never counts; the lone unpinned entry is rank 1 and is always kept.
        Assert.Equal(0, await repo.TrimAsync(maxEntries: 1, maxTotalBytes: 1));
    }

    [Fact]
    public async Task GetPage_returns_entries_in_the_same_order_as_get_all()
    {
        var repo = new InMemoryClipboardHistoryRepository();
        var a = TextEntry("a", "a", T0.AddMinutes(1));
        var b = TextEntry("b", "b", T0.AddMinutes(2));
        var c = TextEntry("c", "c", T0.AddMinutes(3));
        await repo.AddAsync(a, Text("a"));
        await repo.AddAsync(b, Text("b"));
        await repo.AddAsync(c, Text("c"));
        b.Pin();
        await repo.UpdateAsync(b);

        var paged = await DrainAsync(repo, pageSize: 2);
        var all = await repo.GetAllAsync();

        Assert.Equal(all.Select(e => e.Preview), paged.Select(e => e.Preview));
    }

    [Fact]
    public async Task GetPage_pages_through_all_entries_without_gaps_or_duplicates_even_with_tied_timestamps()
    {
        var repo = new InMemoryClipboardHistoryRepository();
        var seeded = new List<ClipboardEntry>();
        for (var i = 0; i < 5; i++)
        {
            // Identical timestamps force every comparison onto the id tiebreak, mirroring the SQLite BLOB ordering.
            var entry = TextEntry($"h{i}", $"p{i}", T0);
            await repo.AddAsync(entry, Text($"p{i}"));
            seeded.Add(entry);
        }

        var paged = await DrainAsync(repo, pageSize: 2);

        Assert.Equal(5, paged.Count);
        Assert.Equal(seeded.Select(e => e.Id).ToHashSet(), paged.Select(e => e.Id).ToHashSet());
    }

    [Fact]
    public async Task GetThumbnail_returns_bytes_for_an_image_and_null_for_text_or_missing()
    {
        var repo = new InMemoryClipboardHistoryRepository();
        var imageEntry = ClipboardEntry.Create(
            ClipContentType.Image,
            new ContentHash("img"),
            "Image 1x1",
            new ImagePreview(new byte[] { 9, 8, 7 }, Width: 1, Height: 1),
            sizeInBytes: 4,
            SourceApplication.Unknown,
            capturedAt: T0);
        await repo.AddAsync(imageEntry, new ClipContent(ClipContentType.Image, new byte[] { 1, 2, 3, 4 }));
        var textEntry = TextEntry("t", "txt", T0);
        await repo.AddAsync(textEntry, Text("txt"));

        Assert.Equal(new byte[] { 9, 8, 7 }, await repo.GetThumbnailAsync(imageEntry.Id));
        Assert.Null(await repo.GetThumbnailAsync(textEntry.Id));
        Assert.Null(await repo.GetThumbnailAsync(EntryId.New()));
    }

    private static async Task<List<ClipboardEntry>> DrainAsync(InMemoryClipboardHistoryRepository repo, int pageSize)
    {
        var all = new List<ClipboardEntry>();
        HistoryCursor? cursor = null;
        do
        {
            var page = await repo.GetPageAsync(cursor, pageSize);
            all.AddRange(page.Entries);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        return all;
    }

    private static ClipboardEntry Sized(string hash, long size, DateTimeOffset at) =>
        ClipboardEntry.Create(
            ClipContentType.Text,
            new ContentHash(hash),
            hash,
            image: null,
            sizeInBytes: size,
            SourceApplication.Unknown,
            capturedAt: at);

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
