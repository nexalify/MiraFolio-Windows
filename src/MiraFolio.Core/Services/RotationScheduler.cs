using System.IO;
using Microsoft.Extensions.Logging;
using MiraFolio.Core.Models;
using MiraFolio.Core.Utilities;

namespace MiraFolio.Core.Services;

public class RotationScheduler : IDisposable
{
    private static readonly TimeSpan DefaultImmediateSelectionRetryWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultImmediateSelectionRetryInterval = TimeSpan.FromMilliseconds(200);

    private readonly IMonitorService _monitorService;
    private readonly IWallpaperService _wallpaperService;
    private readonly IImageSelector _imageSelector;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<RotationScheduler> _logger;

    private readonly Dictionary<string, Timer> _timers = new();
    private readonly Dictionary<string, CancellationTokenSource> _immediateRotationAttempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SemaphoreSlim> _rotationLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _rotationStateLock = new();
    private readonly object _timerLock = new();
    private HashSet<string> _connectedMonitorPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _immediateSelectionRetryWindow;
    private readonly TimeSpan _immediateSelectionRetryInterval;
    private volatile AppSettings _settings;
    private volatile HashSet<string> _removedImagePaths;

    public event EventHandler<WallpaperChangedEventArgs>? WallpaperChanged;

    public RotationScheduler(
        IMonitorService monitorService,
        IWallpaperService wallpaperService,
        IImageSelector imageSelector,
        ISettingsService settingsService,
        ILogger<RotationScheduler> logger,
        TimeSpan? immediateSelectionRetryWindow = null,
        TimeSpan? immediateSelectionRetryInterval = null)
    {
        _monitorService = monitorService;
        _wallpaperService = wallpaperService;
        _imageSelector = imageSelector;
        _settingsService = settingsService;
        _logger = logger;
        _immediateSelectionRetryWindow = immediateSelectionRetryWindow ?? DefaultImmediateSelectionRetryWindow;
        _immediateSelectionRetryInterval = immediateSelectionRetryInterval ?? DefaultImmediateSelectionRetryInterval;
        _settings = _settingsService.Load();
        _removedImagePaths = BuildRemovedImagePathSet(_settings);

        _settingsService.SettingsChanged += OnSettingsChanged;
        _monitorService.MonitorsChanged += OnMonitorsChanged;
    }

    public void Start()
    {
        _settings = _settingsService.Load();
        _removedImagePaths = BuildRemovedImagePathSet(_settings);
        var monitors = _monitorService.GetMonitors();
        UpdateConnectedMonitorPaths(monitors);

        _logger.LogInformation("Scheduler starting — {MonitorCount} monitor(s) detected, {AssignmentCount} assignment(s) configured",
            monitors.Count, _settings.MonitorAssignments.Count);

        // Pre-warm image lists in background; wallpaper rotation starts after the first interval
        foreach (var monitor in monitors)
        {
            var assignment = GetAssignment(monitor.DevicePath);
            if (assignment == null)
            {
                _logger.LogWarning("No assignment configured for monitor [{Name}] {Path}",
                    monitor.FriendlyName, monitor.DevicePath);
                continue;
            }
            if (!assignment.Enabled)
            {
                _logger.LogInformation("Monitor [{Name}] is disabled, skipping", monitor.FriendlyName);
                continue;
            }
            if (string.IsNullOrEmpty(assignment.FolderPath))
            {
                _logger.LogWarning("Monitor [{Name}] has no folder path configured, skipping", monitor.FriendlyName);
                continue;
            }
            _imageSelector.PrewarmFolder(assignment.FolderPath);
        }

        ScheduleTimers(monitors);
    }

    private void ScheduleTimers(IReadOnlyList<MonitorInfo> monitors)
    {
        lock (_timerLock)
        {
            foreach (var timer in _timers.Values)
                timer.Dispose();
            _timers.Clear();

            foreach (var monitor in monitors)
            {
                var assignment = GetAssignment(monitor.DevicePath);
                if (assignment == null || !assignment.Enabled || string.IsNullOrEmpty(assignment.FolderPath))
                    continue;

                var interval = WallpaperIntervalHelper.ToTimeSpan(assignment, _settings.Global.DefaultIntervalMinutes);
                var timer = new Timer(_ => RotateWallpaper(monitor, assignment), null, interval, interval);
                _timers[monitor.DevicePath] = timer;
                _logger.LogInformation("Scheduled rotation for {Monitor} every {Interval}", monitor.FriendlyName, interval);
            }
        }
    }

    private void RotateWallpaper(MonitorInfo monitor, WallpaperAssignment assignment, bool respectFullscreenPause = true)
    {
        RotateWallpaperAsync(
            monitor,
            assignment,
            respectFullscreenPause,
            retryForInitialSelection: false,
            cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task RotateWallpaperAsync(
        MonitorInfo monitor,
        WallpaperAssignment assignment,
        bool respectFullscreenPause,
        bool retryForInitialSelection,
        CancellationToken cancellationToken)
    {
        try
        {
            if (respectFullscreenPause && assignment.PauseWhenFullscreen && FullscreenDetector.IsFullscreenOnMonitor(monitor))
            {
                _logger.LogInformation("Skipping rotation for [{Name}] because a fullscreen window is active on that monitor",
                    monitor.FriendlyName);
                return;
            }

            var rotationLock = GetRotationLock(monitor.DevicePath);
            await rotationLock.WaitAsync(cancellationToken);
            try
            {
                if (!IsCurrentAssignment(assignment))
                {
                    _logger.LogDebug("Skipping stale rotation request for monitor {Monitor}", monitor.FriendlyName);
                    return;
                }

                var state = _settingsService.LoadState();
                var monitorState = state.MonitorStates.FirstOrDefault(s => s.MonitorDevicePath == monitor.DevicePath);
                if (monitorState == null)
                {
                    monitorState = new MonitorState { MonitorDevicePath = monitor.DevicePath };
                    state.MonitorStates.Add(monitorState);
                }
                monitorState.RandomPlayback ??= new RandomPlaybackState();

                var targetOrientation = assignment.SmartOrientationMatching
                    ? monitor.Orientation
                    : ImageOrientation.Landscape;

                var imagePath = retryForInitialSelection
                    ? await SelectImageWithRetryAsync(assignment, targetOrientation, monitorState, cancellationToken)
                    : _imageSelector.SelectImage(
                        assignment.FolderPath,
                        targetOrientation,
                        monitorState.RandomPlayback,
                        assignment.PlaybackOrder,
                        monitorState.CurrentWallpaperPath,
                        GetMinimumImageSideLength(),
                        _removedImagePaths);

                if (imagePath == null)
                {
                    _logger.LogWarning("ImageSelector returned null for monitor [{Name}] — folder: {Folder}, orientation: {Orientation}",
                        monitor.FriendlyName, assignment.FolderPath, targetOrientation);
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentAssignment(assignment))
                {
                    _logger.LogDebug("Discarding stale selected image for monitor {Monitor}", monitor.FriendlyName);
                    return;
                }

                _logger.LogInformation("Rotating [{Name}]: {Image}", monitor.FriendlyName, Path.GetFileName(imagePath));
                _wallpaperService.SetDisplayMode(_settings.Global.DisplayMode);
                _wallpaperService.SetWallpaper(monitor.DevicePath, imagePath);

                // Update the shared state file atomically so simultaneous monitor rotations
                // cannot overwrite each other's entries.
                _settingsService.UpdateState(latestState =>
                {
                    var latestMonitorState = latestState.MonitorStates
                        .FirstOrDefault(s => s.MonitorDevicePath == monitor.DevicePath);
                    if (latestMonitorState == null)
                    {
                        latestMonitorState = new MonitorState { MonitorDevicePath = monitor.DevicePath };
                        latestState.MonitorStates.Add(latestMonitorState);
                    }

                    latestMonitorState.CurrentWallpaperPath = imagePath;
                    latestMonitorState.LastRotationUtc = DateTime.UtcNow;
                    latestMonitorState.RandomPlayback = monitorState.RandomPlayback;
                    latestMonitorState.RecentHistory.Add(imagePath);
                    int maxHistory = Math.Max(0, _settings.Global.HistoryDepth);
                    while (latestMonitorState.RecentHistory.Count > maxHistory)
                        latestMonitorState.RecentHistory.RemoveAt(0);
                });
                WallpaperChanged?.Invoke(this, new WallpaperChangedEventArgs(monitor, imagePath));
            }
            finally
            {
                rotationLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Immediate rotation cancelled for monitor {Monitor}", monitor.FriendlyName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating wallpaper for monitor {Monitor}", monitor.FriendlyName);
        }
    }

    private async Task<string?> SelectImageWithRetryAsync(
        WallpaperAssignment assignment,
        ImageOrientation targetOrientation,
        MonitorState monitorState,
        CancellationToken cancellationToken)
    {
        _imageSelector.PrewarmFolder(assignment.FolderPath);

        var deadline = DateTime.UtcNow + _immediateSelectionRetryWindow;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var imagePath = _imageSelector.SelectImage(
                assignment.FolderPath,
                targetOrientation,
                monitorState.RandomPlayback,
                assignment.PlaybackOrder,
                monitorState.CurrentWallpaperPath,
                GetMinimumImageSideLength(),
                _removedImagePaths);

            if (imagePath != null)
                return imagePath;

            if (DateTime.UtcNow >= deadline)
                return null;

            await Task.Delay(_immediateSelectionRetryInterval, cancellationToken);
        }
    }

    private WallpaperAssignment? GetAssignment(string devicePath) =>
        _settings.MonitorAssignments.FirstOrDefault(a => a.MonitorDevicePath == devicePath);

    private int? GetMinimumImageSideLength() =>
        _settings.Global.LowResolutionFilterEnabled
            ? Math.Max(1, _settings.Global.MinimumImageSideLength)
            : null;

    private void OnSettingsChanged(object? sender, AppSettings newSettings)
    {
        var previousSettings = _settings;
        _settings = newSettings;
        _removedImagePaths = BuildRemovedImagePathSet(newSettings);
        var monitors = _monitorService.GetMonitors();
        ScheduleTimers(monitors);

        foreach (var monitor in monitors)
        {
            var oldAssignment = previousSettings.MonitorAssignments
                .FirstOrDefault(a => a.MonitorDevicePath == monitor.DevicePath);
            var newAssignment = newSettings.MonitorAssignments
                .FirstOrDefault(a => a.MonitorDevicePath == monitor.DevicePath);

            if (newAssignment == null || !newAssignment.Enabled || string.IsNullOrWhiteSpace(newAssignment.FolderPath))
            {
                CancelImmediateRotation(monitor.DevicePath);
                continue;
            }

            bool folderChanged = !string.Equals(
                oldAssignment?.FolderPath ?? string.Empty,
                newAssignment.FolderPath,
                StringComparison.OrdinalIgnoreCase);
            bool enabledNow = !(oldAssignment?.Enabled ?? false) && newAssignment.Enabled;

            if (folderChanged || enabledNow)
                QueueImmediateRotation(monitor, newAssignment, respectFullscreenPause: false);
        }
    }

    private static HashSet<string> BuildRemovedImagePathSet(AppSettings settings) =>
        (settings.RemovedImages ?? [])
        .Where(record => !string.IsNullOrWhiteSpace(record.FilePath))
        .Select(record => record.FilePath)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private void OnMonitorsChanged(object? sender, EventArgs e)
    {
        var monitors = _monitorService.GetMonitors();
        ScheduleTimers(monitors);

        var connectedDevicePaths = monitors.Select(m => m.DevicePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<MonitorInfo> newlyConnectedMonitors;
        List<string> disconnectedAttempts;
        lock (_rotationStateLock)
        {
            newlyConnectedMonitors = monitors
                .Where(monitor => !_connectedMonitorPaths.Contains(monitor.DevicePath))
                .ToList();
            _connectedMonitorPaths = connectedDevicePaths;
            disconnectedAttempts = _immediateRotationAttempts.Keys
                .Where(path => !connectedDevicePaths.Contains(path))
                .ToList();
        }

        foreach (var devicePath in disconnectedAttempts)
            CancelImmediateRotation(devicePath);

        foreach (var monitor in newlyConnectedMonitors)
        {
            var assignment = GetAssignment(monitor.DevicePath);
            if (assignment == null || !assignment.Enabled || string.IsNullOrWhiteSpace(assignment.FolderPath))
                continue;

            _logger.LogInformation("Configured monitor connected; applying wallpaper immediately to {Monitor}", monitor.FriendlyName);
            QueueImmediateRotation(monitor, assignment, respectFullscreenPause: false);
        }
    }

    private void UpdateConnectedMonitorPaths(IReadOnlyList<MonitorInfo> monitors)
    {
        lock (_rotationStateLock)
        {
            _connectedMonitorPaths = monitors
                .Select(monitor => monitor.DevicePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void RotateNow(string monitorDevicePath)
    {
        var monitor = _monitorService.GetMonitors().FirstOrDefault(m => m.DevicePath == monitorDevicePath);
        if (monitor == null) return;
        var assignment = GetAssignment(monitorDevicePath);
        if (assignment == null) return;
        if (!assignment.Enabled || string.IsNullOrWhiteSpace(assignment.FolderPath)) return;

        QueueImmediateRotation(monitor, assignment, respectFullscreenPause: false);
    }

    private void QueueImmediateRotation(MonitorInfo monitor, WallpaperAssignment assignment, bool respectFullscreenPause)
    {
        var cancellationTokenSource = ReplaceImmediateRotationAttempt(monitor.DevicePath);
        _ = Task.Run(async () =>
        {
            try
            {
                await RotateWallpaperAsync(
                    monitor,
                    assignment,
                    respectFullscreenPause,
                    retryForInitialSelection: true,
                    cancellationTokenSource.Token);
            }
            finally
            {
                ClearImmediateRotationAttempt(monitor.DevicePath, cancellationTokenSource);
            }
        });
    }

    private CancellationTokenSource ReplaceImmediateRotationAttempt(string monitorDevicePath)
    {
        lock (_rotationStateLock)
        {
            if (_immediateRotationAttempts.TryGetValue(monitorDevicePath, out var existing))
            {
                existing.Cancel();
                existing.Dispose();
            }

            var replacement = new CancellationTokenSource();
            _immediateRotationAttempts[monitorDevicePath] = replacement;
            return replacement;
        }
    }

    private void CancelImmediateRotation(string monitorDevicePath)
    {
        lock (_rotationStateLock)
        {
            if (!_immediateRotationAttempts.TryGetValue(monitorDevicePath, out var existing)) return;

            _immediateRotationAttempts.Remove(monitorDevicePath);
            existing.Cancel();
            existing.Dispose();
        }
    }

    private void ClearImmediateRotationAttempt(string monitorDevicePath, CancellationTokenSource cancellationTokenSource)
    {
        lock (_rotationStateLock)
        {
            if (!_immediateRotationAttempts.TryGetValue(monitorDevicePath, out var current) ||
                !ReferenceEquals(current, cancellationTokenSource))
                return;

            _immediateRotationAttempts.Remove(monitorDevicePath);
            current.Dispose();
        }
    }

    private SemaphoreSlim GetRotationLock(string monitorDevicePath)
    {
        lock (_rotationStateLock)
        {
            if (_rotationLocks.TryGetValue(monitorDevicePath, out var existing))
                return existing;

            var created = new SemaphoreSlim(1, 1);
            _rotationLocks[monitorDevicePath] = created;
            return created;
        }
    }

    private bool IsCurrentAssignment(WallpaperAssignment assignment)
    {
        var currentAssignment = _settings.MonitorAssignments
            .FirstOrDefault(a => a.MonitorDevicePath == assignment.MonitorDevicePath);
        return currentAssignment != null &&
               ReferenceEquals(currentAssignment, assignment) &&
               currentAssignment.Enabled &&
               !string.IsNullOrWhiteSpace(currentAssignment.FolderPath) &&
               string.Equals(currentAssignment.FolderPath, assignment.FolderPath, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _settingsService.SettingsChanged -= OnSettingsChanged;
        _monitorService.MonitorsChanged -= OnMonitorsChanged;

        lock (_timerLock)
        {
            foreach (var timer in _timers.Values)
                timer.Dispose();
            _timers.Clear();
        }

        lock (_rotationStateLock)
        {
            foreach (var attempt in _immediateRotationAttempts.Values)
            {
                attempt.Cancel();
                attempt.Dispose();
            }
            _immediateRotationAttempts.Clear();

            _rotationLocks.Clear();
            _connectedMonitorPaths.Clear();
        }
    }
}

public class WallpaperChangedEventArgs : EventArgs
{
    public MonitorInfo Monitor { get; }
    public string NewWallpaperPath { get; }

    public WallpaperChangedEventArgs(MonitorInfo monitor, string newWallpaperPath)
    {
        Monitor = monitor;
        NewWallpaperPath = newWallpaperPath;
    }
}
