using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Capture;

/// <summary>The result of a gate evaluation: either accepted (with the transformed snapshot) or rejected (with a reason).</summary>
/// <param name="IsAccepted">A value indicating whether the snapshot was accepted.</param>
/// <param name="Snapshot">The accepted (possibly transformed) snapshot, or <see langword="null"/> when rejected.</param>
/// <param name="RejectionReason">The reason for rejection, or <see langword="null"/> when accepted.</param>
public sealed record CaptureGateResult(bool IsAccepted, ClipboardSnapshot? Snapshot, string? RejectionReason)
{
    /// <summary>Creates an accepted result for the given snapshot.</summary>
    /// <param name="snapshot">The accepted snapshot.</param>
    /// <returns>An accepted <see cref="CaptureGateResult"/>.</returns>
    public static CaptureGateResult Accepted(ClipboardSnapshot snapshot) => new(true, snapshot, null);

    /// <summary>Creates a rejected result with the given reason.</summary>
    /// <param name="reason">The reason for rejection.</param>
    /// <returns>A rejected <see cref="CaptureGateResult"/>.</returns>
    public static CaptureGateResult Rejected(string reason) => new(false, null, reason);
}
