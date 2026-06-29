using System.Globalization;
using System.Reflection;

namespace ClipVault.Infrastructure.Diagnostics;

/// <summary>
/// The single runtime entry point for the build's version and release channel, parsed once from the
/// entry assembly's <see cref="AssemblyInformationalVersionAttribute"/> (stamped at build time from the
/// MSBuild <c>Channel</c> property: <c>X.Y.Z-dev</c>, <c>X.Y.Z-nightly.&lt;date&gt;</c>, or <c>X.Y.Z</c>,
/// with a <c>+&lt;sha&gt;</c> suffix appended by Source Link).
/// </summary>
public static class BuildInfo
{
    /// <summary>The stable channel name (no prerelease suffix); also the default data folder.</summary>
    public const string StableChannel = "stable";

    static BuildInfo()
    {
        var informational = (Assembly.GetEntryAssembly() ?? typeof(BuildInfo).Assembly)
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Display = string.IsNullOrWhiteSpace(informational) ? "0.0.0" : informational;
        (Version, Channel) = Parse(Display);
    }

    /// <summary>Gets the base semantic version without any channel suffix (e.g. <c>0.1.0</c>).</summary>
    public static string Version { get; }

    /// <summary>
    /// Gets the release channel: <c>dev</c>, <c>nightly</c>, or <c>stable</c>. Drives per-channel data
    /// isolation (see <c>AppPaths</c>) and the OS identifiers that must not collide between channels.
    /// </summary>
    public static string Channel { get; }

    /// <summary>
    /// Gets the full version string for diagnostics and UI (e.g. <c>0.1.0-nightly.20260630+abc1234</c>).
    /// </summary>
    public static string Display { get; }

    /// <summary>Gets a value indicating whether this is a stable (released) build.</summary>
    public static bool IsStable => string.Equals(Channel, StableChannel, StringComparison.Ordinal);

    /// <summary>Gets the product name plus full version, e.g. <c>ClipVault 0.1.0-dev+abc1234</c>.</summary>
    public static string ProductAndVersion =>
        string.Create(CultureInfo.InvariantCulture, $"ClipVault {Display}");

    /// <summary>
    /// Parses a stamped informational version (e.g. <c>0.1.0-nightly.20260630+abc1234</c>) into its base
    /// version and channel. A version with no prerelease label is the stable channel.
    /// </summary>
    /// <param name="informational">The informational version string to parse.</param>
    /// <returns>The base version (e.g. <c>0.1.0</c>) and channel (<c>dev</c>/<c>nightly</c>/<c>stable</c>).</returns>
    public static (string Version, string Channel) Parse(string informational)
    {
        ArgumentNullException.ThrowIfNull(informational);

        // Strip the +<sha> build-metadata suffix, then split the base version from the prerelease label.
        var core = informational.Split('+', 2)[0];
        var dash = core.IndexOf('-', StringComparison.Ordinal);
        if (dash < 0)
        {
            return (core, StableChannel);
        }

        // The channel is the first dotted segment of the prerelease (e.g. "nightly" from "nightly.20260630").
        // The label is lowercase by construction (set from the MSBuild Channel property), so no case fold.
        var prerelease = core[(dash + 1)..];
        var dot = prerelease.IndexOf('.', StringComparison.Ordinal);
        return (core[..dash], dot < 0 ? prerelease : prerelease[..dot]);
    }
}
