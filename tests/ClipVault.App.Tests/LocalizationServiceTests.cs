using System.Globalization;
using ClipVault.Application.Abstractions;
using ClipVaultApp.Localization;

namespace ClipVault.App.Tests;

public sealed class LocalizationServiceTests
{
    [Theory]
    [InlineData("ja-JP", AppLanguage.Japanese)]
    [InlineData("ja", AppLanguage.Japanese)]
    [InlineData("en-US", AppLanguage.English)]
    [InlineData("en-GB", AppLanguage.English)]
    [InlineData("zh-Hans", AppLanguage.ChineseSimplified)]
    [InlineData("zh-CN", AppLanguage.ChineseSimplified)]
    [InlineData("zh-SG", AppLanguage.ChineseSimplified)]
    [InlineData("zh-Hant", AppLanguage.English)]
    [InlineData("zh-TW", AppLanguage.English)]
    [InlineData("zh-HK", AppLanguage.English)]
    [InlineData("fr-FR", AppLanguage.English)]
    public void FromCulture_maps_to_a_supported_language(string cultureName, AppLanguage expected)
    {
        Assert.Equal(expected, LocalizationService.FromCulture(CultureInfo.GetCultureInfo(cultureName)));
    }

    [Fact]
    public void Resolve_keeps_a_concrete_language_and_ignores_the_culture()
    {
        Assert.Equal(
            AppLanguage.Japanese,
            LocalizationService.Resolve(AppLanguage.Japanese, CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void Resolve_system_uses_the_os_culture()
    {
        Assert.Equal(
            AppLanguage.ChineseSimplified,
            LocalizationService.Resolve(AppLanguage.System, CultureInfo.GetCultureInfo("zh-CN")));
    }

    [Theory]
    [InlineData(AppLanguage.Japanese, "ja")]
    [InlineData(AppLanguage.English, "en")]
    [InlineData(AppLanguage.ChineseSimplified, "zh-Hans")]
    public void ToTag_maps_a_language_to_its_bcp47_tag(AppLanguage language, string expected)
    {
        Assert.Equal(expected, LocalizationService.ToTag(language));
    }

    [Fact]
    public void Current_resolves_system_against_the_os_culture()
    {
        var loc = new LocalizationService(AppLanguage.System, CultureInfo.GetCultureInfo("ja-JP"));

        Assert.Equal(AppLanguage.Japanese, loc.Current);
        Assert.Equal("ja", loc.CurrentCultureTag);
    }

    [Fact]
    public void GetString_returns_the_localized_value()
    {
        var loc = new LocalizationService(AppLanguage.English, CultureInfo.InvariantCulture);

        Assert.Equal("Clear all", loc.GetString("Main.ClearAll.Name"));
    }

    [Fact]
    public void GetString_returns_the_key_when_it_is_missing()
    {
        var loc = new LocalizationService(AppLanguage.English, CultureInfo.InvariantCulture);

        Assert.Equal("No.Such.Key", loc.GetString("No.Such.Key"));
    }

    [Fact]
    public void All_language_tables_share_the_same_keys()
    {
        var en = LanguageTables.Load("en");
        var ja = LanguageTables.Load("ja");
        var zh = LanguageTables.Load("zh-Hans");

        Assert.NotEmpty(en);
        Assert.Equal(
            en.Keys.OrderBy(k => k, StringComparer.Ordinal),
            ja.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(
            en.Keys.OrderBy(k => k, StringComparer.Ordinal),
            zh.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }
}
