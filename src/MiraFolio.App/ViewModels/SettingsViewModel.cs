using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiraFolio.App.Localization;
using MiraFolio.Core.Models;
using MiraFolio.Core.Services;
using MiraFolio.Core.Utilities;

namespace MiraFolio.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IMonitorService _monitorService;
    private readonly ISettingsService _settingsService;
    private readonly IWallpaperService _wallpaperService;
    private readonly IWallpaperQuickActionService _quickActionService;
    private readonly RotationScheduler _scheduler;

    // Target canvas size (logical pixels); scale is computed to fit within this box.
    private const double CanvasTargetW = 560;
    private const double CanvasTargetH = 320;

    public string AppVersion { get; } = GetAppVersion();

    [ObservableProperty]
    private ObservableCollection<MonitorConfigViewModel> _monitors = new();

    [ObservableProperty]
    private MonitorConfigViewModel? _selectedMonitor;

    [ObservableProperty]
    private GlobalSettingsViewModel _globalSettings = new(new GlobalSettings(), () => { });

    [ObservableProperty]
    private double _monitorCanvasWidth = CanvasTargetW;

    [ObservableProperty]
    private double _monitorCanvasHeight = CanvasTargetH;

    [ObservableProperty]
    private int _removedImageCount;

    public IReadOnlyList<DisplayModeOption> DisplayModeOptions { get; private set; } = [];
    public IReadOnlyList<PlaybackOrderOption> PlaybackOrderOptions { get; private set; } = [];
    public IReadOnlyList<IntervalUnitOption> IntervalUnitOptions { get; private set; } = [];

    private static string GetAppVersion()
    {
        var version = typeof(SettingsViewModel).Assembly.GetName().Version;
        if (version == null)
            return "v—";

        return version.Build >= 0
            ? $"v{version.Major}.{version.Minor}.{version.Build}"
            : $"v{version.Major}.{version.Minor}";
    }

    public SettingsViewModel(
        IMonitorService monitorService,
        ISettingsService settingsService,
        IWallpaperService wallpaperService,
        IWallpaperQuickActionService quickActionService,
        RotationScheduler scheduler)
    {
        _monitorService = monitorService;
        _settingsService = settingsService;
        _wallpaperService = wallpaperService;
        _quickActionService = quickActionService;
        _scheduler = scheduler;

        RefreshLocalizedOptions();
        LoadSettings();

        _monitorService.MonitorsChanged += (_, _) => Application.Current.Dispatcher.Invoke(LoadSettings);
        _settingsService.SettingsChanged += OnSettingsChanged;
        _scheduler.WallpaperChanged     += OnWallpaperChanged;
        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        void Refresh()
        {
            RefreshLocalizedOptions();
            foreach (var monitor in Monitors)
                monitor.RefreshLocalizedText();
        }

        if (Application.Current.Dispatcher.CheckAccess())
            Refresh();
        else
            Application.Current.Dispatcher.Invoke(Refresh);
    }

    private void RefreshLocalizedOptions()
    {
        if (DisplayModeOptions.Count == 0)
        {
            DisplayModeOptions =
            [
                new(LocalizationService.Get("DisplayModeFill"), WallpaperDisplayMode.Fill),
                new(LocalizationService.Get("DisplayModeFit"), WallpaperDisplayMode.Fit),
                new(LocalizationService.Get("DisplayModeStretch"), WallpaperDisplayMode.Stretch),
                new(LocalizationService.Get("DisplayModeTile"), WallpaperDisplayMode.Tile),
                new(LocalizationService.Get("DisplayModeCenter"), WallpaperDisplayMode.Center),
                new(LocalizationService.Get("DisplayModeSpan"), WallpaperDisplayMode.Span)
            ];
            PlaybackOrderOptions =
            [
                new(LocalizationService.Get("PlaybackRandom"), WallpaperPlaybackOrder.Random),
                new(LocalizationService.Get("PlaybackSequential"), WallpaperPlaybackOrder.Sequential),
                new(LocalizationService.Get("PlaybackReverse"), WallpaperPlaybackOrder.ReverseSequential)
            ];
            IntervalUnitOptions =
            [
                new(LocalizationService.Get("UnitSeconds"), WallpaperIntervalUnit.Seconds),
                new(LocalizationService.Get("UnitMinutes"), WallpaperIntervalUnit.Minutes),
                new(LocalizationService.Get("UnitHours"), WallpaperIntervalUnit.Hours)
            ];

            OnPropertyChanged(nameof(DisplayModeOptions));
            OnPropertyChanged(nameof(PlaybackOrderOptions));
            OnPropertyChanged(nameof(IntervalUnitOptions));
            return;
        }

        DisplayModeOptions[0].Label = LocalizationService.Get("DisplayModeFill");
        DisplayModeOptions[1].Label = LocalizationService.Get("DisplayModeFit");
        DisplayModeOptions[2].Label = LocalizationService.Get("DisplayModeStretch");
        DisplayModeOptions[3].Label = LocalizationService.Get("DisplayModeTile");
        DisplayModeOptions[4].Label = LocalizationService.Get("DisplayModeCenter");
        DisplayModeOptions[5].Label = LocalizationService.Get("DisplayModeSpan");

        PlaybackOrderOptions[0].Label = LocalizationService.Get("PlaybackRandom");
        PlaybackOrderOptions[1].Label = LocalizationService.Get("PlaybackSequential");
        PlaybackOrderOptions[2].Label = LocalizationService.Get("PlaybackReverse");

        IntervalUnitOptions[0].Label = LocalizationService.Get("UnitSeconds");
        IntervalUnitOptions[1].Label = LocalizationService.Get("UnitMinutes");
        IntervalUnitOptions[2].Label = LocalizationService.Get("UnitHours");
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        RemovedImageCount = settings.RemovedImages?.Count ?? 0;
        GlobalSettings = new GlobalSettingsViewModel(settings.Global, SaveSettings);

        var monitors = _monitorService.GetMonitors();

        // Compute bounding box → uniform scale to fit CanvasTarget
        double scale      = 1.0;
        int    offsetLeft = 0;
        int    offsetTop  = 0;

        if (monitors.Count > 0)
        {
            offsetLeft = monitors.Min(m => m.Left);
            offsetTop  = monitors.Min(m => m.Top);
            double totalW = monitors.Max(m => m.Left + m.Width)  - offsetLeft;
            double totalH = monitors.Max(m => m.Top  + m.Height) - offsetTop;

            if (totalW > 0 && totalH > 0)
                scale = Math.Min(CanvasTargetW / totalW, CanvasTargetH / totalH);

            MonitorCanvasWidth  = Math.Round(totalW * scale);
            MonitorCanvasHeight = Math.Round(totalH * scale);
        }

        Monitors.Clear();
        foreach (var monitor in monitors)
        {
            var assignment = settings.MonitorAssignments
                .FirstOrDefault(a => a.MonitorDevicePath == monitor.DevicePath)
                ?? new WallpaperAssignment
                {
                    MonitorDevicePath = monitor.DevicePath,
                    FriendlyName      = monitor.FriendlyName
                };

            var currentWallpaper = _wallpaperService.GetCurrentWallpaper(monitor.DevicePath);
            Monitors.Add(new MonitorConfigViewModel(
                monitor,
                assignment,
                scale,
                offsetLeft,
                offsetTop,
                currentWallpaper,
                SaveSettings));
        }

        if (Monitors.Count > 0)
            SelectedMonitor = Monitors[0];
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        void UpdateCount() => RemovedImageCount = settings.RemovedImages?.Count ?? 0;

        if (Application.Current.Dispatcher.CheckAccess())
            UpdateCount();
        else
            Application.Current.Dispatcher.Invoke(UpdateCount);
    }

    private void OnWallpaperChanged(object? sender, WallpaperChangedEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var vm = Monitors.FirstOrDefault(m => m.DevicePath == e.Monitor.DevicePath);
            if (vm != null)
                vm.CurrentWallpaperPath = e.NewWallpaperPath;
        });
    }

    partial void OnSelectedMonitorChanged(MonitorConfigViewModel? value)
    {
        foreach (var m in Monitors)
            m.IsSelected = (m == value);
    }

    [RelayCommand]
    private void SelectMonitor(MonitorConfigViewModel? vm)
    {
        if (vm != null) SelectedMonitor = vm;
    }

    private void SaveSettings()
    {
        var settings = _settingsService.Load();
        settings.Global = GlobalSettings.ToModel(settings.Global);
        settings.MonitorAssignments = MonitorAssignmentMerger.Merge(
            settings.MonitorAssignments,
            Monitors.Select(vm => vm.ToAssignment()));

        _settingsService.Save(settings);
    }

    [RelayCommand]
    private void RotateNow(MonitorConfigViewModel? vm)
    {
        var target = vm ?? SelectedMonitor;
        if (target == null) return;
        _scheduler.RotateNow(target.DevicePath);
    }

    [RelayCommand]
    private void OpenCurrentImageLocation(MonitorConfigViewModel? vm)
    {
        var target = vm ?? SelectedMonitor;
        if (target != null)
            _quickActionService.OpenCurrentImageLocation(target.DevicePath);
    }

    [RelayCommand]
    private void RemoveCurrentImage(MonitorConfigViewModel? vm)
    {
        var target = vm ?? SelectedMonitor;
        if (target != null)
            _quickActionService.ArchiveCurrentImage(target.DevicePath);
    }
}

public sealed partial class DisplayModeOption : ObservableObject
{
    [ObservableProperty]
    private string _label;

    public WallpaperDisplayMode Value { get; }

    public DisplayModeOption(string label, WallpaperDisplayMode value) =>
        (_label, Value) = (label, value);
}

public sealed partial class PlaybackOrderOption : ObservableObject
{
    [ObservableProperty]
    private string _label;

    public WallpaperPlaybackOrder Value { get; }

    public PlaybackOrderOption(string label, WallpaperPlaybackOrder value) =>
        (_label, Value) = (label, value);
}

public sealed partial class IntervalUnitOption : ObservableObject
{
    [ObservableProperty]
    private string _label;

    public WallpaperIntervalUnit Value { get; }

    public IntervalUnitOption(string label, WallpaperIntervalUnit value) =>
        (_label, Value) = (label, value);
}
