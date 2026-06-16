using ClipVault.Application.Insights;

namespace ClipVault.Application.Tests;

public class JsonReformatterTests
{
    [Fact]
    public void Pretty_prints_valid_json()
    {
        var ok = JsonReformatter.TryFormat("{\"a\":1}", indented: true, out var result);

        Assert.True(ok);

        // The compact form would be "a":1; the space after the colon proves it was pretty-printed.
        Assert.Contains("\"a\": 1", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Minifies_valid_json()
    {
        var ok = JsonReformatter.TryFormat("{\n  \"a\": 1,\n  \"b\": [1, 2]\n}", indented: false, out var result);

        Assert.True(ok);
        Assert.Equal("{\"a\":1,\"b\":[1,2]}", result);
    }

    [Fact]
    public void Keeps_non_ascii_unescaped()
    {
        var ok = JsonReformatter.TryFormat("{\"name\":\"日本語\"}", indented: false, out var result);

        Assert.True(ok);
        Assert.Equal("{\"name\":\"日本語\"}", result);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{ unterminated")]
    [InlineData("")]
    public void Returns_false_for_invalid_json(string input)
    {
        Assert.False(JsonReformatter.TryFormat(input, indented: true, out var result));
        Assert.Null(result);
    }
}
