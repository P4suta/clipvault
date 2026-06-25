using ClipVault.Application.Insights;

namespace ClipVault.Application.Tests;

/// <summary>Edge cases for tracking-parameter stripping (scheme normalization, encoding, duplicates, prefixes).</summary>
public class UrlTrackingStripperEdgeTests
{
    [Theory]
    [InlineData("HTTP://example.com/?utm_source=x&id=1", "http://example.com/?id=1")]
    [InlineData("https://example.com/?utm_source=%20a%20&id=1", "https://example.com/?id=1")]
    [InlineData("https://example.com/?utm_source=a&utm_source=b&id=1", "https://example.com/?id=1")]
    [InlineData("https://example.com/?utm_source&id=1", "https://example.com/?id=1")]
    [InlineData("https://e.com/?_branch_match=1&keep=2", "https://e.com/?keep=2")]
    public void Strips_and_reconstructs(string input, string expected)
    {
        Assert.True(UrlTrackingStripper.TryStrip(input, out var cleaned));
        Assert.Equal(expected, cleaned);
    }

    [Theory]
    [InlineData("https://example.com?")]
    [InlineData("https://example.com/?_branchy=1")]
    [InlineData("https://example.com/?id=1&page=2")]
    public void Returns_false_when_nothing_strippable(string input) =>
        Assert.False(UrlTrackingStripper.TryStrip(input, out _));
}
