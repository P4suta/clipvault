using System.Text.RegularExpressions;

namespace ClipVault.Application.Capture.Classifiers;

/// <summary>Detects a PEM-format private key block and rejects it.</summary>
public sealed partial class PemPrivateKeyClassifier : IClipboardContentClassifier
{
    /// <inheritdoc/>
    public string Name => "PemPrivateKey";

    /// <inheritdoc/>
    public ClassificationResult Classify(string text) =>
        PemRegex().IsMatch(text) ? ClassificationResult.Reject : ClassificationResult.Allow;

    [GeneratedRegex(
        @"-----BEGIN (?:[A-Z0-9]+ )?PRIVATE KEY-----",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex PemRegex();
}
