using System.Globalization;
using ClipVault.Infrastructure.Diagnostics;

namespace ClipVault.Infrastructure;

/// <summary>
/// The single source of truth for where this build keeps its on-disk and OS-registered state. Non-stable
/// channels (dev, nightly) live under their own subfolder and use channel-qualified OS identifiers so a
/// development or nightly build never reads, writes, or overwrites the released build's clipboard history,
/// encryption key, startup registration, or Windows Hello credential.
/// </summary>
public static class AppPaths
{
    private const string AppFolder = "ClipVault";

    /// <summary>
    /// Gets the per-channel local application data root: <c>%LOCALAPPDATA%\ClipVault</c> for the stable
    /// channel, and <c>%LOCALAPPDATA%\ClipVault\&lt;channel&gt;</c> (e.g. <c>...\dev</c>) otherwise.
    /// </summary>
    /// <returns>The absolute path to the channel's local application data root.</returns>
    public static string LocalAppDataRoot()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolder);
        return BuildInfo.IsStable ? root : Path.Combine(root, BuildInfo.Channel);
    }

    /// <summary>
    /// Qualifies an OS-global identifier (a registry value name, a credential name) with the channel so it
    /// does not collide with the stable build. Returns <paramref name="baseName"/> unchanged on stable,
    /// e.g. <c>ClipVault</c> -> <c>ClipVault (nightly)</c> off-stable.
    /// </summary>
    /// <param name="baseName">The stable identifier to qualify.</param>
    /// <returns>The channel-qualified identifier.</returns>
    public static string QualifiedName(string baseName) =>
        BuildInfo.IsStable
            ? baseName
            : string.Create(CultureInfo.InvariantCulture, $"{baseName} ({BuildInfo.Channel})");
}
