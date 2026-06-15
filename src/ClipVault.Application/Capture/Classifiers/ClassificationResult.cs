namespace ClipVault.Application.Capture.Classifiers;

/// <summary>A classification result. Holds the handling and, when masking, the replacement text.</summary>
/// <param name="Action">The handling indicated by the classifier.</param>
/// <param name="MaskedText">The replacement text to use when masking, or <see langword="null"/> otherwise.</param>
public sealed record ClassificationResult(ClassificationAction Action, string? MaskedText = null)
{
    /// <summary>Gets a result that allows the content as-is.</summary>
    public static ClassificationResult Allow { get; } = new(ClassificationAction.Allow);

    /// <summary>Gets a result that rejects the content.</summary>
    public static ClassificationResult Reject { get; } = new(ClassificationAction.Reject);

    /// <summary>Creates a result that masks the content with the given replacement text.</summary>
    /// <param name="maskedText">The replacement text to use.</param>
    /// <returns>A masking <see cref="ClassificationResult"/>.</returns>
    public static ClassificationResult Mask(string maskedText) => new(ClassificationAction.Mask, maskedText);
}
