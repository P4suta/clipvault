using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Capture;

/// <summary>
/// The single choke point before saving. It evaluates rules in registration order and aborts the capture if any rule
/// returns a rejection. Each rule may transform the snapshot (for example, by masking) and pass it on to the next.
/// </summary>
/// <param name="rules">The rules that make up the privacy gate, evaluated in order.</param>
public sealed class CaptureGate(IEnumerable<ICaptureRule> rules)
{
    private readonly IReadOnlyList<ICaptureRule> _rules = rules.ToList();

    /// <summary>Evaluates the snapshot against every rule and returns the gate result.</summary>
    /// <param name="snapshot">The snapshot to evaluate.</param>
    /// <returns>An accepted result with the (possibly transformed) snapshot, or a rejection with a reason.</returns>
    public CaptureGateResult Evaluate(ClipboardSnapshot snapshot)
    {
        var current = snapshot;
        foreach (var rule in _rules)
        {
            var result = rule.Evaluate(current);
            if (result.Rejected)
            {
                return CaptureGateResult.Rejected(result.Reason ?? "Unknown reason");
            }

            current = result.Snapshot!;
        }

        return CaptureGateResult.Accepted(current);
    }
}
