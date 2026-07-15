using LegendLauncher.App.Localization;

namespace LegendLauncher.Tests.App;

public sealed class LocalizationServiceTests
{
    [Theory]
    [InlineData(null, "pt-BR")]
    [InlineData("", "pt-BR")]
    [InlineData("pt", "pt-BR")]
    [InlineData("PT-pt", "pt-BR")]
    [InlineData("en", "en-US")]
    [InlineData("en-GB", "en-US")]
    [InlineData("ES_mx", "es-ES")]
    [InlineData("de-DE", "pt-BR")]
    public void NormalizeLanguageCode_MapsSupportedFamiliesAndFallsBack(
        string? requested,
        string expected)
    {
        Assert.Equal(expected, LocalizationService.NormalizeLanguageCode(requested));
    }

    [Fact]
    public void SupportedLanguages_ExposeStableCodesAndNativeNames()
    {
        Assert.Equal(
            ["pt-BR", "en-US", "es-ES"],
            LocalizationService.SupportedLanguages.Select(static language => language.Code));
        Assert.Equal(
            ["Português (Brasil)", "English", "Español"],
            LocalizationService.SupportedLanguages.Select(static language => language.DisplayName));
        Assert.Equal(
            ["Português (Brasil)", "English", "Español"],
            LocalizationService.SupportedLanguages.Select(static language => language.ToString()));
    }

    [Fact]
    public void SetLanguage_NotifiesBindingsOnceAndIgnoresEquivalentSelection()
    {
        var localization = new LocalizationService("pt-BR");
        var changedProperties = new List<string?>();
        int languageChangedCount = 0;
        localization.PropertyChanged += (_, eventArgs) =>
            changedProperties.Add(eventArgs.PropertyName);
        localization.LanguageChanged += (_, _) => languageChangedCount++;

        Assert.True(localization.SetLanguage("en-GB"));
        Assert.Equal("en-US", localization.LanguageCode);
        Assert.Equal("en-US", localization.Culture.Name);
        Assert.Equal(
            [nameof(LocalizationService.LanguageCode), nameof(LocalizationService.Culture), "Item[]"],
            changedProperties);
        Assert.Equal(1, languageChangedCount);

        changedProperties.Clear();
        Assert.False(localization.SetLanguage("EN-us"));
        Assert.Empty(changedProperties);
        Assert.Equal(1, languageChangedCount);
    }

    [Fact]
    public void GetAndFormat_UseSelectedCatalogAndCulture()
    {
        var localization = new LocalizationService("en-US");

        Assert.Equal("Ready to play", localization.Get("Session_ReadyTitle"));
        Assert.Equal("1,234 servers", localization.Format("Servers_CountPlural", 1234));

        localization.SetLanguage("es");
        Assert.Equal("Listo para jugar", localization.Get("Session_ReadyTitle"));
        Assert.Equal("1.234 servidores", localization.Format("Servers_CountPlural", 1234));
        Assert.Equal("[Missing_Test_Key]", localization.Get("Missing_Test_Key"));
    }

    [Fact]
    public void LocalizedMessage_ReResolvesAfterRuntimeLanguageChange()
    {
        var localization = new LocalizationService("pt-BR");
        LocalizedMessage message = LocalizedMessage.Create(
            "Catalog_LoadingServers",
            "Legend Online");

        Assert.Equal(
            "Buscando servidores de Legend Online...",
            message.Resolve(localization));

        localization.SetLanguage("en-US");

        Assert.Equal(
            "Loading servers for Legend Online...",
            message.Resolve(localization));
    }
}
