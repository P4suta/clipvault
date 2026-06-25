using System.Globalization;
using System.Text;
using ClipVault.Application.Insights;

namespace ClipVault.Application.Tests;

/// <summary>Deterministic property-style invariants (fixed seeds) for the insight transforms.</summary>
public class PropertyBasedExtraTests
{
    [Theory]
    [MemberData(nameof(Seeds))]
    public void Url_strip_is_idempotent(int seed)
    {
        var rng = new Random(11000 + seed);
        var url = RandomTrackingUrl(rng);

        Assert.True(UrlTrackingStripper.TryStrip(url, out var once));

        // The cleaned URL has no tracking parameters left, so a second pass removes nothing.
        Assert.False(UrlTrackingStripper.TryStrip(once, out _));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Json_minify_is_stable(int seed)
    {
        var rng = new Random(12000 + seed);
        var json = RandomJson(rng);

        Assert.True(JsonReformatter.TryFormat(json, indented: false, out var once));
        Assert.True(JsonReformatter.TryFormat(once, indented: false, out var twice));
        Assert.Equal(once, twice);
    }

    public static IEnumerable<object[]> Seeds()
    {
        for (var i = 0; i < 30; i++)
        {
            yield return [i];
        }
    }

    private static string RandomTrackingUrl(Random rng)
    {
        var trackers = new[] { "utm_source=a", "fbclid=b", "gclid=c", "mc_eid=d" };
        var functional = new[] { "id=1", "q=test", "page=2" };

        var parts = new List<string>();
        var trackerCount = rng.Next(1, trackers.Length + 1);
        for (var i = 0; i < trackerCount; i++)
        {
            parts.Add(trackers[rng.Next(trackers.Length)]);
        }

        parts.Add(functional[rng.Next(functional.Length)]);
        for (var i = parts.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (parts[i], parts[j]) = (parts[j], parts[i]);
        }

        return "https://example.com/?" + string.Join('&', parts);
    }

    private static string RandomJson(Random rng)
    {
        var inv = CultureInfo.InvariantCulture;
        var builder = new StringBuilder("{");
        var count = rng.Next(1, 6);
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            var value = rng.Next(2) == 0
                ? rng.Next(-1000, 1000).ToString(inv)
                : "\"v" + rng.Next(100).ToString(inv) + "\"";
            builder.Append(inv, $"\"k{i}\":").Append(value);
        }

        builder.Append('}');
        return builder.ToString();
    }
}
