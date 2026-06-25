using ClipVault.Domain.Entities;
using ClipVault.Domain.Policies;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Domain.Tests;

public class DefaultRetentionPolicyEdgeTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(100);

    [Fact]
    public void Zero_max_age_evicts_anything_older_than_an_instant()
    {
        var policy = new DefaultRetentionPolicy(new RetentionSettings { MaxAge = TimeSpan.Zero });

        Assert.True(policy.ShouldEvict(Entry(Now.AddTicks(-1)), Now));
        Assert.False(policy.ShouldEvict(Entry(Now), Now));
    }

    [Fact]
    public void Never_evicts_a_pinned_entry_even_with_zero_max_age()
    {
        var policy = new DefaultRetentionPolicy(new RetentionSettings { MaxAge = TimeSpan.Zero });
        var pinned = Entry(Now.AddDays(-1000));
        pinned.Pin();

        Assert.False(policy.ShouldEvict(pinned, Now));
    }

    [Fact]
    public void Does_not_evict_an_entry_captured_in_the_future()
    {
        var policy = new DefaultRetentionPolicy(new RetentionSettings { MaxAge = TimeSpan.FromDays(1) });

        Assert.False(policy.ShouldEvict(Entry(Now.AddDays(1)), Now));
    }

    private static ClipboardEntry Entry(DateTimeOffset capturedAt) => ClipboardEntry.Create(
        ClipContentType.Text,
        new ContentHash("h"),
        "p",
        image: null,
        sizeInBytes: 1,
        SourceApplication.Unknown,
        capturedAt);
}
