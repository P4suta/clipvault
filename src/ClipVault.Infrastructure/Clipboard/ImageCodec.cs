using System.Diagnostics.CodeAnalysis;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ClipVault.Infrastructure.Clipboard;

/// <summary>
/// Converts a clipboard image into a full-size PNG and a thumbnail PNG (WinRT image processing).
/// </summary>
internal static class ImageCodec
{
    private const uint ThumbnailMaxDimension = 256;

    // Upper bound on decoded image area. The pixel plane is materialized as BGRA (4 bytes/pixel) and
    // SoftwareBitmap.Convert holds a second copy, so peak transient memory is ~8 bytes/pixel. Capping at
    // ~64 megapixels (~512 MB peak) rejects decompression bombs before any pixel buffer is allocated, while
    // still admitting 8K-class screenshots and camera photos. A safety invariant, deliberately not user-tunable.
    private const long MaxDecodedPixels = 64L * 1000 * 1000;

    /// <summary>
    /// Decodes the image from the given stream into a full-size PNG and a thumbnail PNG.
    /// </summary>
    /// <param name="source">The random-access stream containing the source image.</param>
    /// <returns>
    /// A task whose result is the decoded image, or <see langword="null"/> when the image cannot be decoded.
    /// </returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort image decode; returns null on corrupt data.")]
    public static async Task<DecodedImage?> DecodeAsync(IRandomAccessStream source)
    {
        BitmapDecoder decoder;
        try
        {
            decoder = await BitmapDecoder.CreateAsync(source);
        }
        catch
        {
            return null;
        }

        // Reject oversized images from the header dimensions (cheap to read) before GetSoftwareBitmapAsync
        // allocates the full BGRA pixel plane. The per-dimension checks run first and short-circuit the area
        // multiplication, so a corrupt/hostile header claiming enormous dimensions cannot overflow it.
        long pixelWidth = decoder.PixelWidth;
        long pixelHeight = decoder.PixelHeight;
        if (pixelWidth > MaxDecodedPixels || pixelHeight > MaxDecodedPixels || pixelWidth * pixelHeight > MaxDecodedPixels)
        {
            return null;
        }

        var width = (int)decoder.PixelWidth;
        var height = (int)decoder.PixelHeight;

        using var bitmap = await decoder.GetSoftwareBitmapAsync();
        using var bgra = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var png = await EncodePngAsync(bgra, maxDimension: null);
        var thumbnail = await EncodePngAsync(bgra, maxDimension: ThumbnailMaxDimension);
        return new DecodedImage(png, thumbnail, width, height);
    }

    private static async Task<byte[]> EncodePngAsync(SoftwareBitmap bitmap, uint? maxDimension)
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);

        if (maxDimension is { } max && (bitmap.PixelWidth > max || bitmap.PixelHeight > max))
        {
            var (scaledWidth, scaledHeight) = ScaleToFit(bitmap.PixelWidth, bitmap.PixelHeight, max);
            encoder.BitmapTransform.ScaledWidth = scaledWidth;
            encoder.BitmapTransform.ScaledHeight = scaledHeight;
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        }

        await encoder.FlushAsync();
        return await RandomAccessStreams.ToBytesAsync(stream);
    }

    private static (uint Width, uint Height) ScaleToFit(int width, int height, uint max)
    {
        var scale = Math.Min((double)max / width, (double)max / height);
        return ((uint)Math.Max(1.0, width * scale), (uint)Math.Max(1.0, height * scale));
    }
}
