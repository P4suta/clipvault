using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Capture;

/// <summary>The result of a rule evaluation: either continue (with the transformed snapshot) or reject (with a reason).</summary>
public sealed record CaptureRuleResult
{
    /// <summary>Gets a value indicating whether the snapshot was rejected.</summary>
    public bool Rejected { get; private init; }

    /// <summary>Gets the reason for rejection, or <see langword="null"/> when not rejected.</summary>
    public string? Reason { get; private init; }

    /// <summary>Gets the (possibly transformed) snapshot to continue with, or <see langword="null"/> when rejected.</summary>
    public ClipboardSnapshot? Snapshot { get; private init; }

    /// <summary>Creates a result that continues the pipeline with the given snapshot.</summary>
    /// <param name="snapshot">The snapshot to continue with.</param>
    /// <returns>A continuing <see cref="CaptureRuleResult"/>.</returns>
    public static CaptureRuleResult Continue(ClipboardSnapshot snapshot) => new() { Snapshot = snapshot };

    /// <summary>Creates a result that rejects the snapshot with the given reason.</summary>
    /// <param name="reason">The reason for rejection.</param>
    /// <returns>A rejecting <see cref="CaptureRuleResult"/>.</returns>
    public static CaptureRuleResult Reject(string reason) => new() { Rejected = true, Reason = reason };
}
