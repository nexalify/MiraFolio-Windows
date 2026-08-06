using CommunityToolkit.Mvvm.ComponentModel;
using MiraFolio.Core.Models;

namespace MiraFolio.App.ViewModels;

public partial class GlobalSettingsViewModel : ObservableObject
{
    private readonly Action _onChanged;
    private bool _isInitializing;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private WallpaperDisplayMode _displayMode = WallpaperDisplayMode.Fill;

    [ObservableProperty]
    private bool _lowResolutionFilterEnabled;

    [ObservableProperty]
    private int _minimumImageSideLength = GlobalSettings.DefaultMinimumImageSideLength;

    [ObservableProperty]
    private bool _desktopQuickActionsEnabled;

    public GlobalSettingsViewModel(GlobalSettings settings, Action onChanged)
    {
        _onChanged = onChanged;
        _isInitializing = true;

        StartWithWindows = settings.StartWithWindows;
        DisplayMode = settings.DisplayMode;
        LowResolutionFilterEnabled = settings.LowResolutionFilterEnabled;
        MinimumImageSideLength = settings.MinimumImageSideLength;
        DesktopQuickActionsEnabled = settings.DesktopQuickActionsEnabled;

        _isInitializing = false;
    }

    public GlobalSettings ToModel(GlobalSettings existing) => new()
    {
        StartWithWindows = StartWithWindows,
        DefaultIntervalMinutes = existing.DefaultIntervalMinutes,
        DisplayMode = DisplayMode,
        HistoryDepth = existing.HistoryDepth,
        LowResolutionFilterEnabled = LowResolutionFilterEnabled,
        MinimumImageSideLength = MinimumImageSideLength,
        DesktopQuickActionsEnabled = DesktopQuickActionsEnabled,
        LanguageCode = existing.LanguageCode,
        SettingsWindowWidth = existing.SettingsWindowWidth,
        SettingsWindowHeight = existing.SettingsWindowHeight
    };

    partial void OnStartWithWindowsChanged(bool value) => NotifyChanged();
    partial void OnDisplayModeChanged(WallpaperDisplayMode value) => NotifyChanged();
    partial void OnLowResolutionFilterEnabledChanged(bool value) => NotifyChanged();
    partial void OnDesktopQuickActionsEnabledChanged(bool value) => NotifyChanged();
    partial void OnMinimumImageSideLengthChanged(int value)
    {
        if (value < 1)
            MinimumImageSideLength = 1;

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (!_isInitializing)
            _onChanged();
    }
}
