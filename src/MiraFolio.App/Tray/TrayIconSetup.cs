using System.Windows;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using MiraFolio.App.Localization;
using MiraFolio.App.Views;
using MiraFolio.Core.Services;

namespace MiraFolio.App.Tray;

public class TrayIconSetup : IDisposable
{
    private TaskbarIcon? _trayIcon;
    private readonly RotationScheduler _scheduler;
    private readonly IMonitorService _monitorService;
    private SettingsWindow? _settingsWindow;

    public TrayIconSetup(RotationScheduler scheduler, IMonitorService monitorService)
    {
        _scheduler = scheduler;
        _monitorService = monitorService;
    }

    public void Initialize()
    {
        _trayIcon = new TaskbarIcon
        {
            IconSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Resources/mirafolio-icon.ico")),
            ToolTipText = LocalizationService.Get("TrayTooltip"),
        };

        _trayIcon.ContextMenu = BuildContextMenu();
        _trayIcon.LeftClickCommand = new RelayCommand(OpenSettings);
        _trayIcon.NoLeftClickDelay = true;
        LocalizationService.LanguageChanged += OnLanguageChanged;

        // Required when TaskbarIcon is created in code rather than XAML —
        // ForceCreate registers the icon with Win32 Shell_NotifyIcon.
        _trayIcon.ForceCreate(enablesEfficiencyMode: false);
    }

    private System.Windows.Controls.ContextMenu BuildContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var openSettings = new System.Windows.Controls.MenuItem { Header = LocalizationService.Get("TrayOpenSettings") };
        openSettings.Click += (_, _) => OpenSettings();

        var rotateAll = new System.Windows.Controls.MenuItem { Header = LocalizationService.Get("TrayRotateAll") };
        rotateAll.Click += (_, _) =>
        {
            foreach (var monitor in _monitorService.GetMonitors())
                _scheduler.RotateNow(monitor.DevicePath);
        };

        var separator = new System.Windows.Controls.Separator();

        var exit = new System.Windows.Controls.MenuItem { Header = LocalizationService.Get("TrayExit") };
        exit.Click += (_, _) => Application.Current.Shutdown();

        menu.Items.Add(openSettings);
        menu.Items.Add(rotateAll);
        menu.Items.Add(separator);
        menu.Items.Add(exit);

        return menu;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_trayIcon == null)
                return;

            _trayIcon.ToolTipText = LocalizationService.Get("TrayTooltip");
            _trayIcon.ContextMenu = BuildContextMenu();
        });
    }

    public void OpenSettings()
    {
        _settingsWindow ??= new SettingsWindow();
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void Dispose()
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        _trayIcon?.Dispose();
    }
}

// Simple relay command for tray icon double-click
public class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
    public RelayCommand(Action execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}
