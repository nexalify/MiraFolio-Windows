using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MiraFolio.App.Localization;
using MiraFolio.Core.Services;

namespace MiraFolio.App.Views;

public partial class LanguageSettingsWindow : Window
{
    private readonly ISettingsService _settingsService;

    public IReadOnlyList<LanguageOption> Languages { get; }
    public LanguageOption SelectedLanguage { get; set; }

    public LanguageSettingsWindow()
    {
        InitializeComponent();

        _settingsService = ((App)Application.Current).Services!
            .GetRequiredService<ISettingsService>();

        var configuredLanguage = _settingsService.Load().Global.LanguageCode;
        Languages = LocalizationService.GetLanguageOptions();
        SelectedLanguage = Languages.FirstOrDefault(option =>
            string.Equals(option.Code, configuredLanguage, StringComparison.OrdinalIgnoreCase)) ?? Languages[0];
        DataContext = this;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Load();
        settings.Global.LanguageCode = SelectedLanguage.Code;
        _settingsService.Save(settings);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;
}
