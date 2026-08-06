using System.Globalization;
using MiraFolio.Core.Utilities;
using Xunit;

namespace MiraFolio.Tests;

public sealed class LanguageResolverTests
{
    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("de-DE", "de")]
    [InlineData("fr-CA", "fr")]
    [InlineData("es-MX", "es")]
    [InlineData("ja-JP", "ja")]
    [InlineData("ko-KR", "ko")]
    [InlineData("ru-RU", "ru")]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh-SG", "zh-CN")]
    [InlineData("zh-Hant", "zh-TW")]
    [InlineData("zh-HK", "zh-TW")]
    [InlineData("pt-BR", "en")]
    public void Resolve_AutomaticSelection_MatchesSupportedSystemLanguage(
        string cultureName,
        string expected)
    {
        var result = LanguageResolver.Resolve(null, CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("DE-de", "de")]
    [InlineData("zh-tw", "zh-TW")]
    [InlineData("unsupported", "en")]
    public void Resolve_ConfiguredSelection_NormalizesOrFallsBackToEnglish(
        string configured,
        string expected)
    {
        var result = LanguageResolver.Resolve(configured, CultureInfo.GetCultureInfo("ja-JP"));

        Assert.Equal(expected, result);
    }
}
