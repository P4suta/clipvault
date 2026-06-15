namespace ClipVault.Application.Clipboard;

/// <summary>The outcome category of a single ingestion.</summary>
public enum IngestionStatus
{
    /// <summary>Stored as a new entry.</summary>
    Added,

    /// <summary>An existing duplicate was promoted to the most recent.</summary>
    Promoted,

    /// <summary>Discarded by the privacy gate.</summary>
    Rejected,

    /// <summary>Ignored for a reason such as being empty.</summary>
    Ignored,
}
