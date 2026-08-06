using Microsoft.Extensions.Logging;
using MiraFolio.Core.Models;
using MiraFolio.Core.Utilities;

namespace MiraFolio.Core.Services;

public sealed class WallpaperQuickActionService : IWallpaperQuickActionService
{
    private readonly IMonitorService _monitorService;
    private readonly ISettingsService _settingsService;
    private readonly IWallpaperService _wallpaperService;
    private readonly ILogger<WallpaperQuickActionService> _logger;
    private readonly Action<string> _rotateNow;
    private readonly Action<string> _openAndSelectFile;
    private readonly Action<string> _openFolder;

    public WallpaperQuickActionService(
        IMonitorService monitorService,
        ISettingsService settingsService,
        IWallpaperService wallpaperService,
        RotationScheduler scheduler,
        ILogger<WallpaperQuickActionService> logger)
        : this(
            monitorService,
            settingsService,
            wallpaperService,
            logger,
            scheduler.RotateNow,
            FileExplorerHelper.OpenAndSelectFile,
            FileExplorerHelper.OpenFolder)
    {
    }

    internal WallpaperQuickActionService(
        IMonitorService monitorService,
        ISettingsService settingsService,
        IWallpaperService wallpaperService,
        ILogger<WallpaperQuickActionService> logger,
        Action<string> rotateNow,
        Action<string> openAndSelectFile,
        Action<string> openFolder)
    {
        _monitorService = monitorService;
        _settingsService = settingsService;
        _wallpaperService = wallpaperService;
        _logger = logger;
        _rotateNow = rotateNow;
        _openAndSelectFile = openAndSelectFile;
        _openFolder = openFolder;
    }

    public bool OpenCurrentImageLocation(string monitorDevicePath)
    {
        try
        {
            var imagePath = _wallpaperService.GetCurrentWallpaper(monitorDevicePath);
            if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
            {
                _openAndSelectFile(imagePath);
                return true;
            }

            var folderPath = _settingsService.Load().MonitorAssignments
                .FirstOrDefault(assignment => string.Equals(
                    assignment.MonitorDevicePath,
                    monitorDevicePath,
                    StringComparison.OrdinalIgnoreCase))
                ?.FolderPath;
            if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
            {
                _openFolder(folderPath);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open the current wallpaper location for {Monitor}", monitorDevicePath);
        }

        return false;
    }

    public bool ArchiveCurrentImage(string monitorDevicePath)
    {
        try
        {
            var imagePath = NormalizeImagePath(_wallpaperService.GetCurrentWallpaper(monitorDevicePath));
            if (string.IsNullOrWhiteSpace(imagePath))
                return false;

            var settings = _settingsService.Load();
            settings.RemovedImages ??= [];

            if (!settings.RemovedImages.Any(record => string.Equals(
                    NormalizeImagePath(record.FilePath),
                    imagePath,
                    StringComparison.OrdinalIgnoreCase)))
            {
                settings.RemovedImages.Add(new RemovedImageRecord
                {
                    FilePath = imagePath,
                    RemovedAtUtc = DateTime.UtcNow
                });
                _settingsService.Save(settings);
            }

            foreach (var monitor in _monitorService.GetMonitors())
            {
                var currentPath = NormalizeImagePath(_wallpaperService.GetCurrentWallpaper(monitor.DevicePath));
                if (string.Equals(currentPath, imagePath, StringComparison.OrdinalIgnoreCase))
                    _rotateNow(monitor.DevicePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to archive the current wallpaper for {Monitor}", monitorDevicePath);
            return false;
        }
    }

    private static string NormalizeImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
}
