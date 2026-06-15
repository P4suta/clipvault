using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Capture.Rules;

/// <summary>
/// The highest-priority rule that honors the OS privacy signals. It cannot be overridden even by user settings (the core of the product's trust).
/// </summary>
public sealed class PrivacySignalRule : ICaptureRule
{
    /// <inheritdoc/>
    public CaptureRuleResult Evaluate(ClipboardSnapshot snapshot) =>
        snapshot.PrivacySignals.ForbidsCapture
            ? CaptureRuleResult.Reject("The OS forbids storing this in history.")
            : CaptureRuleResult.Continue(snapshot);
}
