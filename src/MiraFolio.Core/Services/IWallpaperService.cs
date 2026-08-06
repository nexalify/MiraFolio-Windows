namespace MiraFolio.Core.Services;
using MiraFolio.Core.Models;

public interface IWallpaperService
{
    void SetDisplayMode(WallpaperDisplayMode mode);
    void SetWallpaper(string monitorDevicePath, string imagePath);
    string? GetCurrentWallpaper(string monitorDevicePath);
}
