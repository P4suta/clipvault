using ClipVault.Application.History;
using ClipVault.Application.Insights;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;
using NSubstitute;

namespace ClipVault.Application.Tests;

public class HistoryQueryServiceTests
{
    private static readonly ContentKind[] ExpectedKinds = [ContentKind.Text, ContentKind.Url, ContentKind.Image];
    private static readonly string[] ExpectedApps = ["chrome", "paint"];

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

    [Fact]
    public async Task Filters_by_source_app_case_insensitively()
    {
        var service = Build(Text("a", "Chrome"), Text("b", "notepad"));

        var result = await service.QueryAsync(sourceApp: "chrome");

        Assert.Equal("a", Assert.Single(result).Preview);
    }

    [Fact]
    public async Task GetFacets_lists_distinct_kinds_and_apps_that_exist()
    {
        var service = Build(
            Text("https://example.com", "chrome"),
            Text("plain text", "chrome"),
            Image("an image", "paint"));

        var facets = await service.GetFacetsAsync();

        // Kinds are ordered by the enum; apps are distinct and ordered.
        Assert.Equal(ExpectedKinds, facets.Kinds);
        Assert.Equal(ExpectedApps, facets.SourceApps);
    }

    [Fact]
    public async Task QueryPage_accumulates_matches_across_batches_until_the_page_is_filled()
    {
        var repo = Substitute.For<IClipboardHistoryRepository>();
        var c1 = new HistoryCursor(false, DateTimeOffset.UnixEpoch, EntryId.New());
        var c2 = new HistoryCursor(false, DateTimeOffset.UnixEpoch.AddMinutes(1), EntryId.New());
        repo.GetPageAsync(Arg.Any<HistoryCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                new HistoryPage([Text("match a", "x"), Text("skip", "y")], c1),
                new HistoryPage([Text("match b", "x")], c2),
                new HistoryPage([Text("match c", "x")], null));
        var service = new HistoryQueryService(repo);

        var page = await service.QueryPageAsync(new HistoryFilter(Search: "match"), after: null, pageSize: 2);

        // Batch 1 yields one match (<2), so batch 2 is scanned to reach 2 — and the third batch is never fetched.
        string[] expected = ["match a", "match b"];
        Assert.Equal(expected, page.Entries.Select(e => e.Preview));
        Assert.Equal(c2, page.NextCursor);
        await repo.Received(2).GetPageAsync(Arg.Any<HistoryCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryPage_returns_a_null_cursor_when_the_source_is_exhausted()
    {
        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.GetPageAsync(Arg.Any<HistoryCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new HistoryPage([Text("only", "x")], null));
        var service = new HistoryQueryService(repo);

        var page = await service.QueryPageAsync(new HistoryFilter(), after: null, pageSize: 50);

        Assert.Equal("only", Assert.Single(page.Entries).Preview);
        Assert.Null(page.NextCursor);
    }

    private static HistoryQueryService Build(params ClipboardEntry[] entries)
    {
        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(entries);

        // Facets stream through GetPageAsync; return the whole set as a single, final page.
        repo.GetPageAsync(Arg.Any<HistoryCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new HistoryPage(entries, null));
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
