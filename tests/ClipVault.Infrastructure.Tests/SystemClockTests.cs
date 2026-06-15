using ClipVault.Infrastructure.Time;

namespace ClipVault.Infrastructure.Tests;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_falls_between_two_capture_points()
    {
        var before = DateTimeOffset.UtcNow;
        var value = new SystemClock().UtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(value, before, after);
    }
}
