namespace ClipVault.Domain.ValueObjects;

/// <summary>
/// A single decrypted snapshot captured when the clipboard changes.
/// It is the input to privacy-gate evaluation and ingestion. When masked by a classifier,
/// it is transformed into a new snapshot via a <c>with</c> expression.
/// </summary>
/// <param name="Type">The kind of clipboard content.</param>
/// <param name="Payload">The raw content bytes.</param>
/// <param name="Preview">A short summary of the content for the list view.</param>
/// <param name="Image">The display data for image content, or <see langword="null"/> for non-image content.</param>
/// <param name="Source">The application that produced the content.</param>
/// <param name="PrivacySignals">The privacy signals reported by the OS for this content.</param>
public sealed record ClipboardSnapshot(
    ClipContentType Type,
    byte[] Payload,
    string Preview,
    ImagePreview? Image,
    SourceApplication Source,
    ClipboardPrivacySignals PrivacySignals)
{
    /// <summary>Gets the size of the content in bytes.</summary>
    public long SizeInBytes => Payload.LongLength;
}
