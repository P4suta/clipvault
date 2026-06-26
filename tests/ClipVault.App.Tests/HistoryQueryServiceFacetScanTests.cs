using System.Text;
using ClipVault.Application.History;
using ClipVault.Domain.Entities;
using ClipVault.Domain.Policies;
using ClipVault.Domain.ValueObjects;
using ClipVault.Infrastructure.Persistence;

namespace ClipVault.App.Tests;

public class HistoryQueryServiceFacetScanTests
{
    [Fact]
    public async Task GetFacets_scans_only_recent_history_so_very_old_apps_are_not_listed()
    {
        // A high budget so the volatile ring does not evict while we seed a large history.
        var repo = new InMemoryClipboardHistoryRepository(
            new RetentionSettings { MaxEntries = int.MaxValue, MaxTotalBytes = long.MaxValue });
        var t0 = DateTimeOffset.UnixEpoch;

        // Oldest entry uses a distinct app, then enough newer entries to push it past the facet scan window.
        await repo.AddAsync(TextEntry("ancient", "ancientapp", t0), Content());
        for (var i = 0; i < 2200; i++)
        {
            await repo.AddAsync(TextEntry($"r{i}", "recentapp", t0.AddSeconds(i + 1)), Content());
        }

        var facets = await new HistoryQueryService(repo).GetFacetsAsync();

        // The facet scan is capped to recent history, so the long-buried "ancientapp" is not offered as a filter.
        Assert.Contains("recentapp", facets.SourceApps);
        Assert.DoesNotContain("ancientapp", facets.SourceApps);
    }

    private static ClipboardEntry TextEntry(string hash, string app, DateTimeOffset at) =>
        ClipboardEntry.Create(
            ClipContentType.Text,
            new ContentHash(hash),
            hash,
            image: null,
            sizeInBytes: 1,
            new SourceApplication(app, null, null),
            capturedAt: at);

    private static ClipContent Content() => new(ClipContentType.Text, Encoding.UTF8.GetBytes("x"));
}
