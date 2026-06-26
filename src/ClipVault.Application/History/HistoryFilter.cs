using ClipVault.Application.Insights;
using ClipVault.Domain.ValueObjects;

namespace ClipVault.Application.History;

/// <summary>
/// The criteria used to filter the history: a free-text search term plus optional content-type, content-kind, and
/// source-application filters. A null or blank field imposes no constraint.
/// </summary>
/// <param name="Search">The term matched against the preview and source process name, or <see langword="null"/> for no text filter.</param>
/// <param name="Type">The content type to filter by, or <see langword="null"/> for no type filter.</param>
/// <param name="Kind">The derived content kind to filter by, or <see langword="null"/> for no kind filter.</param>
/// <param name="SourceApp">The source-application process name to filter by, or <see langword="null"/> for no app filter.</param>
public sealed record HistoryFilter(
    string? Search = null,
    ClipContentType? Type = null,
    ContentKind? Kind = null,
    string? SourceApp = null);
