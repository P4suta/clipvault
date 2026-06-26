using ClipVault.Application.Retention;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Policies;
using NSubstitute;

namespace ClipVault.Application.Tests;

public class RetentionServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(200);

    // The service is a thin orchestrator: it delegates eviction to the repository's content-free primitives.
    // The eviction semantics themselves (age/count/byte/pinned) are covered by the repository tests.
    private static readonly RetentionSettings Settings =
        new() { MaxAge = TimeSpan.FromDays(30), MaxEntries = 7, MaxTotalBytes = 1234 };

    [Fact]
    public async Task Deletes_expired_then_trims_and_sums_the_removed_counts()
    {
        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.DeleteExpiredAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(3);
        repo.TrimAsync(Arg.Any<int>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(2);
        var service = new RetentionService(repo, new DefaultRetentionPolicy(Settings), Settings);

        Assert.Equal(5, await service.EnforceAsync(Now));
    }

    [Fact]
    public async Task Passes_the_age_cutoff_and_the_count_and_byte_budgets()
    {
        var repo = Substitute.For<IClipboardHistoryRepository>();
        var service = new RetentionService(repo, new DefaultRetentionPolicy(Settings), Settings);

        await service.EnforceAsync(Now);

        await repo.Received(1).DeleteExpiredAsync(Now - TimeSpan.FromDays(30), Arg.Any<CancellationToken>());
        await repo.Received(1).TrimAsync(7, 1234, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_zero_when_nothing_is_removed()
    {
        var repo = Substitute.For<IClipboardHistoryRepository>();
        var service = new RetentionService(repo, new DefaultRetentionPolicy(Settings), Settings);

        Assert.Equal(0, await service.EnforceAsync(Now));
    }
}
