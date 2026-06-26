using System.Diagnostics.CodeAnalysis;
using ClipVault.Application.Insights;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace ClipVaultApp.ViewModels;

/// <summary>
/// Display wrapper representing a single row in the list. The underlying
/// <see cref="ClipboardEntry"/> is treated as read-only; state changes (pin, last-used time, etc.)
/// are made only through <c>ClipboardActionService</c>.
/// </summary>
public sealed partial class EntryViewModel : ObservableObject
{
    /// <summary>Fetches the thumbnail bytes on demand (so list rows do not carry them); null falls back to the entry's own bytes.</summary>
    private readonly Func<EntryId, CancellationToken, Task<byte[]?>>? _thumbnailProvider;

    /// <summary>Flag that ensures the thumbnail is decoded only once.</summary>
    private bool _isThumbnailLoadStarted;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntryViewModel"/> class.
    /// </summary>
    /// <param name="entry">The underlying domain entity wrapped by this row.</param>
    /// <param name="kind">The detected content kind used for the badge.</param>
    /// <param name="kindLabel">The localized label shown on the badge.</param>
    /// <param name="thumbnailProvider">Fetches the thumbnail bytes on demand; when null, the entry's own bytes are used.</param>
    public EntryViewModel(
        ClipboardEntry entry,
        ContentKind kind,
        string kindLabel,
        Func<EntryId, CancellationToken, Task<byte[]?>>? thumbnailProvider = null)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        Kind = kind;
        KindLabel = kindLabel ?? string.Empty;
        _thumbnailProvider = thumbnailProvider;
    }

    /// <summary>Gets the underlying domain entity passed to the action service.</summary>
    public ClipboardEntry Entry { get; }

    /// <summary>Gets the detected content kind.</summary>
    public ContentKind Kind { get; }

    /// <summary>Gets the localized label shown on the kind badge.</summary>
    public string KindLabel { get; }

    /// <summary>Gets a value indicating whether a kind badge should be shown (only for the specific kinds, not plain text or images).</summary>
    public bool HasBadge => Kind is not (ContentKind.Text or ContentKind.Image);

    /// <summary>Gets the thumbnail image for the entry, if available.</summary>
    [ObservableProperty]
    public partial ImageSource? Thumbnail { get; private set; }

    /// <summary>Gets the short summary used in the list display; image entries show a localized label plus dimensions.</summary>
    public string Preview => IsImage && Entry.Image is { } image
        ? $"{KindLabel} {image.Width}×{image.Height}"
        : Entry.Preview;

    /// <summary>Gets a value indicating whether the entry is pinned.</summary>
    public bool IsPinned => Entry.IsPinned;

    /// <summary>Gets a value indicating whether the entry is an image entry.</summary>
    public bool IsImage => Entry.ContentType == ClipContentType.Image;

    /// <summary>Gets a value indicating whether the entry is a text entry (used to switch glyph display).</summary>
    public bool IsText => Entry.ContentType == ClipContentType.Text;

    /// <summary>Gets the process name of the source application.</summary>
    public string SourceName => Entry.Source.ProcessName;

    /// <summary>Gets the last-used time (short local-time representation; follows the current culture as it is user-facing).</summary>
    public string Timestamp =>
        Entry.LastUsedAt.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>
    /// Lazily and asynchronously decodes the thumbnail of an image entry. Intended to be called
    /// only once, when each row in the list is loaded.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort thumbnail render; falls back to a glyph on failure.")]
    public async Task EnsureThumbnailAsync()
    {
        if (_isThumbnailLoadStarted || !IsImage)
        {
            return;
        }

        _isThumbnailLoadStarted = true;

        try
        {
            // With a provider, the thumbnail bytes are fetched (and decrypted) on demand; without one, fall back to
            // any bytes carried on the entry itself (used by tests that build an entry with an inline thumbnail).
            var bytes = _thumbnailProvider is not null
                ? await _thumbnailProvider(Entry.Id, CancellationToken.None)
                : Entry.Image?.Thumbnail;
            if (bytes is not { Length: > 0 })
            {
                return;
            }

            var bitmap = new BitmapImage();
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }

            stream.Seek(0);
            await bitmap.SetSourceAsync(stream);
            Thumbnail = bitmap;
        }
        catch
        {
            // Do not break the whole list if thumbnail decoding fails (fall back to glyph display).
            _isThumbnailLoadStarted = false;
        }
    }
}
