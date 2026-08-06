using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using MiraFolio.Core.Services;
using MiraFolio.Core.Utilities;
using Xunit;

namespace MiraFolio.Tests;

public class MonitorServiceTests
{
    [Fact]
    public void RefreshMonitors_BadTarget_DoesNotHideHealthyMonitors()
    {
        var source = new FakeDesktopMonitorSource(
            new Target("display-1", new MonitorBounds(0, 0, 1920, 1080)),
            new Target("stale-display", new MonitorBounds(1920, 0, 1920, 1080), BoundsFail: true),
            new Target("display-3", new MonitorBounds(1920, 0, 2560, 1440)));
        var activeBounds = new[]
        {
            new MonitorBounds(0, 0, 1920, 1080),
            new MonitorBounds(1920, 0, 2560, 1440)
        };

        using var service = CreateService(source, () => activeBounds);

        Assert.Collection(
            service.GetMonitors(),
            monitor => Assert.Equal("display-1", monitor.DevicePath),
            monitor => Assert.Equal("display-3", monitor.DevicePath));
    }

    [Fact]
    public void RefreshMonitors_TemporaryBoundsFailure_ReusesKnownActiveMonitor()
    {
        var source = new FakeDesktopMonitorSource(
            new Target("display-1", new MonitorBounds(0, 0, 1920, 1080)),
            new Target("display-2", new MonitorBounds(1920, 0, 2560, 1440)));
        var activeBounds = new[]
        {
            new MonitorBounds(0, 0, 1920, 1080),
            new MonitorBounds(1920, 0, 2560, 1440)
        };
        using var service = CreateService(source, () => activeBounds);

        source.Targets[1] = source.Targets[1] with { BoundsFail = true };
        service.RefreshMonitors();

        Assert.Equal(2, service.GetMonitors().Count);
        Assert.Equal("display-2", service.GetMonitors()[1].DevicePath);
    }

    [Fact]
    public void RefreshMonitors_NewTarget_AppearsAndRaisesChangeEvent()
    {
        var source = new FakeDesktopMonitorSource(
            new Target("display-1", new MonitorBounds(0, 0, 1920, 1080)));
        IReadOnlyCollection<MonitorBounds> activeBounds =
        [
            new MonitorBounds(0, 0, 1920, 1080)
        ];
        using var service = CreateService(source, () => activeBounds);
        int changedCount = 0;
        service.MonitorsChanged += (_, _) => changedCount++;

        var secondBounds = new MonitorBounds(1920, 0, 2560, 1440);
        source.Targets.Add(new Target("display-2", secondBounds));
        activeBounds =
        [
            new MonitorBounds(0, 0, 1920, 1080),
            secondBounds
        ];
        service.RefreshMonitors();

        Assert.Equal(1, changedCount);
        Assert.Collection(
            service.GetMonitors(),
            monitor => Assert.Equal("display-1", monitor.DevicePath),
            monitor => Assert.Equal("display-2", monitor.DevicePath));
    }

    private static MonitorService CreateService(
        IDesktopMonitorSource source,
        Func<IReadOnlyCollection<MonitorBounds>> activeBoundsProvider) =>
        new(
            NullLogger<MonitorService>.Instance,
            source,
            activeBoundsProvider);

    private sealed record Target(string Path, MonitorBounds Bounds, bool PathFail = false, bool BoundsFail = false);

    private sealed class FakeDesktopMonitorSource(params Target[] targets) : IDesktopMonitorSource
    {
        public List<Target> Targets { get; } = [.. targets];

        public uint GetMonitorCount() => (uint)Targets.Count;

        public string GetDevicePathAt(uint index)
        {
            var target = Targets[(int)index];
            if (target.PathFail)
                throw new COMException("Device path unavailable", unchecked((int)0x80004005));

            return target.Path;
        }

        public MonitorBounds GetMonitorBounds(string devicePath)
        {
            var target = Targets.Single(target => target.Path == devicePath);
            if (target.BoundsFail)
                throw new COMException("Bounds unavailable", unchecked((int)0x80004005));

            return target.Bounds;
        }
    }
}
