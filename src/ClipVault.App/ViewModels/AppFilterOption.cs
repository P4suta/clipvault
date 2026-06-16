namespace ClipVaultApp.ViewModels;

/// <summary>A selectable source-application filter: the label plus the process name to filter by.</summary>
/// <param name="Label">The label shown in the filter selector.</param>
/// <param name="App">The source process name to filter by, or <see langword="null"/> for the "all apps" option.</param>
public sealed record AppFilterOption(string Label, string? App);
