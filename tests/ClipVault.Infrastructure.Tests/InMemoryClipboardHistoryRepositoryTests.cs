using System.Text;
using ClipVault.Domain.Entities;
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
