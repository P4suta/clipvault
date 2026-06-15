using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Capture;

/// <summary>
/// A single rule that makes up the privacy gate. It evaluates a snapshot and returns either continue (optionally transformed) or reject.
/// </summary>
public interface ICaptureRule
{
    /// <summary>Evaluates the snapshot against this rule.</summary>
    /// <param name="snapshot">The snapshot to evaluate.</param>
    /// <returns>A result that continues the pipeline (optionally transformed) or rejects the snapshot.</returns>
    CaptureRuleResult Evaluate(ClipboardSnapshot snapshot);
}
