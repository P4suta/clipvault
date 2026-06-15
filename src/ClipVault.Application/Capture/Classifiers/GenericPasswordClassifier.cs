using ClipVault.Application.Abstractions;

namespace ClipVault.Application.Capture.Classifiers;

/// <summary>
/// Detects a single token that looks like a generic password and masks it (opt-in, off by default).
/// It fires only when the token contains no whitespace, is 8 to 64 characters long, contains upper- and lower-case
/// letters plus digits plus a symbol, and has high entropy. Because this area is prone to false positives, the
/// conditions are deliberately strict, and it does nothing when disabled in settings.
/// </summary>
public sealed class GenericPasswordClassifier(ISettingsService settings) : IClipboardContentClassifier
{
    /// <inheritdoc/>
    public string Name => "GenericPassword";

    /// <inheritdoc/>
    public ClassificationResult Classify(string text)
    {
        if (!settings.Current.MaskGenericPasswords)
        {
            return ClassificationResult.Allow;
        }

        var token = text.Trim();
        return LooksLikePassword(token) ? ClassificationResult.Mask("••••••••") : ClassificationResult.Allow;
    }

    private static bool LooksLikePassword(string token)
    {
        if (token.Length is < 8 or > 64 || token.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var hasUpper = token.Any(char.IsUpper);
        var hasLower = token.Any(char.IsLower);
        var hasDigit = token.Any(char.IsDigit);
        var hasSymbol = token.Any(c => !char.IsLetterOrDigit(c));
        if (!(hasUpper && hasLower && hasDigit && hasSymbol))
        {
            return false;
        }

        return ShannonEntropyBits(token) >= 3.0;
    }

    private static double ShannonEntropyBits(string token)
    {
        var counts = new Dictionary<char, int>();
        foreach (var c in token)
        {
            counts[c] = counts.GetValueOrDefault(c) + 1;
        }

        var entropy = 0.0;
        foreach (var count in counts.Values)
        {
            var p = (double)count / token.Length;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }
}
