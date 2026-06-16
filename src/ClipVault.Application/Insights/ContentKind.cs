namespace ClipVault.Application.Insights;

/// <summary>
/// The detected kind of a clipboard entry, used for at-a-glance badges, filtering, and quick actions.
/// Derived heuristically from the entry preview; never persisted.
/// </summary>
public enum ContentKind
{
    /// <summary>Plain text with no more specific kind detected.</summary>
    Text = 0,

    /// <summary>A single http(s) URL.</summary>
    Url = 1,

    /// <summary>An email address.</summary>
    Email = 2,

    /// <summary>A CSS-style color (hex or rgb()/rgba()).</summary>
    Color = 3,

    /// <summary>JSON content.</summary>
    Json = 4,

    /// <summary>A numeric value.</summary>
    Number = 5,

    /// <summary>An image entry.</summary>
    Image = 6,
}
