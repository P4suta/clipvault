using ClipVault.Application.Capture;

namespace ClipVault.Application.Tests;

/// <summary>Edge cases for preview building (small max lengths, exotic whitespace).</summary>
public class TextPreviewEdgeTests
{
    [Theory]
    [InlineData("abc", 0, "…")]
    [InlineData("abc", 1, "a…")]
    [InlineData("abcde", 2, "ab…")]
    public void Truncates_at_small_max_lengths(string text, int max, string expected) =>
        Assert.Equal(expected, TextPreview.Create(text, max));

    [Fact]
    public void Collapses_non_breaking_space_and_tabs() =>
        Assert.Equal("a b c", TextPreview.Create("a b\tc"));

    [Fact]
    public void Whitespace_only_becomes_empty() =>
        Assert.Equal(string.Empty, TextPreview.Create("  \t \r\n "));

    [Fact]
    public void Does_not_truncate_at_exactly_the_max_length() =>
        Assert.Equal("abcd", TextPreview.Create("abcd", 4));
}
