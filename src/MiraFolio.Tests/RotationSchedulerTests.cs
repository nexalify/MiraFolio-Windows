using Microsoft.Extensions.Logging.Abstractions;
using MiraFolio.Core.Models;
using MiraFolio.Core.Services;
using Xunit;

namespace MiraFolio.Tests;

public class RotationSchedulerTests
{
    [Fact]
    public void RotateNow_RetriesUntilImageIsAvailable()
    {
        var monitor = new MonitorInfo(0, "display-1", 0, 0, 1920, 1080);
        var settings = new AppSettings
        {
            MonitorAssignments =
            [
                new WallpaperAssignment
                {
                    MonitorDevicePath = monitor.DevicePath,
                    FolderPath = @"\\nas\wallpapers",
                    Enabled = true
                }
            ]
        };

        var monitorService = new FakeMonitorService(monitor);
        var wallpaperService = new FakeWallpaperService();
        var imageSelector = new FakeImageSelector([null, null, @"\\nas\wallpapers\a.jpg"]);
        var settingsService = new FakeSettingsService(settings);

        using var scheduler = CreateScheduler(monitorService, wallpaperService, imageSelector, settingsService);

        scheduler.RotateNow(monitor.DevicePath);

        WaitForCondition(() => wallpaperService.SetWallpaperCalls.Count == 1);

        Assert.Single(imageSelector.PrewarmCalls);
        Assert.Equal(@"\\nas\wallpapers", imageSelector.PrewarmCalls[0]);
        Assert.True(imageSelector.SelectCalls.Count >= 3);
        Assert.Equal(@"\\nas\wallpapers\a.jpg", wallpaperService.SetWallpaperCalls[0].ImagePath);
        Assert.Equal(@"\\nas\wallpapers\a.jpg", settingsService.State.MonitorStates.Single().CurrentWallpaperPath);
        Assert.Equal(@"\\nas\wallpapers\a.jpg", settingsService.State.MonitorStates.Single().RandomPlayback.SeenPaths.Single());
    }

    [Fact]
    public void SettingsChanged_FolderChange_TriggersImmediateRotation()
    {
        var monitor = new MonitorInfo(0, "display-1", 0, 0, 1920, 1080);
        var oldSettings = new AppSettings
        {
            MonitorAssignments =
            [
                new WallpaperAssignment
                {
                    MonitorDevicePath = monitor.DevicePath,
                    FolderPath = @"C:\Old",
                    Enabled = true
                }
            ]
        };
        var newSettings = new AppSettings
        {
            MonitorAssignments =
            [
                new WallpaperAssignment
                {
                    MonitorDevicePath = monitor.DevicePath,
                    FolderPath = @"D:\New",
                    Enabled = true
                }
            ]
        };

        var monitorService = new FakeMonitorService(monitor);
        var wallpaperService = new FakeWallpaperService();
        var imageSelector = new FakeImageSelector([null, @"D:\New\picked.jpg"]);
        var settingsService = new FakeSettingsService(oldSettings);

        using var scheduler = CreateScheduler(monitorService, wallpaperService, imageSelector, settingsService);

        settingsService.RaiseSettingsChanged(newSettings);

        WaitForCondition(() => wallpaperService.SetWallpaperCalls.Count == 1);

        Assert.Equal(@"D:\New", imageSelector.PrewarmCalls.Single());
        Assert.Equal(@"D:\New\picked.jpg", wallpaperService.SetWallpaperCalls[0].ImagePath);
    }

    [Fact]
    public void SettingsChanged_IntervalOnly_DoesNotTriggerImmediateRotation()
    {
        var monitor = new MonitorInfo(0, "display-1", 0, 0, 1920, 1080);
        var oldSettings = new AppSettings
        {
            MonitorAssignments =
            [
                new WallpaperAssignment
                {
                    MonitorDevicePath = monitor.DevicePath,
                    FolderPath = @"D:\Wallpapers",
                    Enabled = true,
                    IntervalValue = 30,
                    IntervalUnit = WallpaperIntervalUnit.Minutes
                }
            ]
        };
        var newSettings = new AppSettings
        {
            MonitorAssignments =
            [
                new WallpaperAssignment
                {
                    MonitorDevicePath = monitor.DevicePath,
                    FolderPath = @"D:\Wallpapers",
                    Enabled = true,
                    IntervalValue = 45,
                    IntervalUnit = WallpaperIntervalUnit.Minutes
                }
            ]
        };

        var monitorService = new FakeMonitorService(monitor);
        var wallpaperService = new FakeWallpaperService();
        var imageSelector = new FakeImageSelector([@"D:\Wallpapers\picked.jpg"]);
        var settingsService = new FakeSettingsService(oldSettings);

        using var scheduler = CreateScheduler(monitorService, wallpaperService, imageSelector, settingsService);

        settingsService.RaiseSettingsChanged(newSettings);
        Thread.Sleep(150);

        Assert.Empty(imageSelector.PrewarmCalls);
        Assert.Empty(wallpaperService.SetWallpaperCalls);
        Assert.Empty(imageSelector.SelectCalls);
    }

    [Fact]
    public void RotateNow_WithLowResolutionFilter_PassesMinimumSideLengthToSelector()
    {
        var monitor = new MonitorInfo(0, "display-1", 0, 0, 1920, 1080);
        var settings = new AppSettings
        {
            Global = new GlobalSettings
            {
                LowResolutionFilterEnabled = true,
                MinimumImageSideLength = 1024
            },
            MonitorAssignments =
            [
                new WallpaperAssignment
                {
                    MonitorDevicePath = monitor.DevicePath,
                    FolderPath = @"D:\Wallpapers",
                    Enabled = true
                }
            ]
        };

        var monitorService = new FakeMonitorService(monitor);
        var wallpaperService = new FakeWallpaperService();
        var imageSelector = new FakeImageSelector([@"D:\Wallpapers\picked.jpg"]);
        var settingsService = new FakeSettingsService(settings);

        using var scheduler = CreateScheduler(monitorService, wallpaperService, imageSelector, settingsService);

        scheduler.RotateNow(monitor.DevicePath);

        WaitForCondition(() => wallpaperService.SetWallpaperCalls.Count == 1);

        Assert.Contains(imageSelector.SelectCalls, call => call.MinimumImageSideLength == 1024);
    }

    [Fact]
    public void RotateNow_WithLowResolutionFilterAndNoEligibleImage_DoesNotSetWallpaper()
    {
        var monitor = new MonitorInfo(0, "display-1", 0, 0, 1920, 1080);
        var settings = new AppSettings
        {
            Global = new GlobalSettings
            {
                LowResolutionFilterEnabled = true,
                MinimumImageSideLength = 1024
            },
            MonitorAssignments =
            [
                new WallpaperAssignment
                {
                    MonitorDevicePath = monitor.DevicePath,
                    FolderPath = @"D:\Wallpapers",
                    Enabled = true
                }
            ]
        };

        var monitorService = new FakeMonitorService(monitor);
        var wallpaperService = new FakeWallpaperService();
        var imageSelector = new FakeImageSelector([null, null, null, null]);
        var settingsService = new FakeSettingsService(settings);

        using var scheduler = CreateScheduler(monitorService, wallpaperService, imageSelector, settingsService);

        scheduler.RotateNow(monitor.DevicePath);
        WaitForCondition(() => imageSelector.SelectCalls.Count > 0);

        Assert.Empty(wallpaperService.SetWallpaperCalls);
        Assert.Contains(imageSelector.SelectCalls, call => call.MinimumImageSideLength == 1024);
    }

    [Fact]
    public void RotateNow_PassesRemovedImagesToSelector()
    {
        var monitor = new MonitorInfo(0, "display-1", 0, 0, 1920, 1080);
        var removedPath = @"D:\Wallpapers\removed.jpg";
        var settings = new AppSettings
        {
            RemovedImages = [new RemovedImageRecord { FilePath = removedPath }],
            MonitorAssignments =
            [
                new WallpaperAssignment
                {
                    MonitorDevicePath = monitor.DevicePath,
                    FolderPath = @"D:\Wallpapers",
                    Enabled = true
                }
            ]
        };

        var monitorService = new FakeMonitorService(monitor);
        var wallpaperService = new FakeWallpaperService();
        var imageSelector = new FakeImageSelector([@"D:\Wallpapers\picked.jpg"]);
        var settingsService = new FakeSettingsService(settings);

        using var scheduler = CreateScheduler(monitorService, wallpaperService, imageSelector, settingsService);
        scheduler.RotateNow(monitor.DevicePath);
        WaitForCondition(() => wallpaperService.SetWallpaperCalls.Count == 1);

        Assert.Contains(imageSelector.SelectCalls, call =>
            call.ExcludedImagePaths.Contains(removedPath, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void MonitorsChanged_ConfiguredMonitorConnected_AppliesWallpaperImmediately()
    {
        var existingMonitor = new MonitorInfo(0, "display-1", 0, 0, 1920, 1080);
        var newMonitor = new MonitorInfo(1, "display-2", 1920, 0, 2560, 1440);
        var settings = new AppSettings
        {
            MonitorAssignments =
            [
                new WallpaperAssignment
                {
                    MonitorDevicePath = newMonitor.DevicePath,
                    FolderPath = @"D:\NewMonitor",
                    Enabled = true
                }
            ]
        };
        var monitorService = new FakeMonitorService(existingMonitor);
        var wallpaperService = new FakeWallpaperService();
        var imageSelector = new FakeImageSelector([@"D:\NewMonitor\picked.jpg"]);
        var settingsService = new FakeSettingsService(settings);

        using var scheduler = CreateScheduler(monitorService, wallpaperService, imageSelector, settingsService);
        scheduler.Start();

        monitorService.SetMonitors(existingMonitor, newMonitor);

        WaitForCondition(() => wallpaperService.SetWallpaperCalls.Count == 1);
        Assert.Equal(newMonitor.DevicePath, wallpaperService.SetWallpaperCalls[0].MonitorDevicePath);
        Assert.Equal(@"D:\NewMonitor\picked.jpg", wallpaperService.SetWallpaperCalls[0].ImagePath);
    }

    private static RotationScheduler CreateScheduler(
        FakeMonitorService monitorService,
        FakeWallpaperService wallpaperService,
        FakeImageSelector imageSelector,
        FakeSettingsService settingsService) =>
        new(
            monitorService,
            wallpaperService,
            imageSelector,
            settingsService,
            NullLogger<RotationScheduler>.Instance,
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(20));

    private static void WaitForCondition(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            Thread.Sleep(20);
        }

        Assert.Fail("Timed out waiting for condition.");
    }

    private sealed class FakeMonitorService(params MonitorInfo[] monitors) : IMonitorService
    {
        private IReadOnlyList<MonitorInfo> _monitors = monitors;

        public IReadOnlyList<MonitorInfo> GetMonitors() => _monitors;

        public event EventHandler? MonitorsChanged;

        public void SetMonitors(params MonitorInfo[] monitors)
        {
            _monitors = monitors;
            MonitorsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeWallpaperService : IWallpaperService
    {
        public List<(string MonitorDevicePath, string ImagePath)> SetWallpaperCalls { get; } = [];
        public WallpaperDisplayMode? LastDisplayMode { get; private set; }

        public void SetDisplayMode(WallpaperDisplayMode mode) => LastDisplayMode = mode;

        public void SetWallpaper(string monitorDevicePath, string imagePath) =>
            SetWallpaperCalls.Add((monitorDevicePath, imagePath));

        public string? GetCurrentWallpaper(string monitorDevicePath) =>
            SetWallpaperCalls.LastOrDefault(call => call.MonitorDevicePath == monitorDevicePath).ImagePath;

    }

    private sealed class FakeImageSelector(IEnumerable<string?> responses) : IImageSelector
    {
        private readonly object _lock = new();
        private readonly Queue<string?> _responses = new(responses);

        public List<string> PrewarmCalls { get; } = [];
        public List<(string FolderPath, ImageOrientation Orientation, WallpaperPlaybackOrder PlaybackOrder, int? MinimumImageSideLength, IReadOnlyCollection<string> ExcludedImagePaths)> SelectCalls { get; } = [];

        public string? SelectImage(
            string folderPath,
            ImageOrientation targetOrientation,
            RandomPlaybackState randomPlayback,
            WallpaperPlaybackOrder playbackOrder,
            string? currentWallpaperPath,
            int? minimumImageSideLength = null,
            IReadOnlyCollection<string>? excludedImagePaths = null)
        {
            lock (_lock)
            {
                SelectCalls.Add((
                    folderPath,
                    targetOrientation,
                    playbackOrder,
                    minimumImageSideLength,
                    excludedImagePaths?.ToArray() ?? []));
                var response = _responses.Count > 0 ? _responses.Dequeue() : null;
                if (response != null && playbackOrder == WallpaperPlaybackOrder.Random)
                {
                    randomPlayback.CandidateKey = "fake-cycle";
                    randomPlayback.SeenPaths.Add(response);
                }
                return response;
            }
        }

        public void PrewarmFolder(string folderPath)
        {
            lock (_lock)
            {
                PrewarmCalls.Add(folderPath);
            }
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private AppSettings _settings;

        public FakeSettingsService(AppSettings initialSettings)
        {
            _settings = initialSettings;
        }

        public RuntimeState State { get; private set; } = new();

        public event EventHandler<AppSettings>? SettingsChanged;

        public AppSettings Load() => _settings;

        public void Save(AppSettings settings, bool notifyChanged = true)
        {
            _settings = settings;
            if (notifyChanged)
                SettingsChanged?.Invoke(this, settings);
        }

        public RuntimeState LoadState() => State;

        public void UpdateState(Action<RuntimeState> update)
        {
            lock (this)
                update(State);
        }

        public void RaiseSettingsChanged(AppSettings settings)
        {
            _settings = settings;
            SettingsChanged?.Invoke(this, settings);
        }
    }
}
