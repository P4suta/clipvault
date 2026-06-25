using ClipVault.Application.Insights;

namespace ClipVault.Application.Tests;

/// <summary>Edge cases for the heuristic content-kind detection (grounded in the actual regexes / heuristic).</summary>
public class ContentInsightServiceEdgeTests
{
    [Theory]
    [InlineData("https://", ContentKind.Text)]
    [InlineData("http://a", ContentKind.Url)]
    [InlineData("https://example.com/a b", ContentKind.Text)]
    [InlineData("user+tag@sub.domain.co.uk", ContentKind.Email)]
    [InlineData("@no-local.com", ContentKind.Text)]
    [InlineData("no-at-sign.com", ContentKind.Text)]
    [InlineData("rgb(256, 999, 0)", ContentKind.Color)]
    [InlineData("#abcd", ContentKind.Text)]
    [InlineData("rgba(0,0,0,1.5)", ContentKind.Text)]
    [InlineData("rgba(0,0,0,1)", ContentKind.Color)]
    [InlineData("{", ContentKind.Text)]
    [InlineData("{123}", ContentKind.Json)]
    [InlineData("[x", ContentKind.Text)]
    [InlineData("{\"k\":", ContentKind.Json)]
    [InlineData("1,23", ContentKind.Text)]
    [InlineData("+1000000", ContentKind.Number)]
    public void ClassifyText_handles_edges(string preview, ContentKind expected) =>
        Assert.Equal(expected, ContentInsightService.ClassifyText(preview));

    [Fact]
    public void ClassifyText_treats_whitespace_only_as_text() =>
        Assert.Equal(ContentKind.Text, ContentInsightService.ClassifyText("   \t  "));

    [Fact]
    public void Classify_throws_on_a_null_entry() =>
        Assert.Throws<ArgumentNullException>(() => ContentInsightService.Classify(null!));
}
