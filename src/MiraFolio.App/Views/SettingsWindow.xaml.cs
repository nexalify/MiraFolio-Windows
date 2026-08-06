using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using MiraFolio.App.ViewModels;
using MiraFolio.Core.Models;
using MiraFolio.Core.Services;

namespace MiraFolio.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel? _viewModel;
    private readonly ISettingsService? _settingsService;
    private readonly DispatcherTimer _windowSizeSaveTimer;
    private bool _restoringWindowSize;

    public SettingsWindow()
    {
        InitializeComponent();
        _settingsService = ((App)Application.Current).Services?.GetRequiredService<ISettingsService>();
        _viewModel = ((App)Application.Current).Services?.GetRequiredService<SettingsViewModel>();
        DataContext = _viewModel;

        _windowSizeSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _windowSizeSaveTimer.Tick += (_, _) => SaveWindowSize();

        RestoreWindowSize();
        SizeChanged += OnSizeChanged;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => SystemCommands.MinimizeWindow(this);

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(this);
        else
            SystemCommands.MaximizeWindow(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void RecycleBinButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new RecycleBinWindow { Owner = this };
        window.ShowDialog();
    }

    private void LanguageSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new LanguageSettingsWindow { Owner = this };
        window.ShowDialog();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_windowSizeSaveTimer.IsEnabled)
            SaveWindowSize();
        e.Cancel = true; // Don't destroy, just hide
        Hide();
    }

    private void RestoreWindowSize()
    {
        var settings = _settingsService?.Load();
        var global = settings?.Global;
        if (global == null)
            return;

        _restoringWindowSize = true;
        Width = Math.Max(MinWidth, global.SettingsWindowWidth);
        Height = Math.Max(MinHeight, global.SettingsWindowHeight);
        _restoringWindowSize = false;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_restoringWindowSize || WindowState != WindowState.Normal)
            return;

        _windowSizeSaveTimer.Stop();
        _windowSizeSaveTimer.Start();
    }

    private void SaveWindowSize()
    {
        _windowSizeSaveTimer.Stop();
        var settings = _settingsService?.Load();
        if (settings == null)
            return;

        settings.Global.SettingsWindowWidth = Width;
        settings.Global.SettingsWindowHeight = Height;
        _settingsService!.Save(settings, notifyChanged: false);
    }
}
