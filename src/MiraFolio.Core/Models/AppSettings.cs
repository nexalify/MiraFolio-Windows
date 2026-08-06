namespace MiraFolio.Core.Models;

public class AppSettings
{
    public int Version { get; set; } = 1;
    public GlobalSettings Global { get; set; } = new();
    public List<WallpaperAssignment> MonitorAssignments { get; set; } = new();
    public List<RemovedImageRecord> RemovedImages { get; set; } = new();
}

public class RemovedImageRecord
{
    public string FilePath { get; set; } = string.Empty;
    public DateTime RemovedAtUtc { get; set; } = DateTime.UtcNow;
}

public class GlobalSettings
{
    public const double DefaultSettingsWindowWidth = 1000;
    public const double DefaultSettingsWindowHeight = 760;
    public const int DefaultMinimumImageSideLength = 1024;

    public bool StartWithWindows { get; set; } = false;
    public int DefaultIntervalMinutes { get; set; } = 30;
    public WallpaperDisplayMode DisplayMode { get; set; } = WallpaperDisplayMode.Fill;
    public int HistoryDepth { get; set; } = 50;
    public bool LowResolutionFilterEnabled { get; set; } = false;
    public int MinimumImageSideLength { get; set; } = DefaultMinimumImageSideLength;
    public bool DesktopQuickActionsEnabled { get; set; } = false;
    public string? LanguageCode { get; set; }
    public double SettingsWindowWidth { get; set; } = DefaultSettingsWindowWidth;
    public double SettingsWindowHeight { get; set; } = DefaultSettingsWindowHeight;
}
