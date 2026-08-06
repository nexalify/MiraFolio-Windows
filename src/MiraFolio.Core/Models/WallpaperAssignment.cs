namespace MiraFolio.Core.Models;

public class WallpaperAssignment
{
    public string MonitorDevicePath { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public int? IntervalMinutes { get; set; }
    public int? IntervalValue { get; set; }
    public WallpaperIntervalUnit IntervalUnit { get; set; } = WallpaperIntervalUnit.Minutes;
    public WallpaperPlaybackOrder PlaybackOrder { get; set; } = WallpaperPlaybackOrder.Random;
    public bool Enabled { get; set; } = true;
    public bool SmartOrientationMatching { get; set; } = true;
    public bool PauseWhenFullscreen { get; set; } = false;
}
