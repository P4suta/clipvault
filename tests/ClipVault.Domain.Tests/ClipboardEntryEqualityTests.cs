using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Domain.Tests;

public class ClipboardEntryEqualityTests
{
    [Fact]
    public void Restore_retains_every_field()
    {
        var id = EntryId.New();
        var image = new ImagePreview([1, 2, 3], 4, 5);
        var source = new SourceApplication("paint", "Untitled", @"C:\paint.exe");
        var captured = DateTimeOffset.UnixEpoch.AddDays(1);
        var used = captured.AddHours(2);

        var entry = ClipboardEntry.Restore(
            id,
            ClipContentType.Image,
            new ContentHash("h"),
            "preview",
            image,
            sizeInBytes: 42,
            source,
            captured,
            used,
            isPinned: true);

        Assert.Equal(id, entry.Id);
        Assert.Equal(ClipContentType.Image, entry.ContentType);
        Assert.Equal(new ContentHash("h"), entry.Hash);
        Assert.Equal("preview", entry.Preview);
        Assert.Equal(image, entry.Image);
        Assert.Equal(42, entry.SizeInBytes);
        Assert.Equal(source, entry.Source);
        Assert.Equal(captured, entry.CapturedAt);
        Assert.Equal(used, entry.LastUsedAt);
        Assert.True(entry.IsPinned);
    }

    [Fact]
    public void Equality_is_reflexive_symmetric_and_transitive()
    {
        var id = EntryId.New();
        var a = Restore(id);
        var b = Restore(id);
        var c = Restore(id);

        Assert.Equal(a, a);
        Assert.True(a.Equals(b) && b.Equals(a));
        Assert.True(a.Equals(b) && b.Equals(c) && a.Equals(c));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Is_not_equal_to_null_or_other_types()
    {
        var entry = Restore(EntryId.New());

        Assert.False(entry.Equals(null));
        Assert.False(entry.Equals("not an entry"));
    }

    private static ClipboardEntry Restore(EntryId id) => ClipboardEntry.Restore(
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
}
