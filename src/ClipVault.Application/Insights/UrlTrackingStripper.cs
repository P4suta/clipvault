using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace ClipVault.Application.Insights;

/// <summary>
/// Removes well-known tracking parameters (utm_*, fbclid, gclid, ...) from a single http(s) URL.
/// The list is browser-grade (modelled on the common entries in Firefox query-stripping, Brave, and
/// ClearURLs) but deliberately conservative: only keys that are globally safe to drop are included, so
/// functional query parameters and the fragment are always preserved.
/// </summary>
public static class UrlTrackingStripper
{
    // Whole families identified by a stable prefix. utm_* (analytics), pk_/mtm_/matomo_ (Matomo/Piwik),
    // hsa_ (HubSpot Ads), vero_ (Vero), oly_ (Olytics), _branch_ (Branch.io).
    private static readonly string[] TrackingPrefixes =
    [
        "utm_", "pk_", "mtm_", "matomo_", "hsa_", "vero_", "oly_", "_branch_",
    ];

    // Exact tracker keys that are domain-independent and safe to remove. Ambiguous keys that double as
    // functional parameters on some sites (cid, ref, si, spm, scm, ...) are intentionally excluded.
    private static readonly FrozenSet<string> TrackingKeys = new[]
    {
        // Google (Ads / Analytics / Shopping).
        "gclid", "gclsrc", "gad_source", "dclid", "gbraid", "wbraid", "srsltid", "_ga", "_gl",

        // Meta / Facebook / Instagram.
        "fbclid", "fb_action_ids", "fb_action_types", "fb_source", "fb_ref", "mibextid", "igshid", "igsh",

        // Microsoft / Bing.
        "msclkid",

        // Yandex / Twitter(X) / TikTok / Reddit / LinkedIn / Pinterest / Impact.
        "yclid", "ysclid", "twclid", "ttclid", "rdt_cid", "li_fat_id", "epik", "irclickid", "wickedid",

        // Email / marketing automation (Mailchimp, HubSpot, Marketo, MailerLite, Adobe).
        "mc_eid", "mc_cid", "mkt_tok", "_hsenc", "_hsmi", "__hssc", "__hstc", "__hsfp", "hsCtaTracking",
        "ml_subscriber", "ml_subscriber_hash", "s_kwcid", "_openstat",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Attempts to strip tracking parameters from the input URL.</summary>
    /// <param name="input">The candidate URL text.</param>
    /// <param name="cleaned">The cleaned URL when something was removed; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the input is an http(s) URL and at least one tracking parameter was removed.</returns>
    public static bool TryStrip(string input, [NotNullWhen(true)] out string? cleaned)
    {
        cleaned = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            return false;
        }

        if (uri.Query.Length <= 1)
        {
            return false; // No query (or just '?').
        }

        var pairs = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>(pairs.Length);
        var removed = false;

        foreach (var pair in pairs)
        {
            var key = pair.Split('=', 2)[0];
            if (IsTracking(key))
            {
                removed = true;
            }
            else
            {
                kept.Add(pair);
            }
        }

        if (!removed)
        {
            return false;
        }

        var left = uri.GetLeftPart(UriPartial.Path);
        cleaned = kept.Count > 0
            ? string.Concat(left, "?", string.Join('&', kept), uri.Fragment)
            : string.Concat(left, uri.Fragment);
        return true;
    }

    private static bool IsTracking(string key) =>
        TrackingPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ||
        TrackingKeys.Contains(key);
}
