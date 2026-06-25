using ClipVault.Application.Insights;

namespace ClipVault.Application.Tests;

/// <summary>Edge cases for JSON reformatting (depth, numbers, unicode, minification).</summary>
public class JsonReformatterEdgeTests
{
    [Fact]
    public void Parses_very_large_and_small_numbers()
    {
        Assert.True(JsonReformatter.TryFormat(
            "{\"big\":1.7976931348623157e+308,\"tiny\":1e-300}", indented: false, out var result));
        Assert.Contains("\"big\":", result, StringComparison.Ordinal);
        Assert.Contains("\"tiny\":", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_excessively_deep_nesting()
    {
        var deep = new string('[', 200) + new string(']', 200);
        Assert.False(JsonReformatter.TryFormat(deep, indented: true, out _));
    }

    [Fact]
    public void Decodes_unicode_escapes()
    {
        Assert.True(JsonReformatter.TryFormat("{\"k\":\"\\u0041\"}", indented: false, out var result));
        Assert.Contains("A", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_non_ascii_keys_and_values_unescaped()
    {
        Assert.True(JsonReformatter.TryFormat("{\"鍵\":\"値\"}", indented: false, out var result));
        Assert.Contains("鍵", result, StringComparison.Ordinal);
        Assert.Contains("値", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Minifies_removing_insignificant_whitespace()
    {
        Assert.True(JsonReformatter.TryFormat("{\n  \"a\" : 1\n}", indented: false, out var result));
        Assert.Equal("{\"a\":1}", result);
    }
}
