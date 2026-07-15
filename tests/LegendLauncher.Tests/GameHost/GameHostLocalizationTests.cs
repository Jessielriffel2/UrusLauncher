using System.Diagnostics;
using System.Globalization;
using LegendLauncher.GameHost.Legacy;

namespace LegendLauncher.Tests.GameHost;

public sealed class GameHostLocalizationTests
{
    [Theory]
    [InlineData(null, "pt-BR")]
    [InlineData("", "pt-BR")]
    [InlineData("pt", "pt-BR")]
    [InlineData("PT_br", "pt-BR")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("en", "en-US")]
    [InlineData("en-GB", "en-US")]
    [InlineData("ES_es", "es-ES")]
    [InlineData("es-MX", "es-ES")]
    [InlineData("de-DE", "pt-BR")]
    public void NormalizeCultureName_MapsSupportedLanguageFamilies(
        string? requested,
        string expected)
    {
        Assert.Equal(expected, GameHostLocalization.NormalizeCultureName(requested));
    }

    [Fact]
    public void Catalog_HasEveryTextInEverySupportedCulture()
    {
        foreach (string cultureName in GameHostLocalization.SupportedCultureNames)
        {
            foreach (GameHostText text in Enum.GetValues<GameHostText>())
            {
                Assert.False(string.IsNullOrWhiteSpace(
                    GameHostLocalization.Get(text, cultureName)));
            }
        }
    }

    [Fact]
    public void Catalog_ProvidesDistinctTranslationsForVisibleText()
    {
        string portuguese = GameHostLocalization.Get(GameHostText.InvalidOptions, "pt-BR");
        string english = GameHostLocalization.Get(GameHostText.InvalidOptions, "en-US");
        string spanish = GameHostLocalization.Get(GameHostText.InvalidOptions, "es-ES");

        Assert.NotEqual(portuguese, english);
        Assert.NotEqual(portuguese, spanish);
        Assert.NotEqual(english, spanish);
    }

    [Theory]
    [InlineData("pt-BR", "Verificação de compatibilidade")]
    [InlineData("en-US", "Compatibility check")]
    [InlineData("es-ES", "Comprobación de compatibilidad")]
    public void CompatibilityTitleAvoidsLegacyTestTerminology(
        string cultureName,
        string expected)
    {
        Assert.Equal(
            expected,
            GameHostLocalization.Get(GameHostText.CompatibilityProbe, cultureName));
    }

    [Fact]
    public void VisibleGameHostCaptionsUseUrusWhileLegendOnlineRemainsTheGameTitle()
    {
        string root = FindRepositoryRoot();
        string form = File.ReadAllText(Path.Combine(
            root,
            "src",
            "LegendLauncher.GameHost.Legacy",
            "LegacyGameHostForm.cs"));
        string program = File.ReadAllText(Path.Combine(
            root,
            "src",
            "LegendLauncher.GameHost.Legacy",
            "Program.cs"));

        Assert.DoesNotContain("Legend GameHost", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Legend GameHost", program, StringComparison.Ordinal);
        Assert.Contains("Urus GameHost", form, StringComparison.Ordinal);
        Assert.Contains("Urus GameHost", program, StringComparison.Ordinal);
        Assert.Contains("ConfigureWindow(\"Legend Online\")", form, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("en-CA", "en-US")]
    [InlineData("es-AR", "es-ES")]
    public void ConfigureLanguageEnvironment_PropagatesNormalizedCultureWithoutStartingProcess(
        string currentCulture,
        string expected)
    {
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
        };

        LegacyGameRuntime.ConfigureLanguageEnvironment(
            startInfo,
            new CultureInfo(currentCulture));

        Assert.Equal(
            expected,
            startInfo.Environment[GameHostLocalization.EnvironmentVariableName]);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LegendLauncherNext.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("LegendLauncherNext repository root was not found.");
    }
}
