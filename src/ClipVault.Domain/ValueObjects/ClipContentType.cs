namespace ClipVault.Domain.ValueObjects;

/// <summary>
/// The kind of clipboard content. Explicit numeric values are assigned for persistence
/// (new members must be appended at the end).
/// </summary>
public enum ClipContentType
{
    /// <summary>Textual content (stored as UTF-8 bytes).</summary>
    Text = 0,

    /// <summary>Image content (stored as PNG bytes).</summary>
    Image = 1,
}
