using System.Globalization;
using System.Windows;
using MiraFolio.Core.Utilities;

namespace MiraFolio.App.Localization;

public static class LocalizationService
{
    private const string ResourcePrefix = "Resources/Localization/Strings.";
    private static bool _initialized;

    public static event EventHandler? LanguageChanged;

    public static string CurrentLanguageCode { get; private set; } =
        LanguageResolver.FallbackLanguageCode;

    public static IReadOnlyList<LanguageOption> GetLanguageOptions() =>
    [
        new(null, Get("LanguageSystemDefault")),
        new("en", "English"),
        new("zh-CN", "简体中文"),
        new("zh-TW", "繁體中文"),
        new("de", "Deutsch"),
        new("fr", "Français"),
        new("es", "Español"),
        new("ja", "日本語"),
        new("ko", "한국어"),
        new("ru", "Русский")
    ];

    public static void ApplyLanguage(string? configuredLanguageCode)
    {
        var languageCode = LanguageResolver.Resolve(configuredLanguageCode);
        if (_initialized && string.Equals(CurrentLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
            return;

        var application = Application.Current;
        if (application == null)
            return;

        var dictionaries = application.Resources.MergedDictionaries;
        foreach (var dictionary in dictionaries
            .Where(IsLanguageOverrideDictionary)
            .ToArray())
        {
            dictionaries.Remove(dictionary);
        }

        if (!string.Equals(languageCode, LanguageResolver.FallbackLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            dictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"{ResourcePrefix}{languageCode}.xaml", UriKind.Relative)
            });
        }

        var culture = CultureInfo.GetCultureInfo(languageCode);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        CurrentLanguageCode = languageCode;
        _initialized = true;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key)
    {
        var value = Application.Current?.TryFindResource(key) as string ?? key;
        return value.Replace("\\n", Environment.NewLine, StringComparison.Ordinal);
    }

    public static string Format(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);

    private static bool IsLanguageOverrideDictionary(ResourceDictionary dictionary)
    {
        var source = dictionary.Source?.OriginalString;
        return source != null &&
               source.Contains(ResourcePrefix, StringComparison.OrdinalIgnoreCase) &&
               !source.EndsWith("Strings.en.xaml", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record LanguageOption(string? Code, string NativeName);
