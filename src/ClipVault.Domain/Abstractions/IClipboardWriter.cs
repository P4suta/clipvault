using ClipVault.Domain.ValueObjects;

namespace ClipVault.Domain.Abstractions;

/// <summary>A port that writes a selected past content back to the OS clipboard (paste-back).</summary>
public interface IClipboardWriter
{
    /// <summary>Writes the specified content to the OS clipboard.</summary>
    /// <param name="content">The content to write to the clipboard.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task WriteAsync(ClipContent content, CancellationToken cancellationToken = default);
}
