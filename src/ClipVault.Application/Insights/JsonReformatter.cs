using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ClipVault.Application.Insights;

/// <summary>Reformats JSON text (pretty-print or minify). Pure; invalid JSON yields <see langword="false"/>.</summary>
public static class JsonReformatter
{
    // Relaxed escaping keeps non-ASCII (e.g. Japanese) readable; the output is clipboard text, not HTML.
    private static readonly JsonSerializerOptions IndentedOptions =
        new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly JsonSerializerOptions CompactOptions =
        new() { WriteIndented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Attempts to reformat the input as JSON.</summary>
    /// <param name="input">The candidate JSON text.</param>
    /// <param name="indented"><see langword="true"/> to pretty-print; <see langword="false"/> to minify.</param>
    /// <param name="result">The reformatted JSON when parsing succeeds; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the input parsed as JSON.</returns>
    public static bool TryFormat(string input, bool indented, [NotNullWhen(true)] out string? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(input);
            result = JsonSerializer.Serialize(document.RootElement, indented ? IndentedOptions : CompactOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
