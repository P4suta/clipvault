using ClipVault.Application.Insights;

namespace ClipVault.Application.Tests;

public class UrlTrackingStripperTests
{
    [Fact]
    public void Strips_utm_and_known_click_ids_preserving_functional_params()
    {
        var ok = UrlTrackingStripper.TryStrip(
            "https://example.com/page?utm_source=x&id=42&fbclid=abc&gclid=def&q=hello",
            out var cleaned);

        Assert.True(ok);
        Assert.Equal("https://example.com/page?id=42&q=hello", cleaned);
    }

    [Fact]
    public void Preserves_the_fragment()
    {
        var ok = UrlTrackingStripper.TryStrip("https://example.com/p?utm_medium=x&id=1#sec", out var cleaned);

        Assert.True(ok);
        Assert.Equal("https://example.com/p?id=1#sec", cleaned);
    }

    [Fact]
    public void Drops_the_query_entirely_when_only_tracking_params_remain()
    {
        var ok = UrlTrackingStripper.TryStrip("https://example.com/p?utm_source=x&fbclid=y", out var cleaned);

        Assert.True(ok);
        Assert.Equal("https://example.com/p", cleaned);
    }

    [Theory]
    [InlineData("https://example.com/page?id=42")] // no tracking params
    [InlineData("https://example.com/page")] // no query
    [InlineData("not a url")]
    [InlineData("ftp://example.com/x?utm_source=y")] // non-http scheme
    [InlineData("")]
    public void Returns_false_when_there_is_nothing_to_strip(string input)
    {
        Assert.False(UrlTrackingStripper.TryStrip(input, out var cleaned));
        Assert.Null(cleaned);
    }

    [Theory]
    [InlineData("https://e.com/p?mtm_campaign=x&id=1", "https://e.com/p?id=1")] // Matomo prefix
    [InlineData("https://e.com/p?pk_source=x&id=1", "https://e.com/p?id=1")] // Piwik prefix
    [InlineData("https://e.com/p?hsa_acc=1&id=1", "https://e.com/p?id=1")] // HubSpot Ads prefix
    [InlineData("https://e.com/p?mkt_tok=z&id=1", "https://e.com/p?id=1")] // Marketo
    [InlineData("https://e.com/p?igshid=z&id=1", "https://e.com/p?id=1")] // Instagram
    [InlineData("https://e.com/p?gad_source=1&srsltid=z&id=1", "https://e.com/p?id=1")] // Google Ads/Shopping
    [InlineData("https://e.com/p?mibextid=z&id=1", "https://e.com/p?id=1")] // Facebook mobile share
    public void Strips_expanded_browser_grade_trackers(string input, string expected)
    {
        Assert.True(UrlTrackingStripper.TryStrip(input, out var cleaned));
        Assert.Equal(expected, cleaned);
    }
}
