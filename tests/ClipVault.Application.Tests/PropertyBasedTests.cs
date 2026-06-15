using System.Text;
using ClipVault.Application.Capture;
using ClipVault.Application.Capture.Classifiers;

namespace ClipVault.Application.Tests;

/// <summary>
/// Deterministic property-style tests. Each case is generated from a fixed seed (via [MemberData]) so runs are
/// reproducible without taking a dependency on a property-testing library.
/// </summary>
public class PropertyBasedTests
{
    [Theory]
    [MemberData(nameof(Seeds))]
    public void CreditCard_masks_every_valid_number_and_keeps_only_last_four(int seed)
    {
        var rng = new Random(7000 + seed);
        var number = LuhnComplete(RandomDigits(rng, rng.Next(12, 19))); // 13..19 digits after the check digit

        var result = new CreditCardClassifier().Classify(number);

        Assert.Equal(ClassificationAction.Mask, result.Action);
        Assert.EndsWith(number[^4..], result.MaskedText!, StringComparison.Ordinal);
        Assert.DoesNotContain(number, result.MaskedText!, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void CreditCard_allows_numbers_that_fail_luhn(int seed)
    {
        var rng = new Random(9000 + seed);
        var valid = LuhnComplete(RandomDigits(rng, rng.Next(12, 19)));
        var broken = BreakLuhn(valid);

        Assert.Equal(ClassificationAction.Allow, new CreditCardClassifier().Classify(broken).Action);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TextPreview_create_is_idempotent_and_bounded(int seed)
    {
        var rng = new Random(8000 + seed);
        var text = RandomWhitespaceyText(rng);

        var once = TextPreview.Create(text);

        Assert.Equal(once, TextPreview.Create(once));
        Assert.True(once.Length <= TextPreview.DefaultMaxLength + 1);
    }

    public static IEnumerable<object[]> Seeds()
    {
        for (var i = 0; i < 30; i++)
        {
            yield return [i];
        }
    }

    private static string RandomDigits(Random rng, int count)
    {
        var chars = new char[count];
        for (var i = 0; i < count; i++)
        {
            chars[i] = (char)('0' + rng.Next(0, 10));
        }

        return new string(chars);
    }

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

    // Changes the last digit to a different value, which always breaks the Luhn checksum.
    private static string BreakLuhn(string valid)
    {
        var last = valid[^1];
        var replacement = last == '0' ? '1' : (char)(last - 1);
        return string.Concat(valid.AsSpan(0, valid.Length - 1), replacement.ToString());
    }

    private static string RandomWhitespaceyText(Random rng)
    {
        var whitespace = new[] { " ", "  ", "\t", "\n", "   ", " \t ", "\r\n" };
        var builder = new StringBuilder();
        var words = rng.Next(1, 60);
        for (var w = 0; w < words; w++)
        {
            var length = rng.Next(1, 8);
            for (var c = 0; c < length; c++)
            {
                builder.Append((char)('a' + rng.Next(0, 26)));
            }

            builder.Append(whitespace[rng.Next(0, whitespace.Length)]);
        }

        return builder.ToString();
    }
}
