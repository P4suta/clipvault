using System.Runtime.InteropServices;
using System.Text;
using ClipVault.Application.Abstractions;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.ValueObjects;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace ClipVault.Infrastructure.Clipboard;

/// <summary>
/// Writes the selected content back to the OS clipboard. To keep our own writes from leaking into the OS
/// cloud history, it sets the content with history and roaming disabled when possible.
/// </summary>
/// <param name="dispatcher">The UI dispatcher used to write to the clipboard on the UI thread.</param>
public sealed class WinRtClipboardWriter(IUiDispatcher dispatcher) : IClipboardWriter
{
    /// <inheritdoc/>
    public Task WriteAsync(ClipContent content, CancellationToken cancellationToken = default) =>
        dispatcher.EnqueueAsync(async () =>
        {
            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };

            if (content.Type == ClipContentType.Text)
            {
                package.SetText(Encoding.UTF8.GetString(content.Payload));
            }
            else if (content.Type == ClipContentType.Image)
            {
                var stream = await RandomAccessStreams.FromBytesAsync(content.Payload);
                package.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
            }

            try
            {
                WinClipboard.SetContentWithOptions(
                    package, new ClipboardContentOptions { IsAllowedInHistory = false, IsRoamable = false });
            }
            catch (COMException)
            {
                // Fall back to the regular set on environments where the set-with-options call is unavailable.
                WinClipboard.SetContent(package);
            }
        });
}
