using Microsoft.UI.Xaml.Markup;

namespace ClipVaultApp.Localization;

/// <summary>
/// XAML markup extension that resolves a localized string at load time, used as
/// <c>{loc:Str Key=Some.Key}</c>. The language is fixed for the process lifetime (applied at startup),
/// so returning a plain string here is sufficient. Markup extensions only target dependency properties;
/// non-dependency targets such as <c>Window.Title</c> are set from code-behind instead.
/// </summary>
[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed class StrExtension : MarkupExtension
{
    /// <summary>Gets or sets the string-table key (for example "Main.ClearAll").</summary>
    public string Key { get; set; } = string.Empty;

    /// <inheritdoc/>
    protected override object ProvideValue() => App.Localization.GetString(Key);
}
