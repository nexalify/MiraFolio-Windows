namespace MiraFolio.Core.Models;

public class RuntimeState
{
    public List<MonitorState> MonitorStates { get; set; } = new();
}

public class MonitorState
{
    public string MonitorDevicePath { get; set; } = string.Empty;
    public string CurrentWallpaperPath { get; set; } = string.Empty;
    public DateTime LastRotationUtc { get; set; } = DateTime.MinValue;
    public List<string> RecentHistory { get; set; } = new();
    public RandomPlaybackState RandomPlayback { get; set; } = new();
}

public class RandomPlaybackState
{
    public string CandidateKey { get; set; } = string.Empty;
    public List<string> RemainingPaths { get; set; } = new();
    public List<string> SeenPaths { get; set; } = new();
}
