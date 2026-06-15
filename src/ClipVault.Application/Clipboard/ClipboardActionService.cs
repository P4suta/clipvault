using ClipVault.Application.Messages;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;
using CommunityToolkit.Mvvm.Messaging;

namespace ClipVault.Application.Clipboard;

/// <summary>Groups the entry actions invoked from the UI (paste back, pin, delete, clear all).</summary>
/// <param name="repository">The clipboard history repository.</param>
/// <param name="writer">The clipboard writer used to paste content back.</param>
/// <param name="monitor">The clipboard monitor, used to suppress self re-capture.</param>
/// <param name="clock">The clock used to stamp usage times.</param>
/// <param name="messenger">The messenger used to announce history changes.</param>
public sealed class ClipboardActionService(
    IClipboardHistoryRepository repository,
    IClipboardWriter writer,
    IClipboardMonitor monitor,
    IClock clock,
    IMessenger messenger)
{
    /// <summary>Decrypts the entry content and writes it back to the clipboard (suppressing self re-capture).</summary>
    /// <param name="entry">The entry to copy back to the clipboard.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that produces <see langword="true"/> when the content was written; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> CopyToClipboardAsync(ClipboardEntry entry, CancellationToken cancellationToken = default)
    {
        // Owns the materialized copy: dispose it (zeroing the plaintext) once it has been written.
        using var content = await repository.MaterializeAsync(entry.Id, cancellationToken);
        if (content is null)
        {
            return false;
        }

        using (monitor.SuppressNextCapture())
        {
            await writer.WriteAsync(content, cancellationToken);
        }

        entry.MarkUsed(clock.UtcNow);
        await repository.UpdateAsync(entry, cancellationToken);
        messenger.Send(new HistoryChangedMessage());
        return true;
    }

    /// <summary>Decrypts and returns the entry content for in-app display only, without writing it to the clipboard or updating the last-used time.</summary>
    /// <param name="entry">The entry whose content should be materialized for viewing.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that produces the decrypted content, or <see langword="null"/> when the entry no longer exists.</returns>
    public Task<ClipContent?> MaterializeForViewAsync(ClipboardEntry entry, CancellationToken cancellationToken = default) =>
        repository.MaterializeAsync(entry.Id, cancellationToken);

    /// <summary>Writes the supplied content to the clipboard for an explicit copy from the detail view, without updating the last-used time or the history order.</summary>
    /// <param name="content">The content to write to the clipboard.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task CopyForViewAsync(ClipContent content, CancellationToken cancellationToken = default)
    {
        using (monitor.SuppressNextCapture())
        {
            await writer.WriteAsync(content, cancellationToken);
        }
    }

    /// <summary>Toggles the pinned state of the entry and announces the change.</summary>
    /// <param name="entry">The entry to pin or unpin.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task TogglePinAsync(ClipboardEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.IsPinned)
        {
            entry.Unpin();
        }
        else
        {
            entry.Pin();
        }

        await repository.UpdateAsync(entry, cancellationToken);
        messenger.Send(new HistoryChangedMessage());
    }

    /// <summary>Removes the entry from the history and announces the change.</summary>
    /// <param name="entry">The entry to remove.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteAsync(ClipboardEntry entry, CancellationToken cancellationToken = default)
    {
        await repository.RemoveAsync(entry.Id, cancellationToken);
        messenger.Send(new HistoryChangedMessage());
    }

    /// <summary>Clears all history and announces the change.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await repository.ClearAsync(cancellationToken);
        messenger.Send(new HistoryChangedMessage());
    }
}
