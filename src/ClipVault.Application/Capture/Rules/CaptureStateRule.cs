using ClipVault.Application.Abstractions;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Capture.Rules;

/// <summary>Rejects everything while capture is paused (secret mode).</summary>
public sealed class CaptureStateRule(ICaptureStateService state) : ICaptureRule
{
    /// <inheritdoc/>
    public CaptureRuleResult Evaluate(ClipboardSnapshot snapshot) =>
        state.IsPaused
            ? CaptureRuleResult.Reject("Capture is paused.")
            : CaptureRuleResult.Continue(snapshot);
}
