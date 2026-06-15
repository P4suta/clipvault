using ClipVaultApp.Platform;

namespace ClipVault.App.Tests;

public class TrayMenuItemTests
{
    [Fact]
    public void Command_creates_a_non_separator_item()
    {
        var item = TrayMenuItem.Command(42, "Open", isChecked: true);

        Assert.Equal(42u, item.Id);
        Assert.Equal("Open", item.Text);
        Assert.True(item.IsChecked);
        Assert.False(item.IsSeparator);
    }

    [Fact]
    public void Command_defaults_to_unchecked()
    {
        Assert.False(TrayMenuItem.Command(1, "x").IsChecked);
    }

    [Fact]
    public void Separator_creates_a_separator_item()
    {
        var item = TrayMenuItem.Separator();

        Assert.True(item.IsSeparator);
        Assert.Equal(0u, item.Id);
    }
}
