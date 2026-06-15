using ClipVault.Application.Abstractions;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Capture.Rules;

/// <summary>Discards the capture when the originating process is on the exclusion list (password managers and the like).</summary>
public sealed class SourceAppRule(ISettingsService settings) : ICaptureRule
{
    /// <inheritdoc/>
    public CaptureRuleResult Evaluate(ClipboardSnapshot snapshot)
    {
        var process = snapshot.Source.ProcessName;
        return settings.Current.ExcludedProcessNames.Contains(process)
            ? CaptureRuleResult.Reject($"Copied from an excluded app: {process}")
            : CaptureRuleResult.Continue(snapshot);
    }
}
