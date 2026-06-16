namespace ClipVaultApp.ViewModels;

/// <summary>
/// Immutable wrapper representing a single row in the excluded-apps list. Because using a primitive
/// type (string) directly as the DataType in an x:Bind DataTemplate makes the XAML compiler
/// unstable, it is wrapped in an explicit type.
/// </summary>
public sealed class ExcludedAppViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExcludedAppViewModel"/> class.
    /// </summary>
    /// <param name="name">The excluded process name.</param>
    public ExcludedAppViewModel(string name)
    {
        Name = name;
    }

    /// <summary>Gets the excluded process name.</summary>
    public string Name { get; }
}
