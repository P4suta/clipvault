using System.Text.RegularExpressions;

namespace ClipVault.Application.Capture.Classifiers;

/// <summary>
/// Detects API keys and tokens with known vendor prefixes and rejects the capture.
/// Anchoring on the prefix keeps false positives very low.
/// </summary>
public sealed partial class ApiKeyClassifier : IClipboardContentClassifier
{
    /// <inheritdoc/>
    public string Name => "ApiKey";

    /// <inheritdoc/>
    public ClassificationResult Classify(string text) =>
        KnownKeyRegex().IsMatch(text) ? ClassificationResult.Reject : ClassificationResult.Allow;

    // OpenAI (sk-) / GitHub (ghp_ etc.) / AWS (AKIA) / Slack (xox?-) / Google (AIza) / Stripe (sk_live etc.).
    [GeneratedRegex(
        @"(sk-[A-Za-z0-9]{20,}|gh[pousr]_[A-Za-z0-9]{20,}|AKIA[0-9A-Z]{16}|xox[baprs]-[A-Za-z0-9-]{10,}|AIza[0-9A-Za-z_\-]{35}|sk_(live|test)_[A-Za-z0-9]{16,})",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex KnownKeyRegex();
}
