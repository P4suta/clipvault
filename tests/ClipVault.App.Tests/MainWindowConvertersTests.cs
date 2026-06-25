using ClipVaultApp;
using Microsoft.UI.Xaml;

namespace ClipVault.App.Tests;

/// <summary>The pure static x:Bind converters used by the history list (no UI thread required).</summary>
public class MainWindowConvertersTests
{
    [Theory]
    [InlineData(true, Visibility.Visible)]
    [InlineData(false, Visibility.Collapsed)]
    public void Bool_to_visibility_maps_directly(bool value, Visibility expected) =>
        Assert.Equal(expected, MainWindow.BoolToVisibility(value));

    [Theory]
    [InlineData(true, Visibility.Collapsed)]
    [InlineData(false, Visibility.Visible)]
    public void Bool_to_visibility_inverse_maps_inversely(bool value, Visibility expected) =>
        Assert.Equal(expected, MainWindow.BoolToVisibilityInverse(value));

    [Fact]
    public void Pin_glyph_differs_for_pinned_and_unpinned()
    {
        Assert.Equal(char.ConvertFromUtf32(0xE77A), MainWindow.PinGlyph(isPinned: true));
        Assert.Equal(char.ConvertFromUtf32(0xE718), MainWindow.PinGlyph(isPinned: false));
    }
}
