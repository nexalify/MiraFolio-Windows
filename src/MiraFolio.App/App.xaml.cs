using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiraFolio.App.DesktopOverlay;
using MiraFolio.App.Logging;
using MiraFolio.App.Localization;
using MiraFolio.App.Tray;
using MiraFolio.App.ViewModels;
using MiraFolio.Core.Services;

namespace MiraFolio.App;

public partial class App : Application
{
    private static Mutex? _mutex;
    private IHost? _host;
    public IServiceProvider? Services => _host?.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LocalizationService.ApplyLanguage(null);

        // Single-instance guard
        _mutex = new Mutex(true, "MiraFolio-Windows-SingleInstance", out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show(LocalizationService.Get("AppAlreadyRunning"), "MiraFolio");
            Shutdown();
            return;
        }

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.AddProvider(new FileLoggerProvider());
            })
            .Build();

        await _host.StartAsync();

        // Sync startup registry with settings
        var settingsService = _host.Services.GetRequiredService<ISettingsService>();
        var appSettings = settingsService.Load();
        LocalizationService.ApplyLanguage(appSettings.Global.LanguageCode);
        StartupManager.SetStartWithWindows(appSettings.Global.StartWithWindows);
        settingsService.SettingsChanged += (_, s) =>
        {
            StartupManager.SetStartWithWindows(s.Global.StartWithWindows);
            LocalizationService.ApplyLanguage(s.Global.LanguageCode);
        };

        // Start rotation scheduler
        var scheduler = _host.Services.GetRequiredService<RotationScheduler>();
        scheduler.Start();

        // Setup system tray
        var tray = _host.Services.GetRequiredService<TrayIconSetup>();
        tray.Initialize();

        var desktopOverlay = _host.Services.GetRequiredService<DesktopOverlayManager>();
        desktopOverlay.Start();

        // On first run (no monitor assignments configured), open settings so user can get started
        if (appSettings.MonitorAssignments.Count == 0)
            tray.OpenSettings();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<DesktopWallpaperHost>();
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IWallpaperService, WallpaperService>();
        services.AddSingleton<IImageSelector, ImageSelector>();
        services.AddSingleton<RotationScheduler>();
        services.AddSingleton<IWallpaperQuickActionService, WallpaperQuickActionService>();

        // App services
        services.AddSingleton<TrayIconSetup>();
        services.AddSingleton<DesktopOverlayManager>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<RecycleBinViewModel>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
