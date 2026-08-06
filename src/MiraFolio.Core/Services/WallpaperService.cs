using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MiraFolio.Core.Interop;
using MiraFolio.Core.Models;

namespace MiraFolio.Core.Services;

public class WallpaperService : IWallpaperService
{
    private readonly ILogger<WallpaperService> _logger;
    private readonly DesktopWallpaperHost _desktopWallpaperHost;

    public WallpaperService(
        ILogger<WallpaperService> logger,
        DesktopWallpaperHost desktopWallpaperHost)
    {
        _logger = logger;
        _desktopWallpaperHost = desktopWallpaperHost;
    }

    public void SetWallpaper(string monitorDevicePath, string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            _logger.LogWarning("Wallpaper file not found: {Path}", imagePath);
            throw new FileNotFoundException("Wallpaper file was removed before it could be applied.", imagePath);
        }

        _desktopWallpaperHost.Invoke(desktopWallpaper =>
        {
            try
            {
                Marshal.ThrowExceptionForHR(desktopWallpaper.SetWallpaper(monitorDevicePath, imagePath));
                _logger.LogInformation("Set wallpaper on {Monitor}: {Image}", monitorDevicePath[..Math.Min(30, monitorDevicePath.Length)], Path.GetFileName(imagePath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set wallpaper");
                throw;
            }
        });
    }

    public void SetDisplayMode(WallpaperDisplayMode mode)
    {
        _desktopWallpaperHost.Invoke(desktopWallpaper =>
        {
            try
            {
                Marshal.ThrowExceptionForHR(desktopWallpaper.SetPosition((int)mode));
                _logger.LogInformation("Set wallpaper display mode: {Mode}", mode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set wallpaper display mode: {Mode}", mode);
                throw;
            }
        });
    }

    public string? GetCurrentWallpaper(string monitorDevicePath)
    {
        return _desktopWallpaperHost.Invoke(desktopWallpaper =>
        {
            try
            {
                Marshal.ThrowExceptionForHR(desktopWallpaper.GetWallpaper(monitorDevicePath, out string path));
                return path;
            }
            catch
            {
                return null;
            }
        });
    }

}
