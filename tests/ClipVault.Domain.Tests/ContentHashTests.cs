using ClipVault.Domain.ValueObjects;

namespace ClipVault.Domain.Tests;

public class ContentHashTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_null_or_whitespace(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ContentHash(value!));
    }

    [Fact]
    public void Constructor_keeps_the_value()
    {
        var hash = new ContentHash("ABC123");

        Assert.Equal("ABC123", hash.Value);
        Assert.Equal("ABC123", hash.ToString());
    }

    [Fact]
    public void Equality_is_by_value()
    {
        Assert.Equal(new ContentHash("h"), new ContentHash("h"));
        Assert.NotEqual(new ContentHash("h"), new ContentHash("g"));
    }
}
