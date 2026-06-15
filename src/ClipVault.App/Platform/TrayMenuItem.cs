namespace ClipVaultApp.Platform;

/// <summary>
/// Represents a single item in the tray context menu.
/// </summary>
/// <param name="Id">The command identifier of the menu item.</param>
/// <param name="Text">The display text of the menu item.</param>
/// <param name="IsChecked">A value indicating whether the menu item is checked.</param>
/// <param name="IsSeparator">A value indicating whether the menu item is a separator.</param>
internal readonly record struct TrayMenuItem(uint Id, string Text, bool IsChecked, bool IsSeparator)
{
    /// <summary>
    /// Creates a command menu item.
    /// </summary>
    /// <param name="id">The command identifier of the menu item.</param>
    /// <param name="text">The display text of the menu item.</param>
    /// <param name="isChecked">A value indicating whether the menu item is checked.</param>
    /// <returns>A command <see cref="TrayMenuItem"/>.</returns>
    public static TrayMenuItem Command(uint id, string text, bool isChecked = false) =>
        new(id, text, isChecked, false);

    /// <summary>
    /// Creates a separator menu item.
    /// </summary>
    /// <returns>A separator <see cref="TrayMenuItem"/>.</returns>
    public static TrayMenuItem Separator() => new(0, string.Empty, false, true);
}
