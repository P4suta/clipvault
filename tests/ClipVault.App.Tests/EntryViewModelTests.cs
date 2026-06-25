using ClipVault.Application.Insights;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;
using ClipVaultApp.ViewModels;

namespace ClipVault.App.Tests;

public class EntryViewModelTests
{
    [Fact]
    public void Preview_for_image_entries_combines_the_localized_label_and_dimensions()
    {
        var vm = new EntryViewModel(ImageEntry(width: 1920, height: 1080, preview: "1920×1080"), ContentKind.Image, "画像");

        Assert.Equal("画像 1920×1080", vm.Preview);
    }

    [Fact]
    public void Preview_for_image_entries_recomputes_from_dimensions_not_the_stored_text()
    {
        // Legacy entries stored a localized label at capture time; the display recomputes from the dimensions.
        var vm = new EntryViewModel(ImageEntry(width: 800, height: 600, preview: "画像 640×480"), ContentKind.Image, "Image");

        Assert.Equal("Image 800×600", vm.Preview);
    }

    [Fact]
    public void Preview_for_text_entries_uses_the_stored_preview()
    {
        var vm = new EntryViewModel(TextEntry("hello world"), ContentKind.Text, "Text");

        Assert.Equal("hello world", vm.Preview);
    }

    [Theory]
    [InlineData(ContentKind.Url, true)]
    [InlineData(ContentKind.Email, true)]
    [InlineData(ContentKind.Json, true)]
    [InlineData(ContentKind.Color, true)]
    [InlineData(ContentKind.Number, true)]
    [InlineData(ContentKind.Text, false)]
    [InlineData(ContentKind.Image, false)]
    public void Has_badge_only_for_specific_kinds(ContentKind kind, bool expected) =>
        Assert.Equal(expected, new EntryViewModel(TextEntry("x"), kind, "L").HasBadge);

    [Fact]
    public async Task Ensure_thumbnail_is_a_no_op_for_text_entries()
    {
        var vm = new EntryViewModel(TextEntry("x"), ContentKind.Text, "L");

        await vm.EnsureThumbnailAsync();

        Assert.Null(vm.Thumbnail);
    }

    [Fact]
    public async Task Ensure_thumbnail_is_a_no_op_when_the_thumbnail_is_empty()
    {
        var entry = ClipboardEntry.Create(
            ClipContentType.Image,
            new ContentHash("e"),
            "img",
            new ImagePreview([], 1, 1),
            sizeInBytes: 1,
            new SourceApplication("paint", null, null),
            capturedAt: DateTimeOffset.UnixEpoch);
        var vm = new EntryViewModel(entry, ContentKind.Image, "L");

        await vm.EnsureThumbnailAsync();

        Assert.Null(vm.Thumbnail);
    }

    private static ClipboardEntry ImageEntry(int width, int height, string preview) =>
        ClipboardEntry.Create(
            ClipContentType.Image,
            new ContentHash(preview),
            preview,
            new ImagePreview([1], width, height),
            sizeInBytes: 1,
            new SourceApplication("paint", null, null),
            capturedAt: DateTimeOffset.UnixEpoch);

    private static ClipboardEntry TextEntry(string preview) =>
        ClipboardEntry.Create(
            ClipContentType.Text,
            new ContentHash(preview),
            preview,
            image: null,
            sizeInBytes: 1,
            new SourceApplication("chrome", null, null),
            capturedAt: DateTimeOffset.UnixEpoch);
}
