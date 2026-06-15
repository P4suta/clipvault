using ClipVault.Application.Retention;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.Policies;
using ClipVault.Domain.ValueObjects;
using NSubstitute;

namespace ClipVault.Application.Tests;

public class RetentionServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(200);

    [Fact]
    public async Task Removes_aged_and_count_overflow_but_keeps_pinned()
    {
        var pinnedOld = Entry("pinned", Now.AddDays(-100), pinned: true);
        var aged = Entry("aged", Now.AddDays(-40));
        var b = Entry("b", Now.AddDays(-3));
        var c = Entry("c", Now.AddDays(-2));
        var d = Entry("d", Now.AddDays(-1));

        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ClipboardEntry> { pinnedOld, d, c, b, aged });

        var settings = new RetentionSettings { MaxAge = TimeSpan.FromDays(30), MaxEntries = 2, MaxTotalBytes = long.MaxValue };
        var service = new RetentionService(repo, new DefaultRetentionPolicy(settings), settings);

        var removed = await service.EnforceAsync(Now);

        Assert.Equal(2, removed); // aged (age) + b (count limit)
        await repo.Received(1).RemoveAsync(aged.Id, Arg.Any<CancellationToken>());
        await repo.Received(1).RemoveAsync(b.Id, Arg.Any<CancellationToken>());
        await repo.DidNotReceive().RemoveAsync(pinnedOld.Id, Arg.Any<CancellationToken>());
        await repo.DidNotReceive().RemoveAsync(c.Id, Arg.Any<CancellationToken>());
        await repo.DidNotReceive().RemoveAsync(d.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enforces_total_byte_cap()
    {
        var newest = Entry("newest", Now.AddMinutes(-1), size: 100);
        var middle = Entry("middle", Now.AddMinutes(-2), size: 100);
        var oldest = Entry("oldest", Now.AddMinutes(-3), size: 100);

        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ClipboardEntry> { newest, middle, oldest });

        var settings = new RetentionSettings { MaxAge = TimeSpan.FromDays(30), MaxEntries = 100, MaxTotalBytes = 250 };
        var service = new RetentionService(repo, new DefaultRetentionPolicy(settings), settings);

        var removed = await service.EnforceAsync(Now);

        Assert.Equal(1, removed); // 100+100 fits, but the third reaches 300 > 250, so oldest is removed
        await repo.Received(1).RemoveAsync(oldest.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Keeps_everything_when_all_entries_are_pinned()
    {
        var a = Entry("a", Now.AddDays(-100), pinned: true);
        var b = Entry("b", Now.AddDays(-200), pinned: true);
        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<ClipboardEntry> { a, b });
        var settings = new RetentionSettings { MaxAge = TimeSpan.FromDays(30), MaxEntries = 1, MaxTotalBytes = 1 };
        var service = new RetentionService(repo, new DefaultRetentionPolicy(settings), settings);

        Assert.Equal(0, await service.EnforceAsync(Now));
        await repo.DidNotReceive().RemoveAsync(Arg.Any<EntryId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Always_keeps_the_first_entry_even_if_it_exceeds_the_byte_cap()
    {
        var only = Entry("only", Now.AddMinutes(-1), size: 1000);
        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<ClipboardEntry> { only });
        var settings = new RetentionSettings { MaxAge = TimeSpan.FromDays(30), MaxEntries = 100, MaxTotalBytes = 10 };
        var service = new RetentionService(repo, new DefaultRetentionPolicy(settings), settings);

        // The kept > 0 guard means the first (and only) entry is never removed for the byte cap.
        Assert.Equal(0, await service.EnforceAsync(Now));
        await repo.DidNotReceive().RemoveAsync(only.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Count_limit_keeps_the_most_recently_used()
    {
        var newest = Entry("newest", Now.AddMinutes(-1));
        var older = Entry("older", Now.AddMinutes(-5));
        var oldest = Entry("oldest", Now.AddMinutes(-9));
        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<ClipboardEntry> { newest, older, oldest });
        var settings = new RetentionSettings { MaxAge = TimeSpan.FromDays(30), MaxEntries = 1, MaxTotalBytes = long.MaxValue };
        var service = new RetentionService(repo, new DefaultRetentionPolicy(settings), settings);

        Assert.Equal(2, await service.EnforceAsync(Now));
        await repo.Received(1).RemoveAsync(older.Id, Arg.Any<CancellationToken>());
        await repo.Received(1).RemoveAsync(oldest.Id, Arg.Any<CancellationToken>());
        await repo.DidNotReceive().RemoveAsync(newest.Id, Arg.Any<CancellationToken>());
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
