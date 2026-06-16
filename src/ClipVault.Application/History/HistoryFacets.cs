using ClipVault.Application.Insights;

namespace ClipVault.Application.History;

/// <summary>
/// The distinct facets present in the history, used to build only the filter options that actually exist.
/// </summary>
/// <param name="Kinds">The distinct content kinds present, in enum order.</param>
/// <param name="SourceApps">The distinct source-application process names present, in display order.</param>
public sealed record HistoryFacets(IReadOnlyList<ContentKind> Kinds, IReadOnlyList<string> SourceApps);
