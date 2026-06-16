using ClipVault.Application.Capture;

namespace ClipVault.Application.Tests;

public class TextPreviewTests
{
    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("  hello  ", "hello")] // leading/trailing whitespace trimmed
    [InlineData("a\t\nb", "a b")] // tabs and newlines collapse to a single space
    [InlineData("a    b     c", "a b c")] // runs of spaces collapse
    [InlineData("", "")]
    [InlineData("   ", "")] // all whitespace becomes empty
    public void Create_collapses_whitespace(string input, string expected) => Assert.Equal(expected, TextPreview.Create(input));

    [Fact]
    public void Create_does_not_truncate_at_exactly_the_max_length()
    {
        var preview = TextPreview.Create(new string('a', 160));

        Assert.Equal(160, preview.Length);
        Assert.DoesNotContain("…", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_truncates_and_appends_an_ellipsis_when_too_long()
    {
        var preview = TextPreview.Create(new string('a', 161));

        Assert.Equal(161, preview.Length); // 160 characters plus the ellipsis
        Assert.EndsWith("…", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_honours_a_custom_max_length() => Assert.Equal("ab…", TextPreview.Create("abcdef", maxLength: 2));
}
