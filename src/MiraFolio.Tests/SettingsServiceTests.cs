using Microsoft.Extensions.Logging.Abstractions;
using MiraFolio.Core.Models;
using MiraFolio.Core.Services;
using MiraFolio.Core.Utilities;
using Xunit;

namespace MiraFolio.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MiraFolioTests_Settings_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _service = new SettingsService(
            NullLogger<SettingsService>.Instance,
            Path.Combine(_tempDir, "MiraFolio"));
    }

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        var settings = _service.Load();
        Assert.NotNull(settings);
        Assert.NotNull(settings.Global);
        Assert.NotNull(settings.MonitorAssignments);
        Assert.Equal(1, settings.Version);
        Assert.False(settings.Global.DesktopQuickActionsEnabled);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var original = new AppSettings
        {
            Global = new GlobalSettings
            {
                DefaultIntervalMinutes = 15,
                HistoryDepth = 20,
                StartWithWindows = true,
                DesktopQuickActionsEnabled = true,
                LanguageCode = "ja"
            },
            MonitorAssignments =
            [
                new WallpaperAssignment
                {
                    MonitorDevicePath = "test-path",
                    FolderPath = @"C:\Wallpapers",
                    IntervalValue = 45,
                    IntervalUnit = WallpaperIntervalUnit.Seconds,
                    PlaybackOrder = WallpaperPlaybackOrder.ReverseSequential
                }
            ],
            RemovedImages =
            [
                new RemovedImageRecord
                {
                    FilePath = @"C:\Wallpapers\removed.jpg",
                    RemovedAtUtc = new DateTime(2026, 8, 3, 12, 30, 0, DateTimeKind.Utc)
                }
            ]
        };

        _service.Save(original);
        var loaded = _service.Load();

        Assert.Equal(15, loaded.Global.DefaultIntervalMinutes);
        Assert.Equal(20, loaded.Global.HistoryDepth);
        Assert.True(loaded.Global.StartWithWindows);
        Assert.True(loaded.Global.DesktopQuickActionsEnabled);
        Assert.Equal("ja", loaded.Global.LanguageCode);
        Assert.Single(loaded.MonitorAssignments);
        Assert.Equal("test-path", loaded.MonitorAssignments[0].MonitorDevicePath);
        Assert.Equal(45, loaded.MonitorAssignments[0].IntervalValue);
        Assert.Equal(WallpaperIntervalUnit.Seconds, loaded.MonitorAssignments[0].IntervalUnit);
        Assert.Equal(WallpaperPlaybackOrder.ReverseSequential, loaded.MonitorAssignments[0].PlaybackOrder);
        Assert.Single(loaded.RemovedImages);
        Assert.Equal(@"C:\Wallpapers\removed.jpg", loaded.RemovedImages[0].FilePath);
        Assert.Equal(new DateTime(2026, 8, 3, 12, 30, 0, DateTimeKind.Utc), loaded.RemovedImages[0].RemovedAtUtc);
    }

    [Fact]
    public void Save_FiresSettingsChangedEvent()
    {
        AppSettings? received = null;
        _service.SettingsChanged += (_, s) => received = s;

        var settings = new AppSettings();
        _service.Save(settings);

        Assert.NotNull(received);
    }

    [Fact]
    public void Save_NotificationSuppressed_DoesNotFireSettingsChangedEvent()
    {
        var eventCount = 0;
        _service.SettingsChanged += (_, _) => eventCount++;

        _service.Save(new AppSettings(), notifyChanged: false);

        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void Save_FailingSubscriber_DoesNotBlockRemainingSubscribers()
    {
        var secondSubscriberCalled = false;
        _service.SettingsChanged += (_, _) => throw new InvalidOperationException("test subscriber failure");
        _service.SettingsChanged += (_, _) => secondSubscriberCalled = true;

        _service.Save(new AppSettings());

        Assert.True(secondSubscriberCalled);
    }

    [Fact]
    public void UpdateState_ConcurrentUpdates_PreservesEveryMonitor()
    {
        Parallel.For(0, 20, index =>
        {
            _service.UpdateState(state =>
            {
                state.MonitorStates.Add(new MonitorState
                {
                    MonitorDevicePath = $"display-{index}"
                });
            });
        });

        var state = _service.LoadState();
        Assert.Equal(20, state.MonitorStates.Count);
        Assert.Equal(20, state.MonitorStates.Select(item => item.MonitorDevicePath).Distinct().Count());
    }

    [Fact]
    public void UpdateState_RandomPlaybackQueue_RoundTrips()
    {
        _service.UpdateState(state => state.MonitorStates.Add(new MonitorState
        {
            MonitorDevicePath = "display-1",
            RandomPlayback = new RandomPlaybackState
            {
                CandidateKey = "folder|landscape|none",
                RemainingPaths = [@"C:\Wallpapers\b.jpg"],
                SeenPaths = [@"C:\Wallpapers\a.jpg"]
            }
        }));

        var randomPlayback = _service.LoadState().MonitorStates.Single().RandomPlayback;
        Assert.Equal("folder|landscape|none", randomPlayback.CandidateKey);
        Assert.Equal(@"C:\Wallpapers\b.jpg", randomPlayback.RemainingPaths.Single());
        Assert.Equal(@"C:\Wallpapers\a.jpg", randomPlayback.SeenPaths.Single());
    }

    [Fact]
    public void MergeAssignments_KeepsOfflineMonitorSettings()
    {
        var existing = new[]
        {
            new WallpaperAssignment
            {
                MonitorDevicePath = "display-1",
                FolderPath = @"C:\Wallpapers\One",
                IntervalValue = 10,
                IntervalUnit = WallpaperIntervalUnit.Minutes
            },
            new WallpaperAssignment
            {
                MonitorDevicePath = "display-2",
                FolderPath = @"C:\Wallpapers\Two",
                IntervalValue = 20,
                IntervalUnit = WallpaperIntervalUnit.Hours
            }
        };

        var current = new[]
        {
            new WallpaperAssignment
            {
                MonitorDevicePath = "display-1",
                FolderPath = @"D:\Updated",
                IntervalValue = 30,
                IntervalUnit = WallpaperIntervalUnit.Seconds
            }
        };

        var merged = MonitorAssignmentMerger.Merge(existing, current);

        Assert.Equal(2, merged.Count);
        Assert.Equal(@"D:\Updated", merged.Single(x => x.MonitorDevicePath == "display-1").FolderPath);
        Assert.Equal(30, merged.Single(x => x.MonitorDevicePath == "display-1").IntervalValue);
        Assert.Equal(WallpaperIntervalUnit.Seconds, merged.Single(x => x.MonitorDevicePath == "display-1").IntervalUnit);
        Assert.Equal(@"C:\Wallpapers\Two", merged.Single(x => x.MonitorDevicePath == "display-2").FolderPath);
        Assert.Equal(20, merged.Single(x => x.MonitorDevicePath == "display-2").IntervalValue);
        Assert.Equal(WallpaperIntervalUnit.Hours, merged.Single(x => x.MonitorDevicePath == "display-2").IntervalUnit);
    }

    [Fact]
    public void MergeAssignments_AddsNewOnlineMonitorSettings()
    {
        var existing = new[]
        {
            new WallpaperAssignment
            {
                MonitorDevicePath = "display-1",
                FolderPath = @"C:\Wallpapers\One"
            }
        };

        var current = new[]
        {
            new WallpaperAssignment
            {
                MonitorDevicePath = "display-1",
                FolderPath = @"D:\Updated"
            },
            new WallpaperAssignment
            {
                MonitorDevicePath = "display-3",
                FolderPath = @"E:\New"
            }
        };

        var merged = MonitorAssignmentMerger.Merge(existing, current);

        Assert.Equal(2, merged.Count);
        Assert.Equal(@"D:\Updated", merged.Single(x => x.MonitorDevicePath == "display-1").FolderPath);
        Assert.Equal(@"E:\New", merged.Single(x => x.MonitorDevicePath == "display-3").FolderPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
