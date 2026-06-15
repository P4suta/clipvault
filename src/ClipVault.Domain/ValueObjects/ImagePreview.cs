namespace ClipVault.Domain.ValueObjects;

/// <summary>
/// Display data for an image entry in the list view. Holds the thumbnail (a downscaled PNG)
/// and the dimensions of the original image. The full-size image is fetched lazily via
/// <c>IClipboardHistoryRepository.MaterializeAsync</c>.
/// </summary>
/// <param name="Thumbnail">The downscaled PNG thumbnail bytes.</param>
/// <param name="Width">The width of the original image in pixels.</param>
/// <param name="Height">The height of the original image in pixels.</param>
public sealed record ImagePreview(byte[] Thumbnail, int Width, int Height);
