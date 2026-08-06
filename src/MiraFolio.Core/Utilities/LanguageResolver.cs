using System.Globalization;

namespace MiraFolio.Core.Utilities;

public static class LanguageResolver
{
    public const string FallbackLanguageCode = "en";

    public static IReadOnlySet<string> SupportedLanguageCodes { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "en",
            "zh-CN",
            "zh-TW",
            "de",
            "fr",
            "es",
            "ja",
            "ko",
            "ru"
        };

    public static string Resolve(string? configuredLanguageCode, CultureInfo? systemCulture = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredLanguageCode))
            return NormalizeSupportedCode(configuredLanguageCode) ?? FallbackLanguageCode;

        systemCulture ??= CultureInfo.CurrentUICulture;
        return MatchSystemCulture(systemCulture);
    }

    private static string MatchSystemCulture(CultureInfo culture)
    {
        var name = culture.Name;
        if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Hant", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("-TW", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("-HK", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("-MO", StringComparison.OrdinalIgnoreCase))
                return "zh-TW";

            return "zh-CN";
        }

        return NormalizeSupportedCode(culture.TwoLetterISOLanguageName) ?? FallbackLanguageCode;
    }

    private static string? NormalizeSupportedCode(string languageCode)
    {
        if (languageCode.Equals("zh-CN", StringComparison.OrdinalIgnoreCase))
            return "zh-CN";
        if (languageCode.Equals("zh-TW", StringComparison.OrdinalIgnoreCase))
            return "zh-TW";

        var neutralCode = languageCode.Split('-', '_')[0].ToLowerInvariant();
        return SupportedLanguageCodes.Contains(neutralCode) ? neutralCode : null;
    }
}
