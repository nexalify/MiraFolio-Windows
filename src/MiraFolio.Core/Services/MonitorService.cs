using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MiraFolio.Core.Models;
using MiraFolio.Core.Utilities;

namespace MiraFolio.Core.Services;

public class MonitorService : IMonitorService, IDisposable
{
    private const int MaxRefreshRetries = 5;
    private static readonly TimeSpan DisplayChangeRefreshDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan RefreshRetryDelay = TimeSpan.FromSeconds(1);

    private readonly ILogger<MonitorService> _logger;
    private readonly IDesktopMonitorSource _monitorSource;
    private readonly Func<IReadOnlyCollection<MonitorBounds>> _activeBoundsProvider;
    private readonly bool _subscribeToDisplaySettingsChanges;
    private readonly bool _automaticRetriesEnabled;
    private readonly object _refreshLock = new();
    private readonly object _monitorsLock = new();
    private readonly object _retryLock = new();
    private List<MonitorInfo> _monitors = new();
    private System.Threading.Timer? _refreshRetryTimer;
    private int _refreshRetriesRemaining;
    private bool _disposed;

    public event EventHandler? MonitorsChanged;

    public MonitorService(
        ILogger<MonitorService> logger,
        DesktopWallpaperHost desktopWallpaperHost)
        : this(
            logger,
            new DesktopMonitorSource(desktopWallpaperHost),
            GetActiveMonitorBounds,
            subscribeToDisplaySettingsChanges: true,
            automaticRetriesEnabled: true)
    {
    }

    internal MonitorService(
        ILogger<MonitorService> logger,
        IDesktopMonitorSource monitorSource,
        Func<IReadOnlyCollection<MonitorBounds>> activeBoundsProvider,
        bool subscribeToDisplaySettingsChanges = false,
        bool automaticRetriesEnabled = false)
    {
        _logger = logger;
        _monitorSource = monitorSource;
        _activeBoundsProvider = activeBoundsProvider;
        _subscribeToDisplaySettingsChanges = subscribeToDisplaySettingsChanges;
        _automaticRetriesEnabled = automaticRetriesEnabled;

        RefreshMonitors(startRetryCycle: true);
        if (_subscribeToDisplaySettingsChanges)
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        lock (_monitorsLock)
            return _monitors.ToArray();
    }

    public void RefreshMonitors() => RefreshMonitors(startRetryCycle: true);

    private void RefreshMonitors(bool startRetryCycle)
    {
        if (_disposed)
            return;

        bool incomplete;
        lock (_refreshLock)
            incomplete = RefreshMonitorsCore();

        if (!_automaticRetriesEnabled)
            return;

        if (incomplete)
            ScheduleRefreshRetry(startRetryCycle, RefreshRetryDelay);
        else
            CancelRefreshRetries();
    }

    private bool RefreshMonitorsCore()
    {
        var activeBounds = GetActiveBoundsSafely();
        var previousMonitors = GetMonitors();
        var previousByPath = previousMonitors.ToDictionary(
            monitor => monitor.DevicePath,
            StringComparer.OrdinalIgnoreCase);
        var candidates = new List<MonitorCandidate>();
        int failedTargets = 0;

        uint count;
        try
        {
            count = _monitorSource.GetMonitorCount();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read the desktop monitor count; keeping the previous topology");
            return true;
        }

        for (uint i = 0; i < count; i++)
        {
            string? path = null;
            try
            {
                path = _monitorSource.GetDevicePathAt(i);
            }
            catch (Exception ex)
            {
                failedTargets++;
                _logger.LogWarning(ex, "Skipping desktop monitor target {MonitorIndex} because its device path is unavailable", i);
                continue;
            }

            try
            {
                candidates.Add(new MonitorCandidate(path, _monitorSource.GetMonitorBounds(path)));
            }
            catch (Exception ex)
            {
                failedTargets++;
                if (TryReusePreviousActiveCandidate(path, previousByPath, activeBounds, out var previousCandidate))
                {
                    candidates.Add(previousCandidate);
                    _logger.LogWarning(
                        ex,
                        "Monitor target {MonitorIndex} ({MonitorPath}) did not return bounds; reusing its previous active bounds",
                        i,
                        path);
                }
                else
                {
                    _logger.LogWarning(
                        ex,
                        "Skipping unavailable desktop monitor target {MonitorIndex} ({MonitorPath}) because its bounds could not be read",
                        i,
                        path);
                }
            }
        }

        var monitors = MonitorTopologyFilter.FilterToActiveDesktop(candidates, activeBounds);

        if (candidates.Count != monitors.Count || failedTargets > 0)
        {
            _logger.LogInformation(
                "Resolved {MonitorCount} active monitor(s) from {CandidateCount} valid COM target(s); {FailedCount} target(s) failed",
                monitors.Count,
                candidates.Count,
                failedTargets);
        }

        bool incomplete = activeBounds.Count > 0 && monitors.Count < activeBounds.Count;
        if (incomplete && monitors.Count == 0 && previousMonitors.Count > 0)
        {
            _logger.LogWarning(
                "Monitor refresh produced no usable targets while Windows reports {ActiveCount} active screen(s); keeping the previous topology and retrying",
                activeBounds.Count);
            return true;
        }

        bool changed;
        lock (_monitorsLock)
        {
            changed = !_monitors.SequenceEqual(monitors);
            _monitors = monitors.ToList();
        }

        _logger.LogInformation("Detected {Count} monitors", monitors.Count);

        if (changed)
            NotifyMonitorsChanged();

        return incomplete;
    }

    private IReadOnlyCollection<MonitorBounds> GetActiveBoundsSafely()
    {
        try
        {
            return _activeBoundsProvider();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read the active Windows screen bounds; using COM monitor targets without active-screen filtering");
            return Array.Empty<MonitorBounds>();
        }
    }

    private static bool TryReusePreviousActiveCandidate(
        string path,
        IReadOnlyDictionary<string, MonitorInfo> previousByPath,
        IReadOnlyCollection<MonitorBounds> activeBounds,
        out MonitorCandidate candidate)
    {
        candidate = default;
        if (!previousByPath.TryGetValue(path, out var previous))
            return false;

        var bounds = new MonitorBounds(previous.Left, previous.Top, previous.Width, previous.Height);
        if (activeBounds.Count > 0 && !activeBounds.Contains(bounds))
            return false;

        candidate = new MonitorCandidate(path, bounds);
        return true;
    }

    private static IReadOnlyCollection<MonitorBounds> GetActiveMonitorBounds() =>
        Screen.AllScreens
            .Select(screen => new MonitorBounds(
                screen.Bounds.Left,
                screen.Bounds.Top,
                screen.Bounds.Width,
                screen.Bounds.Height))
            .Where(bounds => bounds.Width > 0 && bounds.Height > 0)
            .Distinct()
            .ToArray();

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Windows can raise this event before IDesktopWallpaper has finished updating its
        // target list. Debounce the first read, then retry if COM still trails Screen.AllScreens.
        ScheduleRefreshRetry(startNewCycle: true, DisplayChangeRefreshDelay);
    }

    private void ScheduleRefreshRetry(bool startNewCycle, TimeSpan delay)
    {
        lock (_retryLock)
        {
            if (_disposed)
                return;

            if (startNewCycle)
                _refreshRetriesRemaining = MaxRefreshRetries;

            if (_refreshRetriesRemaining <= 0)
            {
                _logger.LogWarning("Monitor topology is still incomplete after {RetryCount} retries", MaxRefreshRetries);
                return;
            }

            _refreshRetriesRemaining--;
            _refreshRetryTimer ??= new System.Threading.Timer(
                _ => RefreshMonitors(startRetryCycle: false),
                null,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            _refreshRetryTimer.Change(delay, Timeout.InfiniteTimeSpan);
        }
    }

    private void CancelRefreshRetries()
    {
        lock (_retryLock)
        {
            _refreshRetriesRemaining = 0;
            _refreshRetryTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    private void NotifyMonitorsChanged()
    {
        var handlers = MonitorsChanged;
        if (handlers == null)
            return;

        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A monitor change subscriber failed");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_subscribeToDisplaySettingsChanges)
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

        lock (_retryLock)
        {
            _refreshRetryTimer?.Dispose();
            _refreshRetryTimer = null;
            _refreshRetriesRemaining = 0;
        }
    }
}

internal interface IDesktopMonitorSource
{
    uint GetMonitorCount();
    string GetDevicePathAt(uint index);
    MonitorBounds GetMonitorBounds(string devicePath);
}

internal sealed class DesktopMonitorSource(DesktopWallpaperHost desktopWallpaperHost) : IDesktopMonitorSource
{
    public uint GetMonitorCount() => desktopWallpaperHost.Invoke(desktopWallpaper =>
    {
        Marshal.ThrowExceptionForHR(desktopWallpaper.GetMonitorDevicePathCount(out uint count));
        return count;
    });

    public string GetDevicePathAt(uint index) => desktopWallpaperHost.Invoke(desktopWallpaper =>
    {
        Marshal.ThrowExceptionForHR(desktopWallpaper.GetMonitorDevicePathAt(index, out string path));
        return path;
    });

    public MonitorBounds GetMonitorBounds(string devicePath) => desktopWallpaperHost.Invoke(desktopWallpaper =>
    {
        Marshal.ThrowExceptionForHR(desktopWallpaper.GetMonitorRECT(devicePath, out var rect));
        return new MonitorBounds(
            rect.left,
            rect.top,
            rect.right - rect.left,
            rect.bottom - rect.top);
    });
}
