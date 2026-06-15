namespace ClipVault.Application.Abstractions;

/// <summary>The UI language. Persisted by name; changes take effect after a restart.</summary>
public enum AppLanguage
{
    /// <summary>Follow the OS display language (the default), falling back to English when unsupported.</summary>
    System,

    /// <summary>Japanese (ja).</summary>
    Japanese,

    /// <summary>English (en).</summary>
    English,

    /// <summary>Simplified Chinese (zh-Hans).</summary>
    ChineseSimplified,
}
