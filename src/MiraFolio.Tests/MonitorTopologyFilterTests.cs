using MiraFolio.Core.Utilities;
using Xunit;

namespace MiraFolio.Tests;

public class MonitorTopologyFilterTests
{
    [Fact]
    public void FilterToActiveDesktop_RemovesInactiveAndDuplicateBounds()
    {
        var candidates = new[]
        {
            new MonitorCandidate(@"\\.\DISPLAY1", new MonitorBounds(0, 0, 1920, 1080)),
            new MonitorCandidate(@"\\.\DISPLAY2", new MonitorBounds(1920, 0, 2560, 1440)),
            new MonitorCandidate(@"\\.\DISPLAYVIRT", new MonitorBounds(4480, 0, 1024, 768)),
            new MonitorCandidate(@"\\.\DISPLAY2_DUP", new MonitorBounds(1920, 0, 2560, 1440))
        };

        var activeBounds = new[]
        {
            new MonitorBounds(0, 0, 1920, 1080),
            new MonitorBounds(1920, 0, 2560, 1440)
        };

        var monitors = MonitorTopologyFilter.FilterToActiveDesktop(candidates, activeBounds);

        Assert.Collection(
            monitors,
            monitor =>
            {
                Assert.Equal(0, monitor.Index);
                Assert.Equal(@"\\.\DISPLAY1", monitor.DevicePath);
            },
            monitor =>
            {
                Assert.Equal(1, monitor.Index);
                Assert.Equal(@"\\.\DISPLAY2", monitor.DevicePath);
            });
    }

    [Fact]
    public void FilterToActiveDesktop_FallsBackToCandidatesWhenActiveBoundsUnavailable()
    {
        var candidates = new[]
        {
            new MonitorCandidate(@"\\.\DISPLAY3", new MonitorBounds(-1080, 0, 1080, 1920)),
            new MonitorCandidate(@"\\.\DISPLAY1", new MonitorBounds(0, 0, 1920, 1080))
        };

        var monitors = MonitorTopologyFilter.FilterToActiveDesktop(candidates, Array.Empty<MonitorBounds>());

        Assert.Collection(
            monitors,
            monitor =>
            {
                Assert.Equal(0, monitor.Index);
                Assert.Equal(@"\\.\DISPLAY3", monitor.DevicePath);
                Assert.Equal(-1080, monitor.Left);
            },
            monitor =>
            {
                Assert.Equal(1, monitor.Index);
                Assert.Equal(@"\\.\DISPLAY1", monitor.DevicePath);
                Assert.Equal(1920, monitor.Width);
            });
    }
}
