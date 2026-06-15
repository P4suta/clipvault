using ClipVault.Domain.Policies;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Domain.Entities;

/// <summary>
/// A single entry in the clipboard history. A rich domain model that holds the metadata needed for
/// the list view plus a preview for display. It does not retain the full-size content (the text or
/// image body itself); that is fetched lazily via <c>IClipboardHistoryRepository.MaterializeAsync</c>
/// when required.
/// </summary>
public sealed class ClipboardEntry
{
    private ClipboardEntry(
        EntryId id,
        ClipContentType contentType,
        ContentHash hash,
        string preview,
        ImagePreview? image,
        long sizeInBytes,
        SourceApplication source,
        DateTimeOffset capturedAt,
        DateTimeOffset lastUsedAt,
        bool isPinned)
    {
        Id = id;
        ContentType = contentType;
        Hash = hash;
        Preview = preview;
        Image = image;
        SizeInBytes = sizeInBytes;
        Source = source;
        CapturedAt = capturedAt;
        LastUsedAt = lastUsedAt;
        IsPinned = isPinned;
    }

    /// <summary>Gets the unique identifier of the entry.</summary>
    public EntryId Id { get; }

    /// <summary>Gets the kind of clipboard content.</summary>
    public ClipContentType ContentType { get; }

    /// <summary>Gets the duplicate-detection key (the keyed hash).</summary>
    public ContentHash Hash { get; }

    /// <summary>Gets the short summary for the list view (the start of the text, or an image's dimension label, etc.).</summary>
    public string Preview { get; }

    /// <summary>Gets the display data for an image entry. Non-null only when <see cref="ContentType"/> is Image.</summary>
    public ImagePreview? Image { get; }

    /// <summary>Gets the size of the full-size content in bytes.</summary>
    public long SizeInBytes { get; }

    /// <summary>Gets the application that produced the content.</summary>
    public SourceApplication Source { get; }

    /// <summary>Gets the time at which the content was captured.</summary>
    public DateTimeOffset CapturedAt { get; }

    /// <summary>Gets the time at which the entry was last used.</summary>
    public DateTimeOffset LastUsedAt { get; private set; }

    /// <summary>Gets a value indicating whether the entry is pinned.</summary>
    public bool IsPinned { get; private set; }

    /// <summary>Creates a newly captured entry.</summary>
    /// <param name="contentType">The kind of clipboard content.</param>
    /// <param name="hash">The duplicate-detection key (the keyed hash).</param>
    /// <param name="preview">The short summary for the list view.</param>
    /// <param name="image">The display data for an image entry, or <see langword="null"/> for non-image content.</param>
    /// <param name="sizeInBytes">The size of the full-size content in bytes.</param>
    /// <param name="source">The application that produced the content.</param>
    /// <param name="capturedAt">The time at which the content was captured.</param>
    /// <returns>A new, unpinned <see cref="ClipboardEntry"/> whose last-used time equals its captured time.</returns>
    public static ClipboardEntry Create(
        ClipContentType contentType,
        ContentHash hash,
        string preview,
        ImagePreview? image,
        long sizeInBytes,
        SourceApplication source,
        DateTimeOffset capturedAt) =>
        new(
            EntryId.New(),
            contentType,
            hash,
            preview,
            image,
            sizeInBytes,
            source,
            capturedAt,
            lastUsedAt: capturedAt,
            isPinned: false);

    /// <summary>Restores a persisted entry from all of its fields (for repository use).</summary>
    /// <param name="id">The unique identifier of the entry.</param>
    /// <param name="contentType">The kind of clipboard content.</param>
    /// <param name="hash">The duplicate-detection key (the keyed hash).</param>
    /// <param name="preview">The short summary for the list view.</param>
    /// <param name="image">The display data for an image entry, or <see langword="null"/> for non-image content.</param>
    /// <param name="sizeInBytes">The size of the full-size content in bytes.</param>
    /// <param name="source">The application that produced the content.</param>
    /// <param name="capturedAt">The time at which the content was captured.</param>
    /// <param name="lastUsedAt">The time at which the entry was last used.</param>
    /// <param name="isPinned">A value indicating whether the entry is pinned.</param>
    /// <returns>A <see cref="ClipboardEntry"/> reconstructed from the supplied fields.</returns>
    public static ClipboardEntry Restore(
        EntryId id,
        ClipContentType contentType,
        ContentHash hash,
        string preview,
        ImagePreview? image,
        long sizeInBytes,
        SourceApplication source,
        DateTimeOffset capturedAt,
        DateTimeOffset lastUsedAt,
        bool isPinned) =>
        new(id, contentType, hash, preview, image, sizeInBytes, source, capturedAt, lastUsedAt, isPinned);

    /// <summary>Pins the entry.</summary>
    public void Pin() => IsPinned = true;

    /// <summary>Unpins the entry.</summary>
    public void Unpin() => IsPinned = false;

    /// <summary>Updates the time at which this entry was used (copied or pasted).</summary>
    /// <param name="now">The time at which the entry was used.</param>
    public void MarkUsed(DateTimeOffset now) => LastUsedAt = now;

    /// <summary>Determines whether this entry has expired according to the supplied retention policy.</summary>
    /// <param name="policy">The retention policy to evaluate against.</param>
    /// <param name="now">The current time used to evaluate the entry's age.</param>
    /// <returns><see langword="true"/> if the entry has expired; otherwise, <see langword="false"/>.</returns>
    public bool IsExpired(IRetentionPolicy policy, DateTimeOffset now) => policy.ShouldEvict(this, now);

    // An entity establishes identity by its identifier.

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ClipboardEntry other && other.Id == Id;

    /// <inheritdoc/>
    public override int GetHashCode() => Id.GetHashCode();
}
