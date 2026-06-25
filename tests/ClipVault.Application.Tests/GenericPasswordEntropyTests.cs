using ClipVault.Application.Abstractions;
using ClipVault.Application.Capture.Classifiers;
using ClipVault.Application.Settings;

namespace ClipVault.Application.Tests;

/// <summary>Entropy and length boundary behavior of the generic-password classifier (opt-in).</summary>
public class GenericPasswordEntropyTests
{
    // Eight distinct characters -> entropy = log2(8) = 3.0 exactly (the >= 3.0 boundary).
    [Fact]
    public void Masks_at_exactly_three_bits_of_entropy() =>
        Assert.Equal(ClassificationAction.Mask, Enabled().Classify("Aa1!bC2@").Action);

    // Four distinct characters repeated -> entropy = log2(4) = 2.0, below the 3.0 threshold.
    [Fact]
    public void Allows_below_three_bits_even_with_all_character_classes() =>
        Assert.Equal(ClassificationAction.Allow, Enabled().Classify("Aa1!Aa1!").Action);

    // All four classes present, but length 7 is below the minimum of 8.
    [Fact]
    public void Allows_a_seven_character_token() =>
        Assert.Equal(ClassificationAction.Allow, Enabled().Classify("Aa1!bC2").Action);

    private static GenericPasswordClassifier Enabled()
    {
        var settings = new InMemorySettingsService();
        settings.Update(ClipVaultSettings.Default with { MaskGenericPasswords = true });
        return new GenericPasswordClassifier(settings);
    }
}
