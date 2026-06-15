namespace ClipVault.Application.Messages;

/// <summary>
/// Loosely notifies the UI that the history has changed (via the CommunityToolkit messenger).
/// Recipients re-query the history and refresh their display.
/// </summary>
public sealed record HistoryChangedMessage;
