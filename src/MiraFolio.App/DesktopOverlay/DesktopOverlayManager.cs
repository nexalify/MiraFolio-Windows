using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using MiraFolio.Core.Models;
using MiraFolio.Core.Services;

namespace MiraFolio.App.DesktopOverlay;

public sealed class DesktopOverlayManager : IDisposable
{
    private readonly IMonitorService _monitorService;
    private readonly ISettingsService _settingsService;
    private readonly IWallpaperQuickActionService _quickActionService;
    private readonly RotationScheduler _scheduler;
    private readonly ILogger<DesktopOverlayManager> _logger;
    private readonly Dictionary<string, DesktopActionOverlayWindow> _windows =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly DesktopOverlayNativeMethods.WinEventDelegate _foregroundChangedCallback;
    private readonly DispatcherTimer _pointerPollTimer;

    private nint _foregroundHook;
    private bool _started;
    private bool _enabled;
    private bool _disposed;

    public DesktopOverlayManager(
        IMonitorService monitorService,
        ISettingsService settingsService,
        IWallpaperQuickActionService quickActionService,
        RotationScheduler scheduler,
        ILogger<DesktopOverlayManager> logger)
    {
        _monitorService = monitorService;
        _settingsService = settingsService;
        _quickActionService = quickActionService;
        _scheduler = scheduler;
        _logger = logger;
        _foregroundChangedCallback = OnForegroundChanged;
        _pointerPollTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _pointerPollTimer.Tick += (_, _) => UpdateVisibility();
    }

    public void Start()
    {
        if (_started || _disposed)
            return;

        _started = true;
        _settingsService.SettingsChanged += OnSettingsChanged;
        _monitorService.MonitorsChanged += OnMonitorsChanged;
        ApplyEnabledState(_settingsService.Load().Global.DesktopQuickActionsEnabled);
    }

    private void OnSettingsChanged(object? sender, AppSettings settings) =>
        Dispatch(() => ApplyEnabledState(settings.Global.DesktopQuickActionsEnabled));

    private void OnMonitorsChanged(object? sender, EventArgs e) =>
        Dispatch(() =>
        {
            if (_enabled)
            {
                ReconcileWindows();
                UpdateVisibility();
            }
        });

    private void ApplyEnabledState(bool enabled)
    {
        if (_disposed || _enabled == enabled)
            return;

        _enabled = enabled;
        if (enabled)
        {
            RegisterForegroundHook();
            ReconcileWindows();
            _pointerPollTimer.Start();
            UpdateVisibility();
            _logger.LogInformation("Desktop quick actions enabled for {MonitorCount} monitor(s)", _windows.Count);
        }
        else
        {
            _pointerPollTimer.Stop();
            UnregisterForegroundHook();
            CloseAllWindows();
            _logger.LogInformation("Desktop quick actions disabled");
        }
    }

    private void RegisterForegroundHook()
    {
        if (_foregroundHook != nint.Zero)
            return;

        _foregroundHook = DesktopOverlayNativeMethods.SetWinEventHook(
            DesktopOverlayNativeMethods.EventSystemForeground,
            DesktopOverlayNativeMethods.EventSystemForeground,
            nint.Zero,
            _foregroundChangedCallback,
            0,
            0,
            DesktopOverlayNativeMethods.WineventOutOfContext);

        if (_foregroundHook == nint.Zero)
            _logger.LogWarning("Unable to register the desktop foreground event hook");
    }

    private void UnregisterForegroundHook()
    {
        if (_foregroundHook == nint.Zero)
            return;

        DesktopOverlayNativeMethods.UnhookWinEvent(_foregroundHook);
        _foregroundHook = nint.Zero;
    }

    private void OnForegroundChanged(
        nint winEventHook,
        uint eventType,
        nint hwnd,
        int idObject,
        int idChild,
        uint eventThread,
        uint eventTime) => Dispatch(UpdateVisibility);

    private void ReconcileWindows()
    {
        var monitors = _monitorService.GetMonitors();
        var connectedPaths = monitors
            .Select(monitor => monitor.DevicePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var removedPath in _windows.Keys.Where(path => !connectedPaths.Contains(path)).ToArray())
        {
            _windows[removedPath].Close();
            _windows.Remove(removedPath);
        }

        foreach (var monitor in monitors)
        {
            if (_windows.TryGetValue(monitor.DevicePath, out var existingWindow))
            {
                existingWindow.UpdateMonitor(monitor);
                continue;
            }

            _windows[monitor.DevicePath] = new DesktopActionOverlayWindow(
                monitor,
                _quickActionService,
                _scheduler,
                _logger);
        }
    }

    private void UpdateVisibility()
    {
        if (!_enabled || _disposed)
            return;

        if (!DesktopOverlayNativeMethods.GetCursorPos(out var pointer))
        {
            foreach (var window in _windows.Values)
                window.SetDesktopVisible(false);
            return;
        }

        var ignoredWindows = _windows.Values
            .Select(window => window.WindowHandle)
            .Where(handle => handle != nint.Zero)
            .ToHashSet();

        foreach (var window in _windows.Values)
        {
            var bounds = window.ScreenBounds;
            var desktopUncovered = DesktopOverlayNativeMethods.IsDesktopAreaUncovered(
                bounds,
                ignoredWindows);

            if (!desktopUncovered)
                window.SetDesktopVisible(false);
            else if (window.ContainsPointer(pointer))
                window.SetDesktopVisible(true);
            else
                window.ScheduleHide();
        }
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted)
            return;

        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action);
    }

    private void CloseAllWindows()
    {
        foreach (var window in _windows.Values)
            window.Close();
        _windows.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _pointerPollTimer.Stop();
        _settingsService.SettingsChanged -= OnSettingsChanged;
        _monitorService.MonitorsChanged -= OnMonitorsChanged;
        UnregisterForegroundHook();
        CloseAllWindows();
    }
}
