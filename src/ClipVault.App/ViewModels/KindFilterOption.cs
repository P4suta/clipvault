using ClipVault.Application.Insights;

namespace ClipVaultApp.ViewModels;

/// <summary>A selectable content-kind filter: the localized label plus the kind to filter by.</summary>
/// <param name="Label">The label shown in the filter selector.</param>
/// <param name="Kind">The content kind to filter by, or <see langword="null"/> for the "all kinds" option.</param>
public sealed record KindFilterOption(string Label, ContentKind? Kind);
