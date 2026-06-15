namespace ClipVault.Domain.ValueObjects;

/// <summary>
/// The clipboard sensitivity signals reported by the OS. The raw Win32 formats
/// ("ExcludeClipboardContentFromMonitorProcessing" / "CanIncludeInClipboardHistory") are
/// interpreted on the Infrastructure side and passed into the Domain as this typed value.
/// </summary>
/// <param name="ExcludeFromHistory">
/// A value indicating whether the OS requested that the content be excluded from monitoring and history.
/// </param>
/// <param name="CanIncludeInHistory">
/// A tri-state value indicating whether the content may be included in clipboard history,
/// or <see langword="null"/> when the OS gave no indication.
/// </param>
public sealed record ClipboardPrivacySignals(bool ExcludeFromHistory, bool? CanIncludeInHistory)
{
    /// <summary>Gets the state in which no sensitivity signals are present at all.</summary>
    public static ClipboardPrivacySignals None { get; } = new(ExcludeFromHistory: false, CanIncludeInHistory: null);

    /// <summary>Gets a value indicating whether the OS has explicitly indicated that the content must not be kept in history.</summary>
    public bool ForbidsCapture => ExcludeFromHistory || CanIncludeInHistory == false;
}
