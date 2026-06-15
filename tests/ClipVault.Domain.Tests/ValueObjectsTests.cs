using ClipVault.Domain.ValueObjects;

namespace ClipVault.Domain.Tests;

public class ValueObjectsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1024)]
    public void ClipContent_size_matches_payload_length(int length)
    {
        Assert.Equal(length, new ClipContent(ClipContentType.Text, new byte[length]).SizeInBytes);
    }

    [Fact]
    public void ClipContent_Dispose_zeroes_the_payload()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var content = new ClipContent(ClipContentType.Text, payload);

        content.Dispose();

        Assert.All(payload, b => Assert.Equal(0, b));
    }

    [Fact]
    public void SourceApplication_Unknown_is_the_named_sentinel()
    {
        Assert.Equal("unknown", SourceApplication.Unknown.ProcessName);
        Assert.Null(SourceApplication.Unknown.WindowTitle);
        Assert.Null(SourceApplication.Unknown.ExecutablePath);
    }

    [Fact]
    public void SourceApplication_equality_is_by_value()
    {
        Assert.Equal(new SourceApplication("p", null, null), new SourceApplication("p", null, null));
        Assert.NotEqual(new SourceApplication("p", null, null), new SourceApplication("q", null, null));
    }

    [Theory]
    [InlineData(true, null, true)] // ExcludeFromHistory forbids capture.
    [InlineData(false, false, true)] // CanIncludeInHistory == false forbids capture.
    [InlineData(false, true, false)] // Explicitly allowed.
    [InlineData(false, null, false)] // No signal.
    [InlineData(true, true, true)] // ExcludeFromHistory dominates.
    public void PrivacySignals_ForbidsCapture_truth_table(bool exclude, bool? canInclude, bool expected)
    {
        Assert.Equal(expected, new ClipboardPrivacySignals(exclude, canInclude).ForbidsCapture);
    }

    [Fact]
    public void PrivacySignals_None_allows_capture()
    {
        Assert.False(ClipboardPrivacySignals.None.ForbidsCapture);
    }

    [Fact]
    public void EntryId_New_produces_unique_values()
    {
        Assert.NotEqual(EntryId.New(), EntryId.New());
    }

    [Fact]
    public void EntryId_equality_is_by_value()
    {
        var guid = Guid.NewGuid();
        Assert.Equal(new EntryId(guid), new EntryId(guid));
    }

    [Fact]
    public void ImagePreview_equality_uses_all_fields()
    {
        var bytes = new byte[] { 1, 2, 3 };

        Assert.Equal(new ImagePreview(bytes, 4, 5), new ImagePreview(bytes, 4, 5));
        Assert.NotEqual(new ImagePreview(bytes, 4, 5), new ImagePreview(bytes, 4, 6));
    }
}
