using MiraFolio.Core.Models;

namespace MiraFolio.Core.Services;

public interface IImageSelector
{
    /// <summary>
    /// Selects the next image. Random playback mutates <paramref name="randomPlayback"/>
    /// in memory; callers should persist that state only after the wallpaper is applied.
    /// </summary>
    string? SelectImage(
        string folderPath,
        ImageOrientation targetOrientation,
        RandomPlaybackState randomPlayback,
        WallpaperPlaybackOrder playbackOrder,
        string? currentWallpaperPath,
        int? minimumImageSideLength = null,
        IReadOnlyCollection<string>? excludedImagePaths = null);

    /// <summary>
    /// Triggers a background scan of the folder so the image list and dimensions are
    /// ready by the time the first rotation fires. Does not change the wallpaper.
    /// </summary>
    void PrewarmFolder(string folderPath);
}
