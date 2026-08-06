using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiraFolio.App.Localization;
using MiraFolio.Core.Models;
using MiraFolio.Core.Utilities;

namespace MiraFolio.App.ViewModels;

public partial class MonitorConfigViewModel : ObservableObject
{
    private readonly MonitorInfo _monitor;
    private readonly Action _onChanged;
    private bool _isInitializing;

    public string DevicePath => _monitor.DevicePath;
    public string FriendlyName => _monitor.FriendlyName;
    public string DisplayLabel => LocalizationService.Format("DisplayLabel", _monitor.Index + 1);
    // Canvas layout coords — set by SettingsViewModel after computing scale/offset
    public double ScaledWidth  { get; private set; }
    public double ScaledHeight { get; private set; }
    public double ScaledLeft   { get; private set; }
    public double ScaledTop    { get; private set; }

    [ObservableProperty]
    private string? _currentWallpaperPath;

    [ObservableProperty]
    private string _folderPath = string.Empty;

    [ObservableProperty]
    private int _intervalValue = 30;

    [ObservableProperty]
    private WallpaperIntervalUnit _intervalUnit = WallpaperIntervalUnit.Minutes;

    [ObservableProperty]
    private WallpaperPlaybackOrder _playbackOrder = WallpaperPlaybackOrder.Random;

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    private bool _smartOrientationMatching = true;

    [ObservableProperty]
    private bool _pauseWhenFullscreen;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _currentWallpaperDimensionsText = string.Empty;

    public MonitorConfigViewModel(
        MonitorInfo monitor,
        WallpaperAssignment assignment,
        double scale,
        int offsetLeft,
        int offsetTop,
        string? currentWallpaper,
        Action onChanged)
    {
        _monitor = monitor;
        _onChanged = onChanged;
        _isInitializing = true;

        ScaledWidth  = monitor.Width  * scale;
        ScaledHeight = monitor.Height * scale;
        ScaledLeft   = (monitor.Left - offsetLeft) * scale;
        ScaledTop    = (monitor.Top  - offsetTop)  * scale;

        CurrentWallpaperPath = currentWallpaper;
        RefreshWallpaperMetadata();

        FolderPath             = assignment.FolderPath;
        var (intervalValue, intervalUnit) = WallpaperIntervalHelper.GetDisplayValue(assignment);
        IntervalValue          = intervalValue;
        IntervalUnit           = intervalUnit;
        PlaybackOrder          = assignment.PlaybackOrder;
        Enabled                = assignment.Enabled;
        SmartOrientationMatching = assignment.SmartOrientationMatching;
        PauseWhenFullscreen    = assignment.PauseWhenFullscreen;

        _isInitializing = false;
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = LocalizationService.Get("SelectWallpaperFolder"),
            SelectedPath = FolderPath
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            FolderPath = dialog.SelectedPath;
    }

    public WallpaperAssignment ToAssignment() => new()
    {
        MonitorDevicePath    = DevicePath,
        FriendlyName         = FriendlyName,
        FolderPath           = FolderPath,
        IntervalMinutes      = null,
        IntervalValue        = IntervalValue,
        IntervalUnit         = IntervalUnit,
        PlaybackOrder        = PlaybackOrder,
        Enabled              = Enabled,
        SmartOrientationMatching = SmartOrientationMatching,
        PauseWhenFullscreen  = PauseWhenFullscreen
    };

    partial void OnFolderPathChanged(string value) => NotifyChanged();
    partial void OnCurrentWallpaperPathChanged(string? value) => RefreshWallpaperMetadata();
    partial void OnIntervalValueChanged(int value)
    {
        if (value < 1)
            IntervalValue = 1;

        NotifyChanged();
    }
    partial void OnIntervalUnitChanged(WallpaperIntervalUnit value) => NotifyChanged();
    partial void OnPlaybackOrderChanged(WallpaperPlaybackOrder value) => NotifyChanged();
    partial void OnEnabledChanged(bool value) => NotifyChanged();
    partial void OnSmartOrientationMatchingChanged(bool value) => NotifyChanged();
    partial void OnPauseWhenFullscreenChanged(bool value) => NotifyChanged();

    private void RefreshWallpaperMetadata()
    {
        CurrentWallpaperDimensionsText = LocalizationService.Get("NoData");

        if (string.IsNullOrEmpty(CurrentWallpaperPath) || !File.Exists(CurrentWallpaperPath))
            return;

        try
        {
            if (ImageMetadataHelper.TryReadDimensions(CurrentWallpaperPath, out var width, out var height))
                CurrentWallpaperDimensionsText = $"{width} x {height}";

        }
        catch
        {
            // Keep fallback text when the current wallpaper file disappears or becomes unreadable.
        }
    }

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(DisplayLabel));
        RefreshWallpaperMetadata();
    }

    private void NotifyChanged()
    {
        if (!_isInitializing)
            _onChanged();
    }
}
