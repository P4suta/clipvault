using ClipVault.Domain.ValueObjects;

namespace ClipVault.Domain.Abstractions;

/// <summary>
/// The event arguments for a clipboard-change notification (<see cref="IClipboardMonitor.ClipboardChanged"/>).
/// Carries a decrypted, debounced snapshot.
/// </summary>
/// <param name="snapshot">The decrypted clipboard snapshot to be ingested.</param>
public sealed class ClipboardChangedEventArgs(ClipboardSnapshot snapshot) : EventArgs
{
    /// <summary>Gets the decrypted clipboard snapshot to be ingested.</summary>
    public ClipboardSnapshot Snapshot { get; } = snapshot;
}
