using ClipVault.Domain.Entities;
using ClipVault.Domain.Policies;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Domain.Tests;

public class DefaultRetentionPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(100);
    private readonly DefaultRetentionPolicy _policy = new(
        new RetentionSettings { MaxAge = TimeSpan.FromDays(30) });

    [Fact]
    public void Evicts_unpinned_entry_older_than_max_age()
    {
        var old = EntryCapturedAt(Now.AddDays(-31));
        Assert.True(_policy.ShouldEvict(old, Now));
    }

    [Fact]
    public void Keeps_recent_entry()
    {
        var recent = EntryCapturedAt(Now.AddDays(-29));
        Assert.False(_policy.ShouldEvict(recent, Now));
    }

    [Fact]
    public void Never_evicts_pinned_entry_however_old()
    {
        var oldPinned = EntryCapturedAt(Now.AddDays(-3650), pinned: true);
        Assert.False(_policy.ShouldEvict(oldPinned, Now));
    }

    [Fact]
    public void Does_not_evict_at_exactly_max_age()
    {
        // now - capturedAt == MaxAge exactly; the rule uses > so the entry is kept.
        var exactly = EntryCapturedAt(Now.AddDays(-30));
        Assert.False(_policy.ShouldEvict(exactly, Now));
    }

    [Fact]
    public void Evicts_just_over_max_age()
    {
        var justOver = EntryCapturedAt(Now.AddDays(-30).AddTicks(-1));
        Assert.True(_policy.ShouldEvict(justOver, Now));
    }

    private static ClipboardEntry EntryCapturedAt(DateTimeOffset at, bool pinned = false)
    {
        var entry = ClipboardEntry.Create(
            ClipContentType.Text,
            new ContentHash("h"),
            "p",
            image: null,
            sizeInBytes: 1,
            SourceApplication.Unknown,
            capturedAt: at);
        if (pinned)
        {
            entry.Pin();
        }

        return entry;
    }
}
