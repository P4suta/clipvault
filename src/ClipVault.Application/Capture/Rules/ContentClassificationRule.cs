using System.Text;
using ClipVault.Application.Capture.Classifiers;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Capture.Rules;

/// <summary>
/// A safety net that applies the classifiers to text content. If any classifier rejects, the snapshot is discarded
/// (rejection wins); otherwise the first mask is applied, replacing the content and the preview. The full text is
/// scanned (each classifier regex is bounded by a match timeout) so a secret near the end is not missed.
/// </summary>
public sealed class ContentClassificationRule(IEnumerable<IClipboardContentClassifier> classifiers) : ICaptureRule
{
    private readonly IReadOnlyList<IClipboardContentClassifier> _classifiers = classifiers.ToList();

    /// <inheritdoc/>
    public CaptureRuleResult Evaluate(ClipboardSnapshot snapshot)
    {
        if (snapshot.Type != ClipContentType.Text)
        {
            return CaptureRuleResult.Continue(snapshot);
        }

        var text = Encoding.UTF8.GetString(snapshot.Payload);

        var results = _classifiers
            .Select(classifier => (classifier.Name, Result: classifier.Classify(text)))
            .ToList();

        var rejected = results.FirstOrDefault(x => x.Result.Action == ClassificationAction.Reject);
        if (rejected.Result is not null)
        {
            return CaptureRuleResult.Reject($"Sensitive content detected: {rejected.Name}");
        }

        var masked = results.FirstOrDefault(x => x.Result.Action == ClassificationAction.Mask);
        if (masked.Result?.MaskedText is { } maskedText)
        {
            return CaptureRuleResult.Continue(snapshot with
            {
                Payload = Encoding.UTF8.GetBytes(maskedText),
                Preview = TextPreview.Create(maskedText),
            });
        }

        return CaptureRuleResult.Continue(snapshot);
    }
}
