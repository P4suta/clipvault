using ClipVault.Application.Abstractions;
using ClipVault.Application.Capture.Classifiers;
using ClipVault.Application.Settings;

namespace ClipVault.Application.Tests;

public class ClassifierTests
{
    // ---------- ApiKeyClassifier ----------
    [Theory]
    [InlineData("token sk-abcdefghijklmnopqrstuvwxyz0123")] // OpenAI
    [InlineData("ghp_abcdefghijklmnopqrst1234")] // GitHub personal
    [InlineData("gho_abcdefghijklmnopqrst1234")] // GitHub OAuth
    [InlineData("ghu_abcdefghijklmnopqrst1234")] // GitHub user-to-server
    [InlineData("ghr_abcdefghijklmnopqrst1234")] // GitHub refresh
    [InlineData("ghs_abcdefghijklmnopqrst1234")] // GitHub server-to-server
    [InlineData("aws AKIAIOSFODNN7EXAMPLE here")] // AWS access key id
    [InlineData("xoxb-1234567890-abcdefghij")] // Slack bot
    [InlineData("xoxa-1234567890-abcdefghij")] // Slack app
    [InlineData("xoxp-1234567890-abcdefghij")] // Slack user
    [InlineData("xoxr-1234567890-abcdefghij")] // Slack refresh
    [InlineData("xoxs-1234567890-abcdefghij")] // Slack workspace
    [InlineData("sk_live_abcdefghijklmnop")] // Stripe live
    [InlineData("sk_test_abcdefghijklmnop")] // Stripe test
    [InlineData("prefix sk-abcdefghijklmnopqrstuvwx suffix")] // embedded in surrounding text
    public void ApiKey_rejects_known_vendor_prefixes(string text)
    {
        Assert.Equal(ClassificationAction.Reject, new ApiKeyClassifier().Classify(text).Action);
    }

    [Theory]
    [InlineData("ただのテキストです")]
    [InlineData("sk-tooshort")] // sk- with fewer than 20 trailing chars
    [InlineData("AKIA1234567890ABCD")] // AKIA with fewer than 16 trailing chars
    [InlineData("sk_prod_abcdefghijklmnop")] // not the live/test sub-keyword
    [InlineData("https://example.com/path")]
    public void ApiKey_allows_non_keys(string text)
    {
        Assert.Equal(ClassificationAction.Allow, new ApiKeyClassifier().Classify(text).Action);
    }

    [Fact]
    public void ApiKey_respects_length_boundaries()
    {
        var classifier = new ApiKeyClassifier();

        // sk- requires at least 20 trailing characters.
        Assert.Equal(ClassificationAction.Reject, classifier.Classify("sk-" + new string('a', 20)).Action);
        Assert.Equal(ClassificationAction.Allow, classifier.Classify("sk-" + new string('a', 19)).Action);

        // AKIA requires exactly 16 trailing upper/digit characters.
        Assert.Equal(ClassificationAction.Reject, classifier.Classify("AKIA" + new string('A', 16)).Action);
        Assert.Equal(ClassificationAction.Allow, classifier.Classify("AKIA" + new string('A', 15)).Action);

        // AIza (Google) requires exactly 35 trailing characters.
        Assert.Equal(ClassificationAction.Reject, classifier.Classify("AIza" + new string('a', 35)).Action);
        Assert.Equal(ClassificationAction.Allow, classifier.Classify("AIza" + new string('a', 34)).Action);
    }

    // ---------- JwtClassifier ----------
    [Fact]
    public void Jwt_rejects_well_formed_token()
    {
        const string jwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
                           "eyJzdWIiOiIxMjM0NTY3ODkwIn0." +
                           "SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
        Assert.Equal(ClassificationAction.Reject, new JwtClassifier().Classify(jwt).Action);
    }

    [Fact]
    public void Jwt_requires_three_segments_of_sufficient_length()
    {
        var classifier = new JwtClassifier();
        var ten = new string('a', 10);
        var nine = new string('a', 9);

        // Each segment needs at least 10 base64url characters.
        Assert.Equal(ClassificationAction.Reject, classifier.Classify($"eyJ{ten}.{ten}.{ten}").Action);
        Assert.Equal(ClassificationAction.Allow, classifier.Classify($"eyJ{ten}.{nine}.{ten}").Action);

        // Two segments only, or a missing eyJ header, are not JWTs.
        Assert.Equal(ClassificationAction.Allow, classifier.Classify($"eyJ{ten}.{ten}").Action);
        Assert.Equal(ClassificationAction.Allow, classifier.Classify("not.a.jwt").Action);
    }

    // ---------- PemPrivateKeyClassifier ----------
    [Theory]
    [InlineData("-----BEGIN PRIVATE KEY-----")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----")]
    [InlineData("-----BEGIN EC PRIVATE KEY-----")]
    [InlineData("-----BEGIN OPENSSH PRIVATE KEY-----")]
    [InlineData("-----BEGIN ENCRYPTED PRIVATE KEY-----")]
    [InlineData("preamble\n-----BEGIN RSA PRIVATE KEY-----\nMIIE...")]
    public void Pem_rejects_begin_private_key_blocks(string text)
    {
        Assert.Equal(ClassificationAction.Reject, new PemPrivateKeyClassifier().Classify(text).Action);
    }

    [Theory]
    [InlineData("-----END PRIVATE KEY-----")] // end marker must not trigger
    [InlineData("-----begin private key-----")] // case-sensitive
    [InlineData("-----BEGIN PUBLIC KEY-----")]
    [InlineData("-----BEGIN CERTIFICATE-----")]
    [InlineData("nothing sensitive here")]
    public void Pem_allows_non_private_key_text(string text)
    {
        Assert.Equal(ClassificationAction.Allow, new PemPrivateKeyClassifier().Classify(text).Action);
    }

    // ---------- CreditCardClassifier ----------
    [Theory]
    [InlineData("支払いは 4111 1111 1111 1111 で。")] // 16-digit Visa, spaced
    [InlineData("4111111111111111")] // no separators
    [InlineData("4111-1111-1111-1111")] // hyphenated
    [InlineData("378282246310005")] // 15-digit Amex
    [InlineData("4242424242424242")] // 16-digit, valid Luhn
    public void CreditCard_masks_valid_luhn(string text)
    {
        Assert.Equal(ClassificationAction.Mask, new CreditCardClassifier().Classify(text).Action);
    }

    [Theory]
    [InlineData("番号 4111 1111 1111 1112 は無効。")] // fails Luhn
    [InlineData("4242424242424241")] // fails Luhn
    [InlineData("電話 03-1234-5678 まで")] // too few digits
    [InlineData("ただの文章です")]
    public void CreditCard_allows_non_cards(string text)
    {
        Assert.Equal(ClassificationAction.Allow, new CreditCardClassifier().Classify(text).Action);
    }

    [Fact]
    public void CreditCard_mask_keeps_last_four_and_strips_separators()
    {
        var result = new CreditCardClassifier().Classify("pay 4111 1111 1111 1111 now");

        Assert.Equal(ClassificationAction.Mask, result.Action);
        Assert.Equal("pay ••••••••••••1111 now", result.MaskedText);
    }

    [Fact]
    public void CreditCard_masks_every_number_in_the_text()
    {
        var result = new CreditCardClassifier().Classify("a 4111111111111111 b 4242424242424242 c");

        Assert.Equal(ClassificationAction.Mask, result.Action);
        Assert.DoesNotContain("4111111111111111", result.MaskedText, StringComparison.Ordinal);
        Assert.DoesNotContain("4242424242424242", result.MaskedText, StringComparison.Ordinal);
        Assert.Contains("1111", result.MaskedText, StringComparison.Ordinal);
        Assert.Contains("4242", result.MaskedText, StringComparison.Ordinal);
    }

    [Fact]
    public void CreditCard_respects_digit_count_boundaries()
    {
        var classifier = new CreditCardClassifier();

        // 13 and 19 digits (with a valid Luhn check) are within range -> masked.
        Assert.Equal(ClassificationAction.Mask, classifier.Classify(LuhnComplete("400000000000")).Action);
        Assert.Equal(ClassificationAction.Mask, classifier.Classify(LuhnComplete("400000000000000000")).Action);

        // 12 digits is below the candidate range -> allowed even with a valid Luhn check.
        Assert.Equal(ClassificationAction.Allow, classifier.Classify(LuhnComplete("40000000000")).Action);

        // 20 contiguous digits cannot be bracketed by word boundaries in the candidate pattern -> allowed.
        Assert.Equal(ClassificationAction.Allow, classifier.Classify(new string('1', 20)).Action);
    }

    // ---------- GenericPasswordClassifier ----------
    [Fact]
    public void GenericPassword_is_off_by_default()
    {
        var classifier = new GenericPasswordClassifier(new InMemorySettingsService());
        Assert.Equal(ClassificationAction.Allow, classifier.Classify("P@ssw0rd!Xy").Action);
    }

    [Theory]
    [InlineData("P@ssw0rd!Xy")]
    [InlineData("Tr0ub4dour&3xtra")]
    public void GenericPassword_masks_strong_tokens_when_enabled(string token)
    {
        Assert.Equal(ClassificationAction.Mask, new GenericPasswordClassifier(Enabled()).Classify(token).Action);
    }

    [Theory]
    [InlineData("password123!")] // no upper-case
    [InlineData("PASSWORD123!")] // no lower-case
    [InlineData("Password!!!!")] // no digit
    [InlineData("Password1234")] // no symbol
    [InlineData("P@ss w0rd!")] // contains whitespace
    [InlineData("Aa1!Aa1!")] // class-complete but low entropy (2.0 bits)
    [InlineData("これは普通の文章です")]
    public void GenericPassword_allows_weak_or_incomplete_tokens_when_enabled(string token)
    {
        Assert.Equal(ClassificationAction.Allow, new GenericPasswordClassifier(Enabled()).Classify(token).Action);
    }

    [Fact]
    public void GenericPassword_respects_length_and_entropy_boundaries()
    {
        var classifier = new GenericPasswordClassifier(Enabled());

        // 8 distinct chars -> entropy is exactly 3.0 bits and length is exactly 8 (both inclusive) -> masked.
        Assert.Equal(ClassificationAction.Mask, classifier.Classify("Aa1!Bc2@").Action);

        // 7 chars -> below the length floor -> allowed.
        Assert.Equal(ClassificationAction.Allow, classifier.Classify("Aa1!Bc2").Action);

        var strong64 = string.Concat(Enumerable.Repeat("AaBb12!@CcDd34#$", 4)); // 64 chars, entropy 4.0 bits
        Assert.Equal(ClassificationAction.Mask, classifier.Classify(strong64).Action);

        // One more character pushes it above the 64-char ceiling -> allowed.
        Assert.Equal(ClassificationAction.Allow, classifier.Classify(strong64 + "x").Action);
    }

    [Fact]
    public void GenericPassword_mask_text_is_fixed_bullets()
    {
        var result = new GenericPasswordClassifier(Enabled()).Classify("P@ssw0rd!Xy");

        Assert.Equal(ClassificationAction.Mask, result.Action);
        Assert.Equal("••••••••", result.MaskedText);
    }

    private static InMemorySettingsService Enabled()
    {
        var settings = new InMemorySettingsService();
        settings.Update(ClipVaultSettings.Default with { MaskGenericPasswords = true });
        return settings;
    }

    // Appends a Luhn check digit so a number of any length can be generated for boundary tests.
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

        var check = (10 - (sum % 10)) % 10;
        return digitsWithoutCheck + check;
    }
}
