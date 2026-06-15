using System;
using System.Collections.Generic;
using System.Globalization;
using ClipVault.Application.Abstractions;

namespace ClipVaultApp.Localization;

/// <summary>
/// Loads per-language string tables from embedded JSON and resolves keys with an English fallback.
/// <see cref="AppLanguage.System"/> is resolved against the OS display language at construction.
/// </summary>
internal sealed class LocalizationService : ILocalizationService
{
    private const string FallbackTag = "en";

    private readonly IReadOnlyDictionary<string, string> _active;
    private readonly IReadOnlyDictionary<string, string> _fallback;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizationService"/> class, resolving
    /// <see cref="AppLanguage.System"/> against the current OS UI culture.
    /// </summary>
    /// <param name="requested">The requested language (possibly <see cref="AppLanguage.System"/>).</param>
    public LocalizationService(AppLanguage requested)
        : this(requested, CultureInfo.CurrentUICulture)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizationService"/> class with an explicit OS
    /// culture. Used by tests to exercise <see cref="AppLanguage.System"/> resolution deterministically.
    /// </summary>
    /// <param name="requested">The requested language (possibly <see cref="AppLanguage.System"/>).</param>
    /// <param name="osUICulture">The OS UI culture used to resolve <see cref="AppLanguage.System"/>.</param>
    internal LocalizationService(AppLanguage requested, CultureInfo osUICulture)
    {
        Current = Resolve(requested, osUICulture);
        CurrentCultureTag = ToTag(Current);
        _fallback = LanguageTables.Load(FallbackTag);
        _active = string.Equals(CurrentCultureTag, FallbackTag, StringComparison.Ordinal)
            ? _fallback
            : LanguageTables.Load(CurrentCultureTag);
    }

    /// <inheritdoc/>
    public AppLanguage Current { get; }

    /// <inheritdoc/>
    public string CurrentCultureTag { get; }

    /// <inheritdoc/>
    public string GetString(string key)
    {
        if (_active.TryGetValue(key, out var value))
        {
            return value;
        }

        return _fallback.TryGetValue(key, out var fallback) ? fallback : key;
    }

    /// <summary>Resolves a request to a concrete language; <see cref="AppLanguage.System"/> uses the OS culture.</summary>
    /// <param name="requested">The requested language.</param>
    /// <param name="osUICulture">The OS UI culture used when the request is <see cref="AppLanguage.System"/>.</param>
    /// <returns>A concrete (non-System) language.</returns>
    internal static AppLanguage Resolve(AppLanguage requested, CultureInfo osUICulture) =>
        requested == AppLanguage.System ? FromCulture(osUICulture) : requested;

    /// <summary>Maps an OS culture to one of the supported languages, falling back to English.</summary>
    /// <param name="culture">The OS culture to map.</param>
    /// <returns>The closest supported language.</returns>
    internal static AppLanguage FromCulture(CultureInfo culture)
    {
        var name = culture.Name;
        if (name.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return AppLanguage.Japanese;
        }

        if (name.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return AppLanguage.English;
        }

        return IsSimplifiedChinese(name) ? AppLanguage.ChineseSimplified : AppLanguage.English;
    }

    /// <summary>Maps a language to its BCP-47 / JSON-table tag.</summary>
    /// <param name="language">The language to map.</param>
    /// <returns>The BCP-47 tag.</returns>
    internal static string ToTag(AppLanguage language) => language switch
    {
        AppLanguage.Japanese => "ja",
        AppLanguage.ChineseSimplified => "zh-Hans",
        _ => "en",
    };

    // zh, zh-Hans*, zh-CN, zh-SG -> Simplified; zh-Hant*, zh-TW, zh-HK, zh-MO -> Traditional (unsupported).
    private static bool IsSimplifiedChinese(string name)
    {
        if (!name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (name.Contains("Hant", StringComparison.OrdinalIgnoreCase)
            || name.Contains("TW", StringComparison.OrdinalIgnoreCase)
            || name.Contains("HK", StringComparison.OrdinalIgnoreCase)
            || name.Contains("MO", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
