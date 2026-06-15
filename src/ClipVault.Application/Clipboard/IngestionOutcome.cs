using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.Clipboard;

/// <summary>The result of a single ingestion (for tests and diagnostics).</summary>
/// <param name="Status">The outcome category of the ingestion.</param>
/// <param name="EntryId">The identifier of the affected entry, or <see langword="null"/> when none applies.</param>
/// <param name="Reason">The reason for rejection or ignoring, or <see langword="null"/> when none applies.</param>
public sealed record IngestionOutcome(IngestionStatus Status, EntryId? EntryId, string? Reason)
{
    /// <summary>Creates an outcome indicating a new entry was added.</summary>
    /// <param name="id">The identifier of the added entry.</param>
    /// <returns>An <see cref="IngestionOutcome"/> with status <see cref="IngestionStatus.Added"/>.</returns>
    public static IngestionOutcome Added(EntryId id) => new(IngestionStatus.Added, id, null);

    /// <summary>Creates an outcome indicating an existing duplicate was promoted to the most recent.</summary>
    /// <param name="id">The identifier of the promoted entry.</param>
    /// <returns>An <see cref="IngestionOutcome"/> with status <see cref="IngestionStatus.Promoted"/>.</returns>
    public static IngestionOutcome Promoted(EntryId id) => new(IngestionStatus.Promoted, id, null);

    /// <summary>Creates an outcome indicating the snapshot was rejected by the privacy gate.</summary>
    /// <param name="reason">The reason for rejection.</param>
    /// <returns>An <see cref="IngestionOutcome"/> with status <see cref="IngestionStatus.Rejected"/>.</returns>
    public static IngestionOutcome Rejected(string reason) => new(IngestionStatus.Rejected, null, reason);

    /// <summary>Creates an outcome indicating the snapshot was ignored (for example, because it was empty).</summary>
    /// <param name="reason">The reason the snapshot was ignored.</param>
    /// <returns>An <see cref="IngestionOutcome"/> with status <see cref="IngestionStatus.Ignored"/>.</returns>
    public static IngestionOutcome Ignored(string reason) => new(IngestionStatus.Ignored, null, reason);
}
