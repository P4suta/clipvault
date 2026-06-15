namespace ClipVaultApp;

/// <summary>Result of unlocking at startup (success with an optional passphrase, or cancellation).</summary>
internal abstract record UnlockResult
{
    /// <summary>Validation succeeded (the passphrase is null when none was used).</summary>
    /// <param name="Passphrase">The validated passphrase, or null when no passphrase was used.</param>
    internal sealed record Unlocked(string? Passphrase) : UnlockResult;

    /// <summary>The user gave up on unlocking (chose to exit or closed the window).</summary>
    internal sealed record Cancelled : UnlockResult;
}
