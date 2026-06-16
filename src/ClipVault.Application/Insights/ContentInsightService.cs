using System.Text.RegularExpressions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Insights;

/// <summary>
/// Classifies a clipboard entry into a <see cref="ContentKind"/> from its preview, for at-a-glance
/// badges, filtering, and quick actions. Detection is heuristic and runs over the already-decrypted
/// <see cref="ClipboardEntry.Preview"/>, so it needs no extra storage or decryption. Detection is
/// anchored on the whole (whitespace-collapsed) preview to keep false positives low.
/// </summary>
public static partial class ContentInsightService
{
    /// <summary>Classifies the given entry.</summary>
    /// <param name="entry">The entry to classify.</param>
    /// <returns>The detected content kind.</returns>
    public static ContentKind Classify(ClipboardEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return entry.ContentType == ClipContentType.Image
            ? ContentKind.Image
            : ClassifyText(entry.Preview);
    }

    /// <summary>Classifies a plain-text preview into a kind (first match wins).</summary>
    /// <param name="preview">The preview text to classify.</param>
    /// <returns>The detected content kind.</returns>
    public static ContentKind ClassifyText(string preview)
    {
        if (string.IsNullOrWhiteSpace(preview))
        {
            return ContentKind.Text;
        }

        var text = preview.Trim();

        if (UrlRegex().IsMatch(text))
        {
            return ContentKind.Url;
        }

        if (EmailRegex().IsMatch(text))
        {
            return ContentKind.Email;
        }

        if (ColorRegex().IsMatch(text))
        {
            return ContentKind.Color;
        }

        if (LooksLikeJson(text))
        {
            return ContentKind.Json;
        }

        return NumberRegex().IsMatch(text) ? ContentKind.Number : ContentKind.Text;
    }

    // The preview may be truncated, so a full JSON parse would usually fail; use a cheap structural
    // heuristic: it must open with an object/array bracket and either close cleanly or carry a key marker.
    private static bool LooksLikeJson(string text)
    {
        if (text.Length < 2 || text[0] is not ('{' or '['))
        {
            return false;
        }

        return text[^1] is '}' or ']' || text.Contains("\":", StringComparison.Ordinal);
    }

    [GeneratedRegex(
        @"^https?://\S+$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailRegex();

    // #rgb / #rrggbb / #rrggbbaa, or rgb()/rgba() with an optional alpha component.
    [GeneratedRegex(
        @"^(#([0-9a-f]{3}|[0-9a-f]{6}|[0-9a-f]{8})|rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*(,\s*(0|1|0?\.\d+)\s*)?\))$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex ColorRegex();

    // A grouped-thousands number (1,234,567.89) or a plain number (-42, 3.14).
    [GeneratedRegex(
        @"^[+-]?\d{1,3}(,\d{3})+(\.\d+)?$|^[+-]?\d+(\.\d+)?$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex NumberRegex();
}
