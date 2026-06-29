using ClipVault.Infrastructure;
using ClipVault.Infrastructure.Diagnostics;

namespace ClipVault.Infrastructure.Tests;

/// <summary>
/// Verifies the build-channel parsing contract: an informational version stamped from the MSBuild
/// <c>Channel</c> property (plus Source Link's <c>+sha</c>) maps to the right base version and channel,
/// and the per-channel data root / OS-identifier qualification follow from it.
/// </summary>
public sealed class BuildInfoTests
{
    [Theory]
    [InlineData("0.1.0", "0.1.0", "stable")]
    [InlineData("0.1.0+abc1234", "0.1.0", "stable")]
    [InlineData("0.1.0-dev", "0.1.0", "dev")]
    [InlineData("0.1.0-dev+abc1234", "0.1.0", "dev")]
    [InlineData("1.2.3-nightly.20260630", "1.2.3", "nightly")]
    [InlineData("1.2.3-nightly.20260630+deadbee", "1.2.3", "nightly")]
    public void Parse_splits_version_and_channel(string informational, string version, string channel)
    {
        var (parsedVersion, parsedChannel) = BuildInfo.Parse(informational);

        Assert.Equal(version, parsedVersion);
        Assert.Equal(channel, parsedChannel);
    }

    [Fact]
    public void Parse_throws_on_null() => Assert.Throws<ArgumentNullException>(() => BuildInfo.Parse(null!));

    [Fact]
    public void Runtime_build_is_the_dev_channel_under_test()
    {
        // The test host builds with the default Channel, so it parses as a non-stable dev build.
        Assert.Equal("dev", BuildInfo.Channel);
        Assert.False(BuildInfo.IsStable);
        Assert.StartsWith(BuildInfo.Version + "-dev", BuildInfo.Display, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalAppDataRoot_isolates_the_non_stable_channel()
    {
        var root = AppPaths.LocalAppDataRoot();

        Assert.EndsWith(Path.Combine("ClipVault", BuildInfo.Channel), root, StringComparison.Ordinal);
    }

    [Fact]
    public void QualifiedName_appends_the_channel_off_stable() =>
        Assert.Equal("ClipVault (dev)", AppPaths.QualifiedName("ClipVault"));
}
