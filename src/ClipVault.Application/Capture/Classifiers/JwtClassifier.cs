using System.Text.RegularExpressions;

namespace ClipVault.Application.Capture.Classifiers;

/// <summary>Detects a JWT (a base64url triplet of header.payload.signature) and rejects it.</summary>
public sealed partial class JwtClassifier : IClipboardContentClassifier
{
    /// <inheritdoc/>
    public string Name => "Jwt";

    /// <inheritdoc/>
    public ClassificationResult Classify(string text) =>
        JwtRegex().IsMatch(text) ? ClassificationResult.Reject : ClassificationResult.Allow;

    [GeneratedRegex(
        @"eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex JwtRegex();
}
