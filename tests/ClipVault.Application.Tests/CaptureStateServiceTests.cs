using ClipVault.Application.Capture;

namespace ClipVault.Application.Tests;

public class CaptureStateServiceTests
{
    [Fact]
    public void Starts_unpaused() => Assert.False(new CaptureStateService().IsPaused);

    [Fact]
    public void Pause_then_unpause_toggles_state()
    {
        var service = new CaptureStateService();

        service.Pause();
        Assert.True(service.IsPaused);

        service.Unpause();
        Assert.False(service.IsPaused);
    }

    [Fact]
    public void Toggle_flips_the_state()
    {
        var service = new CaptureStateService();

        service.Toggle();
        Assert.True(service.IsPaused);

        service.Toggle();
        Assert.False(service.IsPaused);
    }

    [Fact]
    public void StateChanged_fires_only_on_an_actual_change()
    {
        var service = new CaptureStateService();
        var count = 0;
        service.StateChanged += (_, _) => count++;

        service.Pause();   // Changes -> fires.
        service.Pause();   // No change -> no fire.
        service.Unpause(); // Changes -> fires.
        service.Unpause(); // No change -> no fire.

        Assert.Equal(2, count);
    }
}
