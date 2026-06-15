using System.Diagnostics.CodeAnalysis;
using ClipVault.Application.Capture;
using ClipVault.Application.Messages;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;
using CommunityToolkit.Mvvm.Messaging;

namespace ClipVault.Application.Clipboard;

/// <summary>
/// The core capture use case: privacy gate, then duplicate detection, then encrypted storage (encapsulated in the
/// repository), then notification. It is entirely port-based orchestration and can be unit-tested without the UI, the
/// clipboard, or a database.
/// </summary>
/// <param name="gate">The privacy gate evaluated before storage.</param>
/// <param name="encryption">The encryption service used to compute the keyed hash.</param>
/// <param name="repository">The clipboard history repository.</param>
/// <param name="clock">The clock used to stamp capture and usage times.</param>
/// <param name="messenger">The messenger used to announce history changes.</param>
public sealed class ClipboardIngestionService(
    CaptureGate gate,
    IEncryptionService encryption,
    IClipboardHistoryRepository repository,
    IClock clock,
    IMessenger messenger)
{
    /// <summary>Ingests a single clipboard snapshot through the gate and into the history.</summary>
    /// <param name="snapshot">The snapshot to ingest.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that produces the outcome of the ingestion.</returns>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The repository takes ownership of the ClipContent (the in-memory store retains it); disposing here would zero the retained payload.")]
    public async Task<IngestionOutcome> IngestAsync(ClipboardSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot.Payload.Length == 0)
        {
            return IngestionOutcome.Ignored("Empty snapshot");
        }

        var gateResult = gate.Evaluate(snapshot);
        if (!gateResult.IsAccepted)
        {
            return IngestionOutcome.Rejected(gateResult.RejectionReason!);
        }

        var accepted = gateResult.Snapshot!;
        var hash = encryption.KeyedHash(accepted.Payload);

        var existing = await repository.FindByHashAsync(hash, cancellationToken);
        if (existing is not null)
        {
            existing.MarkUsed(clock.UtcNow);
            await repository.UpdateAsync(existing, cancellationToken);
            messenger.Send(new HistoryChangedMessage());
            return IngestionOutcome.Promoted(existing.Id);
        }

        var entry = ClipboardEntry.Create(
            accepted.Type,
            hash,
            accepted.Preview,
            accepted.Image,
            accepted.SizeInBytes,
            accepted.Source,
            clock.UtcNow);

        await repository.AddAsync(entry, new ClipContent(accepted.Type, accepted.Payload), cancellationToken);
        messenger.Send(new HistoryChangedMessage());
        return IngestionOutcome.Added(entry.Id);
    }
}
