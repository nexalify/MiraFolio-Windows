namespace MiraFolio.Core.Services;

public interface IWallpaperQuickActionService
{
    bool OpenCurrentImageLocation(string monitorDevicePath);
    bool ArchiveCurrentImage(string monitorDevicePath);
}
