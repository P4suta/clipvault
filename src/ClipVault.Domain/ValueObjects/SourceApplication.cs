namespace ClipVault.Domain.ValueObjects;

/// <summary>The source application that wrote the content to the clipboard.</summary>
/// <param name="ProcessName">The name of the source process.</param>
/// <param name="WindowTitle">The title of the source window, or <see langword="null"/> when unavailable.</param>
/// <param name="ExecutablePath">The full path to the source executable, or <see langword="null"/> when unavailable.</param>
public sealed record SourceApplication(string ProcessName, string? WindowTitle, string? ExecutablePath)
{
    /// <summary>Gets the default value used when the source could not be identified.</summary>
    public static SourceApplication Unknown { get; } = new("unknown", null, null);
}
