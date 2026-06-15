namespace ClipVault.Application.Capture;

/// <summary>Builds a short preview for list display from text (collapsing whitespace and truncating).</summary>
public static class TextPreview
{
    /// <summary>The default maximum length of a generated preview.</summary>
    public const int DefaultMaxLength = 160;

    /// <summary>Creates a short preview from the given text.</summary>
    /// <param name="text">The text to build a preview from.</param>
    /// <param name="maxLength">The maximum length of the preview.</param>
    /// <returns>A whitespace-collapsed preview, truncated with an ellipsis when longer than <paramref name="maxLength"/>.</returns>
    public static string Create(string text, int maxLength = DefaultMaxLength)
    {
        var collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= maxLength ? collapsed : string.Concat(collapsed.AsSpan(0, maxLength), "…");
    }
}
