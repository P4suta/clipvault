using ClipVault.Application.Capture.Classifiers;

namespace ClipVault.Application.Tests;

/// <summary>Luhn and digit-count boundary behavior of the credit-card classifier (exercised via Classify).</summary>
public class CreditCardLuhnTests
{
    [Fact]
    public void All_zeros_pass_luhn_and_are_masked()
    {
        // Thirteen zeros: the Luhn sum is 0, which is divisible by 10.
        var result = Classify(new string('0', 13));

        Assert.Equal(ClassificationAction.Mask, result.Action);
        Assert.Equal(new string('•', 9) + "0000", result.MaskedText);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(20)]
    public void Digit_count_outside_13_to_19_is_not_masked(int count) =>
        Assert.Equal(ClassificationAction.Allow, Classify(LuhnComplete(new string('1', count - 1))).Action);

    [Fact]
    public void Masks_two_adjacent_cards_in_one_text()
    {
        var a = LuhnComplete("400000000000");
        var b = LuhnComplete("510000000000");

        var result = Classify($"{a} {b}");

        Assert.Equal(ClassificationAction.Mask, result.Action);
        Assert.DoesNotContain(a, result.MaskedText!, StringComparison.Ordinal);
        Assert.DoesNotContain(b, result.MaskedText!, StringComparison.Ordinal);
    }

    private static ClassificationResult Classify(string text) => new CreditCardClassifier().Classify(text);

    // Appends the Luhn check digit so the result always passes the checksum.
    private static string LuhnComplete(string digitsWithoutCheck)
    {
        var sum = 0;
        var doubled = true;
        for (var i = digitsWithoutCheck.Length - 1; i >= 0; i--)
        {
            var n = digitsWithoutCheck[i] - '0';
            if (doubled)
            {
                n *= 2;
                if (n > 9)
                {
                    n -= 9;
                }
            }

            sum += n;
            doubled = !doubled;
        }

        return digitsWithoutCheck + ((10 - (sum % 10)) % 10);
    }
}
