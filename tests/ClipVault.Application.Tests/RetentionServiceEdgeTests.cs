using ClipVault.Application.Retention;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.Policies;
using ClipVault.Domain.ValueObjects;
using NSubstitute;

namespace ClipVault.Application.Tests;

public class RetentionServiceEdgeTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(200);

    [Fact]
    public async Task Empty_repository_removes_nothing()
    {
        var service = Build([], new RetentionSettings { MaxEntries = 1, MaxTotalBytes = 1 }, out var repo);

        Assert.Equal(0, await service.EnforceAsync(Now));
        await repo.DidNotReceive().RemoveAsync(Arg.Any<EntryId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Keeps_exactly_max_entries()
    {
        var entries = new List<ClipboardEntry>
        {
            Entry("a", Now.AddMinutes(-1)),
            Entry("b", Now.AddMinutes(-2)),
            Entry("c", Now.AddMinutes(-3)),
        };
        var service = Build(entries, new RetentionSettings { MaxEntries = 3, MaxTotalBytes = long.MaxValue }, out var repo);

        Assert.Equal(0, await service.EnforceAsync(Now));
        await repo.DidNotReceive().RemoveAsync(Arg.Any<EntryId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Keeps_when_total_bytes_is_exactly_at_the_cap()
    {
        var entries = new List<ClipboardEntry>
        {
            Entry("a", Now.AddMinutes(-1), size: 100),
            Entry("b", Now.AddMinutes(-2), size: 100),
        };
        var service = Build(entries, new RetentionSettings { MaxEntries = 100, MaxTotalBytes = 200 }, out var repo);

        Assert.Equal(0, await service.EnforceAsync(Now));
        await repo.DidNotReceive().RemoveAsync(Arg.Any<EntryId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pinned_entries_do_not_count_toward_the_entry_limit()
    {
        var entries = new List<ClipboardEntry>
        {
            Entry("pin", Now.AddMinutes(-1), pinned: true),
            Entry("u1", Now.AddMinutes(-2)),
            Entry("u2", Now.AddMinutes(-3)),
        };
        var service = Build(entries, new RetentionSettings { MaxEntries = 2, MaxTotalBytes = long.MaxValue }, out var repo);

        Assert.Equal(0, await service.EnforceAsync(Now));
        await repo.DidNotReceive().RemoveAsync(Arg.Any<EntryId>(), Arg.Any<CancellationToken>());
    }

    private static RetentionService Build(
        List<ClipboardEntry> entries, RetentionSettings settings, out IClipboardHistoryRepository repo)
    {
        var withAge = settings with { MaxAge = TimeSpan.FromDays(30) };
        repo = Substitute.For<IClipboardHistoryRepository>();
        repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(entries);
        return new RetentionService(repo, new DefaultRetentionPolicy(withAge), withAge);
    }

    private static ClipboardEntry Entry(string hash, DateTimeOffset capturedAt, bool pinned = false, long size = 10)
    {
        var entry = ClipboardEntry.Create(
            ClipContentType.Text,
            new ContentHash(hash),
            hash,
            image: null,
            sizeInBytes: size,
            SourceApplication.Unknown,
            capturedAt);
        if (pinned)
        {
            entry.Pin();
        }

        return entry;
    }
}
