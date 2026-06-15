using System.Diagnostics.CodeAnalysis;
using System.Text;
using ClipVault.Application.Capture;
using ClipVault.Domain.ValueObjects;
using Windows.ApplicationModel.DataTransfer;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace ClipVault.Infrastructure.Clipboard;

/// <summary>
/// Reads the current clipboard contents with WinRT and converts them into a domain snapshot.
/// Must be called from the UI thread (a constraint of the WinRT clipboard API).
/// </summary>
public static class WinRtClipboardReader
{
    // The strongest signal from the OS that content should not be kept in history (presence alone is decisive).
    private const string ExcludeFromMonitorFormat = "ExcludeClipboardContentFromMonitorProcessing";

    /// <summary>
    /// Reads the current clipboard contents and converts them into a domain snapshot.
    /// </summary>
    /// <returns>
    /// A task whose result is the clipboard snapshot, or <see langword="null"/> when there is nothing to capture.
    /// </returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort clipboard read; returns null on failure.")]
    public static async Task<ClipboardSnapshot?> ReadAsync()
    {
        DataPackageView view;
        try
        {
            view = WinClipboard.GetContent();
        }
        catch
        {
            return null;
        }

        var signals = new ClipboardPrivacySignals(
            ExcludeFromHistory: view.AvailableFormats.Contains(ExcludeFromMonitorFormat, StringComparer.Ordinal),
            CanIncludeInHistory: null);

        var source = SourceAppResolver.Resolve();

        if (view.Contains(StandardDataFormats.Text))
        {
            return await ReadTextAsync(view, source, signals);
        }

        if (view.Contains(StandardDataFormats.Bitmap))
        {
            return await ReadImageAsync(view, source, signals);
        }

        return null;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort clipboard read; returns null on failure.")]
    private static async Task<ClipboardSnapshot?> ReadTextAsync(
        DataPackageView view, SourceApplication source, ClipboardPrivacySignals signals)
    {
        string text;
        try
        {
            text = await view.GetTextAsync();
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var payload = Encoding.UTF8.GetBytes(text);
        return new ClipboardSnapshot(ClipContentType.Text, payload, TextPreview.Create(text), Image: null, source, signals);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Best-effort image read/decode; returns null on failure.")]
    private static async Task<ClipboardSnapshot?> ReadImageAsync(
        DataPackageView view, SourceApplication source, ClipboardPrivacySignals signals)
    {
        DecodedImage? decoded;
        try
        {
            var reference = await view.GetBitmapAsync();
            using var stream = await reference.OpenReadAsync();
            decoded = await ImageCodec.DecodeAsync(stream);
        }
        catch
        {
            return null;
        }

        if (decoded is not { } image)
        {
            return null;
        }

        var preview = $"画像 {image.Width}×{image.Height}";
        return new ClipboardSnapshot(
            ClipContentType.Image,
            image.Png,
            preview,
            new ImagePreview(image.Thumbnail, image.Width, image.Height),
            source,
            signals);
    }
}
