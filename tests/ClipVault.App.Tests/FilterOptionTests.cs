using ClipVault.Application.Abstractions;
using ClipVault.Application.Insights;
using ClipVaultApp.ViewModels;

namespace ClipVault.App.Tests;

public class FilterOptionTests
{
    [Fact]
    public void Kind_filter_options_are_equal_by_value()
    {
        Assert.Equal(new KindFilterOption("Url", ContentKind.Url), new KindFilterOption("Url", ContentKind.Url));
        Assert.NotEqual(new KindFilterOption("Url", ContentKind.Url), new KindFilterOption("Url", ContentKind.Text));
    }

    [Fact]
    public void App_filter_options_are_equal_by_value()
    {
        Assert.Equal(new AppFilterOption("Chrome", "chrome"), new AppFilterOption("Chrome", "chrome"));
        Assert.NotEqual(new AppFilterOption("Chrome", "chrome"), new AppFilterOption("Chrome", "code"));
    }

    [Fact]
    public void Language_option_exposes_value_and_display_name()
    {
        var option = new LanguageOption(AppLanguage.Japanese, "日本語");

        Assert.Equal(AppLanguage.Japanese, option.Value);
        Assert.Equal("日本語", option.DisplayName);
    }

    [Fact]
    public void Theme_option_is_equal_by_value()
    {
        Assert.Equal(new ThemeOption(AppTheme.Dark, "Dark"), new ThemeOption(AppTheme.Dark, "Dark"));
        Assert.NotEqual(new ThemeOption(AppTheme.Dark, "Dark"), new ThemeOption(AppTheme.Light, "Light"));
    }
}
