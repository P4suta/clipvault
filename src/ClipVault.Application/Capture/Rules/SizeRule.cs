using ClipVault.Application.Abstractions;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Capture.Rules;

/// <summary>Discards images that exceed the limit (to prevent database bloat and memory pressure).</summary>
public sealed class SizeRule(ISettingsService settings) : ICaptureRule
{
    /// <inheritdoc/>
    public CaptureRuleResult Evaluate(ClipboardSnapshot snapshot)
    {
        var max = settings.Current.MaxImageBytes;
        return snapshot.Type == ClipContentType.Image && snapshot.SizeInBytes > max
            ? CaptureRuleResult.Reject($"The image exceeds the {max} byte limit.")
            : CaptureRuleResult.Continue(snapshot);
    }
}
