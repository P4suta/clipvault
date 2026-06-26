using ClipVault.Domain.Entities;

namespace ClipVault.Domain.Abstractions;

/// <summary>
/// A page of history entries plus the cursor to resume after it. <see cref="NextCursor"/> is the position of the
/// last returned entry when the page was full (more may follow), or <see langword="null"/> once the source is exhausted.
/// </summary>
/// <param name="Entries">The entries in this page, in the history's sort order.</param>
/// <param name="NextCursor">The cursor to resume after, or <see langword="null"/> when no more entries remain.</param>
public sealed record HistoryPage(IReadOnlyList<ClipboardEntry> Entries, HistoryCursor? NextCursor);
