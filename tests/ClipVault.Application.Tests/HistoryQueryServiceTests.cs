using ClipVault.Application.History;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;
using NSubstitute;

namespace ClipVault.Application.Tests;

public class HistoryQueryServiceTests
{
    [Fact]
    public async Task Returns_all_entries_when_no_filters_are_given()
    {
        var service = Build(Text("hello", "notepad"), Text("world", "chrome"));

        Assert.Equal(2, (await service.QueryAsync()).Count);
    }

    [Fact]
    public async Task Filters_by_content_type()
    {
        var service = Build(Text("a", "x"), Image("an image", "y"));

        var result = await service.QueryAsync(typeFilter: ClipContentType.Image);

        Assert.Equal(ClipContentType.Image, Assert.Single(result).ContentType);
    }

    [Fact]
    public async Task Searches_the_preview_case_insensitively()
    {
        var service = Build(Text("Hello World", "notepad"), Text("goodbye", "notepad"));

        var result = await service.QueryAsync(search: "WORLD");

        Assert.Equal("Hello World", Assert.Single(result).Preview);
    }

    [Fact]
    public async Task Searches_the_source_process_name()
    {
        var service = Build(Text("a", "KeePass"), Text("b", "notepad"));

        var result = await service.QueryAsync(search: "keepass");

        Assert.Equal("a", Assert.Single(result).Preview);
    }

    [Fact]
    public async Task Whitespace_only_search_is_treated_as_no_filter()
    {
        var service = Build(Text("a", "x"), Text("b", "y"));

        Assert.Equal(2, (await service.QueryAsync(search: "   ")).Count);
    }

    [Fact]
    public async Task Combines_type_and_search_filters()
    {
        var service = Build(Text("report draft", "word"), Image("report image", "paint"));

        var result = await service.QueryAsync(search: "report", typeFilter: ClipContentType.Text);

        Assert.Equal("report draft", Assert.Single(result).Preview);
    }

    [Fact]
    public async Task Returns_empty_when_nothing_matches()
    {
        var service = Build(Text("a", "x"));

        Assert.Empty(await service.QueryAsync(search: "nomatch"));
    }

    private static HistoryQueryService Build(params ClipboardEntry[] entries)
    {
        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(entries);
        return new HistoryQueryService(repo);
    }

    private static ClipboardEntry Text(string preview, string process) =>
        ClipboardEntry.Create(
            ClipContentType.Text,
            new ContentHash(preview + process),
            preview,
            image: null,
            sizeInBytes: 1,
            new SourceApplication(process, null, null),
            capturedAt: DateTimeOffset.UnixEpoch);

    private static ClipboardEntry Image(string preview, string process) =>
        ClipboardEntry.Create(
            ClipContentType.Image,
            new ContentHash(preview + process),
            preview,
            new ImagePreview([1], 1, 1),
            sizeInBytes: 1,
            new SourceApplication(process, null, null),
            capturedAt: DateTimeOffset.UnixEpoch);
}
