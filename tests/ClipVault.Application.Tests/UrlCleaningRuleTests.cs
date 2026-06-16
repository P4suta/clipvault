using System.Text;
using ClipVault.Application.Abstractions;
using ClipVault.Application.Capture.Rules;
using ClipVault.Application.Settings;

namespace ClipVault.Application.Tests;

public class UrlCleaningRuleTests
{
    [Fact]
    public void Does_nothing_when_the_setting_is_off()
    {
        var rule = new UrlCleaningRule(new InMemorySettingsService());
        var snapshot = Snapshots.Text("https://example.com/p?utm_source=x&id=1");

        var result = rule.Evaluate(snapshot);

        Assert.False(result.Rejected);
        Assert.Same(snapshot, result.Snapshot);
    }

    [Fact]
    public void Cleans_a_tracking_url_when_enabled()
    {
        var rule = new UrlCleaningRule(Enabled());
        var snapshot = Snapshots.Text("https://example.com/p?utm_source=x&id=1&fbclid=abc");

        var result = rule.Evaluate(snapshot);

        Assert.False(result.Rejected);
        Assert.Equal("https://example.com/p?id=1", Encoding.UTF8.GetString(result.Snapshot!.Payload));
        Assert.Equal("https://example.com/p?id=1", result.Snapshot.Preview); // preview reflects the cleaned URL
    }

    [Theory]
    [InlineData("https://example.com/p?id=1")] // no tracking params
    [InlineData("just some text, not a url")]
    public void Leaves_content_unchanged_when_there_is_nothing_to_strip(string text)
    {
        var rule = new UrlCleaningRule(Enabled());
        var snapshot = Snapshots.Text(text);

        var result = rule.Evaluate(snapshot);

        Assert.False(result.Rejected);
        Assert.Same(snapshot, result.Snapshot);
    }

    private static InMemorySettingsService Enabled()
    {
        var settings = new InMemorySettingsService();
        settings.Update(ClipVaultSettings.Default with { StripTrackingParameters = true });
        return settings;
    }
}
