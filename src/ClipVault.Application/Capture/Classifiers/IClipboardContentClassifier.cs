namespace ClipVault.Application.Capture.Classifiers;

/// <summary>A pure strategy that judges the sensitivity of text content (text format only).</summary>
public interface IClipboardContentClassifier
{
    /// <summary>Gets the name of the classifier.</summary>
    string Name { get; }

    /// <summary>Classifies the given text.</summary>
    /// <param name="text">The text to classify.</param>
    /// <returns>The classification result describing how the content should be handled.</returns>
    ClassificationResult Classify(string text);
}
