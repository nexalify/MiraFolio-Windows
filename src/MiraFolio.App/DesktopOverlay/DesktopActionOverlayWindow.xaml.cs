using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using MiraFolio.Core.Models;
using MiraFolio.Core.Services;

namespace MiraFolio.App.DesktopOverlay;

public partial class DesktopActionOverlayWindow : Window
{
    private const double OverlayWidthDip = 520;
    private const double OverlayHeightDip = 176;

    private readonly IWallpaperQuickActionService _quickActionService;
    private readonly RotationScheduler _scheduler;
    private readonly ILogger _logger;
    private readonly DispatcherTimer _collapseTimer;

    private MonitorInfo _monitor;
    private HwndSource? _source;
    private nint _windowHandle;
    private bool _expanded;
    private bool _desktopVisible;

    public DesktopActionOverlayWindow(
        MonitorInfo monitor,
        IWallpaperQuickActionService quickActionService,
        RotationScheduler scheduler,
        ILogger logger)
    {
        InitializeComponent();
        _monitor = monitor;
        _quickActionService = quickActionService;
        _scheduler = scheduler;
        _logger = logger;

        _collapseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            SetDesktopVisible(false);
        };

        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    internal nint WindowHandle
    {
        get
        {
            if (_windowHandle == nint.Zero)
                new WindowInteropHelper(this).EnsureHandle();
            return _windowHandle;
        }
    }

    internal DesktopOverlayNativeMethods.Rect ScreenBounds
    {
        get
        {
            _ = WindowHandle;
            return CalculateScreenBounds();
        }
    }

    internal bool ContainsPointer(DesktopOverlayNativeMethods.Point screenPoint) =>
        DesktopOverlayNativeMethods.IsPointWithinWindowClient(WindowHandle, screenPoint);

    public void UpdateMonitor(MonitorInfo monitor)
    {
        _monitor = monitor;
        if (_windowHandle != nint.Zero)
            ApplyBounds();
    }

    public void SetDesktopVisible(bool visible)
    {
        if (_desktopVisible == visible)
            return;

        _desktopVisible = visible;
        if (visible)
        {
            if (!IsVisible)
                Show();

            if (_windowHandle != nint.Zero)
            {
                DesktopOverlayNativeMethods.ShowWindow(
                    _windowHandle,
                    DesktopOverlayNativeMethods.SwShowNoActivate);
                ApplyBounds();
            }
            ExpandOverlay();
        }
        else
        {
            CollapseOverlay(immediate: true);
            Hide();
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        var extendedStyle = DesktopOverlayNativeMethods.GetWindowLongPtr(
            _windowHandle,
            DesktopOverlayNativeMethods.GwlExStyle).ToInt64();
        extendedStyle |= DesktopOverlayNativeMethods.WsExToolWindow |
                         DesktopOverlayNativeMethods.WsExNoActivate;
        DesktopOverlayNativeMethods.SetWindowLongPtr(
            _windowHandle,
            DesktopOverlayNativeMethods.GwlExStyle,
            new nint(extendedStyle));

        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WindowMessageHook);
        ApplyBounds();
    }

    private nint WindowMessageHook(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message == DesktopOverlayNativeMethods.WmMouseActivate)
        {
            handled = true;
            return new nint(DesktopOverlayNativeMethods.MaNoActivate);
        }

        if (message == DesktopOverlayNativeMethods.WmDpiChanged)
            Dispatcher.BeginInvoke(ApplyBounds, DispatcherPriority.Loaded);

        return nint.Zero;
    }

    private void Overlay_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _collapseTimer.Stop();
        ExpandOverlay();
    }

    private void Overlay_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        ScheduleHide();
    }

    internal void ScheduleHide()
    {
        if (_desktopVisible && !_collapseTimer.IsEnabled)
            _collapseTimer.Start();
    }

    private void ExpandOverlay()
    {
        if (_expanded || !_desktopVisible)
            return;

        _expanded = true;
        ActionPanel.BeginAnimation(OpacityProperty, null);
        ActionPanel.Visibility = Visibility.Visible;
        ActionPanel.Opacity = 1;
    }

    private void CollapseOverlay(bool immediate = false)
    {
        _collapseTimer.Stop();
        if (!_expanded && ActionPanel.Visibility == Visibility.Collapsed)
            return;

        _expanded = false;

        if (immediate)
        {
            ActionPanel.BeginAnimation(OpacityProperty, null);
            ActionPanel.Opacity = 0;
            ActionPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var animation = new DoubleAnimation(ActionPanel.Opacity, 0, TimeSpan.FromMilliseconds(100));
        animation.Completed += (_, _) =>
        {
            if (_expanded)
                return;

            ActionPanel.Visibility = Visibility.Collapsed;
        };
        ActionPanel.BeginAnimation(OpacityProperty, animation);
    }

    private void ApplyBounds()
    {
        if (_windowHandle == nint.Zero)
            return;

        var bounds = CalculateScreenBounds();

        var flags = DesktopOverlayNativeMethods.SwpNoActivate |
                    DesktopOverlayNativeMethods.SwpNoOwnerZOrder;
        if (_desktopVisible)
            flags |= DesktopOverlayNativeMethods.SwpShowWindow;

        if (!DesktopOverlayNativeMethods.SetWindowPos(
                _windowHandle,
                DesktopOverlayNativeMethods.HwndTopmost,
                bounds.Left,
                bounds.Top,
                bounds.Right - bounds.Left,
                bounds.Bottom - bounds.Top,
                flags))
        {
            _logger.LogWarning("Failed to position the desktop quick actions window for {Monitor}", _monitor.DevicePath);
        }
    }

    private DesktopOverlayNativeMethods.Rect CalculateScreenBounds()
    {
        var dpi = DesktopOverlayNativeMethods.GetDpiForWindow(_windowHandle);
        if (dpi == 0)
            dpi = 96;

        var scale = dpi / 96d;
        var widthPixels = Math.Max(1, (int)Math.Round(OverlayWidthDip * scale));
        var heightPixels = Math.Max(1, (int)Math.Round(OverlayHeightDip * scale));
        var left = _monitor.Left + ((_monitor.Width - widthPixels) / 2);
        var top = _monitor.Top + ((_monitor.Height - heightPixels) / 2);
        return new DesktopOverlayNativeMethods.Rect(
            left,
            top,
            left + widthPixels,
            top + heightPixels);
    }

    private void RotateNow_Click(object sender, RoutedEventArgs e)
    {
        _scheduler.RotateNow(_monitor.DevicePath);
        CollapseOverlay();
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        _quickActionService.OpenCurrentImageLocation(_monitor.DevicePath);
        CollapseOverlay();
    }

    private void RemoveCurrentImage_Click(object sender, RoutedEventArgs e)
    {
        _quickActionService.ArchiveCurrentImage(_monitor.DevicePath);
        CollapseOverlay();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _collapseTimer.Stop();
        _source?.RemoveHook(WindowMessageHook);
        _source = null;
        _windowHandle = nint.Zero;
    }
}
