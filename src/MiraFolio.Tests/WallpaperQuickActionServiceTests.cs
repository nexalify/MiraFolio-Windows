using Microsoft.Extensions.Logging.Abstractions;
using MiraFolio.Core.Models;
using MiraFolio.Core.Services;
using Xunit;

namespace MiraFolio.Tests;

public sealed class WallpaperQuickActionServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "MiraFolioTests_QuickActions_" + Guid.NewGuid());

    public WallpaperQuickActionServiceTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void ArchiveCurrentImage_MatchingDisplays_ArchivesOnceAndRotatesAll()
    {
        var imagePath = Path.Combine(_tempDirectory, "Wallpaper.jpg");
        var settings = new FakeSettingsService();
        var monitors = new FakeMonitorService(
            new MonitorInfo(0, "display-1", 0, 0, 1920, 1080),
            new MonitorInfo(1, "display-2", 1920, 0, 1920, 1080));
        var wallpaper = new FakeWallpaperService(new Dictionary<string, string?>
        {
            ["display-1"] = imagePath,
            ["display-2"] = imagePath.ToUpperInvariant()
        });
        var rotated = new List<string>();
        var service = CreateService(monitors, settings, wallpaper, rotated.Add);

        Assert.True(service.ArchiveCurrentImage("display-1"));
        Assert.True(service.ArchiveCurrentImage("display-1"));

        Assert.Single(settings.Settings.RemovedImages);
        Assert.Equal(Path.GetFullPath(imagePath), settings.Settings.RemovedImages[0].FilePath);
        Assert.Equal(4, rotated.Count);
        Assert.Equal(2, rotated.Count(path => path == "display-1"));
        Assert.Equal(2, rotated.Count(path => path == "display-2"));
    }

    [Fact]
    public void ArchiveCurrentImage_NoCurrentPath_DoesNothing()
    {
        var settings = new FakeSettingsService();
        var monitors = new FakeMonitorService(new MonitorInfo(0, "display-1", 0, 0, 1920, 1080));
        var wallpaper = new FakeWallpaperService(new Dictionary<string, string?>());
        var rotated = new List<string>();
        var service = CreateService(monitors, settings, wallpaper, rotated.Add);

        Assert.False(service.ArchiveCurrentImage("display-1"));
        Assert.Empty(settings.Settings.RemovedImages);
        Assert.Empty(rotated);
    }

    [Fact]
    public void OpenCurrentImageLocation_ExistingImage_SelectsFile()
    {
        var imagePath = Path.Combine(_tempDirectory, "wallpaper.jpg");
        File.WriteAllText(imagePath, "test");
        var selectedFiles = new List<string>();
        var openedFolders = new List<string>();
        var service = CreateService(
            new FakeMonitorService(),
            new FakeSettingsService(),
            new FakeWallpaperService(new Dictionary<string, string?> { ["display-1"] = imagePath }),
            _ => { },
            selectedFiles.Add,
            openedFolders.Add);

        Assert.True(service.OpenCurrentImageLocation("display-1"));
        Assert.Equal(imagePath, Assert.Single(selectedFiles));
        Assert.Empty(openedFolders);
    }

    [Fact]
    public void OpenCurrentImageLocation_MissingImage_OpensConfiguredFolder()
    {
        var settings = new FakeSettingsService
        {
            Settings = new AppSettings
            {
                MonitorAssignments =
                [
                    new WallpaperAssignment
                    {
                        MonitorDevicePath = "DISPLAY-1",
                        FolderPath = _tempDirectory
                    }
                ]
            }
        };
        var openedFolders = new List<string>();
        var service = CreateService(
            new FakeMonitorService(),
            settings,
            new FakeWallpaperService(new Dictionary<string, string?>
            {
                ["display-1"] = Path.Combine(_tempDirectory, "missing.jpg")
            }),
            _ => { },
            _ => { },
            openedFolders.Add);

        Assert.True(service.OpenCurrentImageLocation("display-1"));
        Assert.Equal(_tempDirectory, Assert.Single(openedFolders));
    }

    [Fact]
    public void OpenCurrentImageLocation_NoValidFileOrFolder_ReturnsFalse()
    {
        var service = CreateService(
            new FakeMonitorService(),
            new FakeSettingsService(),
            new FakeWallpaperService(new Dictionary<string, string?>()),
            _ => { });

        Assert.False(service.OpenCurrentImageLocation("display-1"));
    }

    private static WallpaperQuickActionService CreateService(
        IMonitorService monitorService,
        ISettingsService settingsService,
        IWallpaperService wallpaperService,
        Action<string> rotateNow,
        Action<string>? openAndSelectFile = null,
        Action<string>? openFolder = null) =>
        new(
            monitorService,
            settingsService,
            wallpaperService,
            NullLogger<WallpaperQuickActionService>.Instance,
            rotateNow,
            openAndSelectFile ?? (_ => { }),
            openFolder ?? (_ => { }));

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private sealed class FakeMonitorService(params MonitorInfo[] monitors) : IMonitorService
    {
        public IReadOnlyList<MonitorInfo> GetMonitors() => monitors;
        public event EventHandler? MonitorsChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class FakeWallpaperService(IReadOnlyDictionary<string, string?> currentPaths) : IWallpaperService
    {
        public void SetDisplayMode(WallpaperDisplayMode mode) { }
        public void SetWallpaper(string monitorDevicePath, string imagePath) { }
        public string? GetCurrentWallpaper(string monitorDevicePath) =>
            currentPaths.TryGetValue(monitorDevicePath, out var path) ? path : null;
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; set; } = new();
        public AppSettings Load() => Settings;
        public void Save(AppSettings settings, bool notifyChanged = true)
        {
            Settings = settings;
            if (notifyChanged)
                SettingsChanged?.Invoke(this, settings);
        }

        public RuntimeState LoadState() => new();
        public void UpdateState(Action<RuntimeState> update) { }
        public event EventHandler<AppSettings>? SettingsChanged;
    }
}
