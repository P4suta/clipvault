using ClipVault.Domain.Entities;
using ClipVault.Domain.Policies;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Domain.Tests;

public class ClipboardEntryTests
{
    [Fact]
    public void Create_starts_unpinned_and_unused()
    {
        var at = DateTimeOffset.UnixEpoch;
        var entry = NewText(at);

        Assert.False(entry.IsPinned);
        Assert.Equal(at, entry.CapturedAt);
        Assert.Equal(at, entry.LastUsedAt);
    }

    [Fact]
    public void Pin_then_Unpin_toggles_state()
    {
        var entry = NewText(DateTimeOffset.UnixEpoch);

        entry.Pin();
        Assert.True(entry.IsPinned);

        entry.Unpin();
        Assert.False(entry.IsPinned);
    }

    [Fact]
    public void MarkUsed_updates_last_used_without_touching_captured_at()
    {
        var captured = DateTimeOffset.UnixEpoch;
        var entry = NewText(captured);
        var used = captured.AddHours(3);

        entry.MarkUsed(used);

        Assert.Equal(used, entry.LastUsedAt);
        Assert.Equal(captured, entry.CapturedAt);
    }

    [Fact]
    public void Entries_are_equal_by_id()
    {
        var id = EntryId.New();
        ClipboardEntry Restore() => ClipboardEntry.Restore(
            id,
            ClipContentType.Text,
            new ContentHash("h"),
            "a",
            image: null,
            sizeInBytes: 1,
            SourceApplication.Unknown,
            capturedAt: DateTimeOffset.UnixEpoch,
            lastUsedAt: DateTimeOffset.UnixEpoch,
            isPinned: false);

        Assert.Equal(Restore(), Restore());
        Assert.NotEqual(NewText(DateTimeOffset.UnixEpoch), NewText(DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void IsExpired_delegates_to_the_policy()
    {
        var entry = NewText(DateTimeOffset.UnixEpoch);

        Assert.True(entry.IsExpired(new AlwaysEvictPolicy(), DateTimeOffset.UnixEpoch));
        Assert.False(entry.IsExpired(new NeverEvictPolicy(), DateTimeOffset.UnixEpoch));
    }

    private static ClipboardEntry NewText(DateTimeOffset at) =>
        ClipboardEntry.Create(
            ClipContentType.Text,
            new ContentHash("h"),
            "hello",
            image: null,
            sizeInBytes: 5,
            SourceApplication.Unknown,
            capturedAt: at);

    private sealed class AlwaysEvictPolicy : IRetentionPolicy
    {
        public bool ShouldEvict(ClipboardEntry entry, DateTimeOffset now) => true;

        public DateTimeOffset EvictionCutoff(DateTimeOffset now) => DateTimeOffset.MaxValue;
    }

    private sealed class NeverEvictPolicy : IRetentionPolicy
    {
        public bool ShouldEvict(ClipboardEntry entry, DateTimeOffset now) => false;

        public DateTimeOffset EvictionCutoff(DateTimeOffset now) => DateTimeOffset.MinValue;
    }
}
