using System.Text;
using System.Text.RegularExpressions;

namespace ClipVault.Application.Capture.Classifiers;

/// <summary>
/// Detects digit sequences that look like credit card numbers (and pass the Luhn check) and masks them, keeping only the last four digits.
/// Luhn plus the digit count keeps false positives down.
/// </summary>
public sealed partial class CreditCardClassifier : IClipboardContentClassifier
{
    /// <inheritdoc/>
    public string Name => "CreditCard";

    /// <inheritdoc/>
    public ClassificationResult Classify(string text)
    {
        var masked = false;
        var result = CandidateRegex().Replace(text, match =>
        {
            var digits = new string([.. match.Value.Where(char.IsDigit)]);
            if (digits.Length is < 13 or > 19 || !PassesLuhn(digits))
            {
                return match.Value;
            }

            masked = true;
            return MaskKeepingLast4(digits);
        });

        return masked ? ClassificationResult.Mask(result) : ClassificationResult.Allow;
    }

    private static string MaskKeepingLast4(string digits) =>
        string.Concat(new string('•', digits.Length - 4), digits.AsSpan(digits.Length - 4));

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9)
                {
                    n -= 9;
                }
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    // Candidate pattern that captures 13 to 19 digits, allowing spaces or hyphens between digits.
    [GeneratedRegex(@"\b\d(?:[ -]?\d){12,18}\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CandidateRegex();
}
