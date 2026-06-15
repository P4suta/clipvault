using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ClipVaultApp.Localization;

/// <summary>Loads embedded per-language JSON string tables (Localization/Strings/&lt;tag&gt;.json).</summary>
internal static class LanguageTables
{
    /// <summary>Loads the string table for a tag, returning an empty table when the resource is missing.</summary>
    /// <param name="tag">The BCP-47 tag (for example "ja", "en", or "zh-Hans").</param>
    /// <returns>The key/value table for the language.</returns>
    public static IReadOnlyDictionary<string, string> Load(string tag)
    {
        var assembly = typeof(LanguageTables).Assembly;
        var suffix = $"Strings.{tag}.json";

        var resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith(suffix, StringComparison.Ordinal));

        if (resourceName is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
