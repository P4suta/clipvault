using ClipVault.Application.Abstractions;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Capture.Rules;

/// <summary>Rejects everything while capture is paused (secret mode).</summary>
/// <param name="state">Reports whether clipboard capture is paused.</param>
public sealed class CaptureStateRule(ICaptureStateService state) : ICaptureRule
{
    /// <inheritdoc/>
    public CaptureRuleResult Evaluate(ClipboardSnapshot snapshot) =>
        state.IsPaused
            ? CaptureRuleResult.Reject("Capture is paused.")
            : CaptureRuleResult.Continue(snapshot);
}
