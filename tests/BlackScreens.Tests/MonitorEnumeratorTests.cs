using Xunit;

namespace BlackScreens.Tests;

public sealed class MonitorEnumeratorTests
{
    [Fact]
    public void Capture_returns_at_least_one_monitor()
    {
        var monitors = MonitorEnumerator.Capture();

        Assert.NotEmpty(monitors);
        Assert.All(monitors, monitor => Assert.True(monitor.Width > 0 && monitor.Height > 0));
    }
}
