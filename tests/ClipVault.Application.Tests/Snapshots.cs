using System.Text;
using ClipVault.Application.Capture;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Tests;

/// <summary>A helper that builds clipboard snapshots for tests.</summary>
internal static class Snapshots
{
    public static ClipboardSnapshot Text(
        string text,
        SourceApplication? source = null,
        ClipboardPrivacySignals? signals = null) =>
        new(
            ClipContentType.Text,
            Encoding.UTF8.GetBytes(text),
            TextPreview.Create(text),
            Image: null,
            source ?? SourceApplication.Unknown,
            signals ?? ClipboardPrivacySignals.None);

    public static ClipboardSnapshot Image(int byteCount) =>
        new(
            ClipContentType.Image,
            new byte[byteCount],
            "Image",
            new ImagePreview(new byte[] { 1 }, 10, 10),
            SourceApplication.Unknown,
            ClipboardPrivacySignals.None);
}
