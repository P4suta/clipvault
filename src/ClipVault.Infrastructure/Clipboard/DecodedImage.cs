namespace ClipVault.Infrastructure.Clipboard;

/// <summary>
/// The result of decoding a clipboard image (full-size PNG, thumbnail PNG, and dimensions).
/// </summary>
/// <param name="Png">The full-size image encoded as PNG.</param>
/// <param name="Thumbnail">The thumbnail image encoded as PNG.</param>
/// <param name="Width">The width of the full-size image in pixels.</param>
/// <param name="Height">The height of the full-size image in pixels.</param>
internal readonly record struct DecodedImage(byte[] Png, byte[] Thumbnail, int Width, int Height);
