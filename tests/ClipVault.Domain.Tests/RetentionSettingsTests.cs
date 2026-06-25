using ClipVault.Domain.Policies;

namespace ClipVault.Domain.Tests;

public class RetentionSettingsTests
{
    [Fact]
    public void Defaults_match_the_documented_values()
    {
        var settings = RetentionSettings.Default;

        Assert.Equal(TimeSpan.FromDays(30), settings.MaxAge);
        Assert.Equal(500, settings.MaxEntries);
        Assert.Equal(256L * 1024 * 1024, settings.MaxTotalBytes);
    }

    [Fact]
    public void With_overrides_only_the_named_member()
    {
        var settings = RetentionSettings.Default with { MaxEntries = 10 };

        Assert.Equal(10, settings.MaxEntries);
        Assert.Equal(TimeSpan.FromDays(30), settings.MaxAge);
        Assert.Equal(256L * 1024 * 1024, settings.MaxTotalBytes);
    }
}
