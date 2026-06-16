using ClipVault.Application.Insights;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Tests;

public class ContentInsightServiceTests
{
    [Theory]
    [InlineData("https://example.com", ContentKind.Url)]
    [InlineData("http://example.com/path?q=1", ContentKind.Url)]
    [InlineData("foo.bar@example.co.jp", ContentKind.Email)]
    [InlineData("#3366ff", ContentKind.Color)]
    [InlineData("#abc", ContentKind.Color)]
    [InlineData("rgb(12, 34, 56)", ContentKind.Color)]
    [InlineData("rgba(0,0,0,0.5)", ContentKind.Color)]
    [InlineData("{\"a\":1,\"b\":2}", ContentKind.Json)]
    [InlineData("[1, 2, 3]", ContentKind.Json)]
    [InlineData("42", ContentKind.Number)]
    [InlineData("-3.14", ContentKind.Number)]
    [InlineData("1,234,567", ContentKind.Number)]
    [InlineData("just some text", ContentKind.Text)]
    [InlineData("see https://example.com here", ContentKind.Text)] // not the whole preview -> plain text
    [InlineData("", ContentKind.Text)]
    public void ClassifyText_detects_kind(string preview, ContentKind expected)
    {
        Assert.Equal(expected, ContentInsightService.ClassifyText(preview));
    }

    [Fact]
    public void Classify_uses_the_image_content_type()
    {
        Assert.Equal(ContentKind.Image, ContentInsightService.Classify(ImageEntry("anything")));
    }

    [Fact]
    public void Classify_uses_the_preview_for_text_entries()
    {
        Assert.Equal(ContentKind.Url, ContentInsightService.Classify(TextEntry("https://example.com")));
    }

    private static ClipboardEntry TextEntry(string preview) =>
        ClipboardEntry.Create(
            ClipContentType.Text,
            new ContentHash(preview),
            preview,
            image: null,
            sizeInBytes: 1,
            new SourceApplication("chrome", null, null),
            capturedAt: DateTimeOffset.UnixEpoch);

    private static ClipboardEntry ImageEntry(string preview) =>
        ClipboardEntry.Create(
            ClipContentType.Image,
            new ContentHash(preview),
            preview,
            new ImagePreview([1], 1, 1),
            sizeInBytes: 1,
            new SourceApplication("paint", null, null),
            capturedAt: DateTimeOffset.UnixEpoch);
}
