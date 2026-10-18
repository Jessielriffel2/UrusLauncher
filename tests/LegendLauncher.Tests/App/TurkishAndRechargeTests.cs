using System.Text.RegularExpressions;
using LegendLauncher.App.Localization;
using LegendLauncher.App.Updates;
using LegendLauncher.Providers.SwordOnline;

namespace LegendLauncher.Tests.App;

public sealed partial class TurkishAndRechargeTests
{
    [Theory]
    [InlineData("tr", "tr-TR")]
    [InlineData("tr-TR", "tr-TR")]
    [InlineData("TR_tr", "tr-TR")]
    public void TurkishSystemLanguagesUseTheTurkishCatalog(string system, string expected)
    {
        Assert.Equal(expected, LocalizationService.NormalizeLanguageCode(system));
    }

    [Fact]
    public void TurkishCatalogHasEveryKeyAndTheSamePlaceholders()
    {
        IReadOnlyDictionary<string, string> english = LocalizationService.GetCatalog("en-US");
        IReadOnlyDictionary<string, string> turkish = LocalizationService.GetCatalog("tr-TR");

        Assert.Equal(english.Keys.OrderBy(k => k, StringComparer.Ordinal), turkish.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach ((string key, string text) in english)
        {
            Assert.Equal(Placeholders(text), Placeholders(turkish[key]));
        }

        Assert.Equal("Sürüm {0}", turkish["App_VersionLabel"]);
    }

    [Fact]
    public void ReleaseNotesWithoutTurkishFallBackToEnglish()
    {
        var entry = new ReleaseCatalogEntry(
            new Version(1, 1, 22),
            new Dictionary<string, string> { ["pt-BR"] = "Cores", ["en-US"] = "Colors" },
            new Dictionary<string, IReadOnlyList<string>> { ["pt-BR"] = ["nota"], ["en-US"] = ["note"] });

        Assert.Equal("Colors", entry.GetTitle("tr-TR"));
        Assert.Equal(["note"], entry.GetNotes("tr-TR"));
        Assert.Equal("Cores", entry.GetTitle("es-ES"));
    }

    [Fact]
    public void RechargeButtonOpensTheWebsiteTopUpPage()
    {
        Assert.Equal("https://swordonline.tech/recarga", SwordOnlinePlatformCatalog.RechargeUri.AbsoluteUri);

        string main = File.ReadAllText(FindRepositoryFile("src", "LegendLauncher.App", "MainWindow.xaml"));
        string workspace = File.ReadAllText(FindRepositoryFile("src", "LegendLauncher.App", "Views", "Game", "GameWorkspaceView.xaml"));
        Assert.Contains("Command=\"{Binding OpenRechargeCommand}\"", main, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenRechargeCommand}\"", workspace, StringComparison.Ordinal);
    }

    private static string[] Placeholders(string text) =>
        [.. PlaceholderPattern().Matches(text).Select(m => m.Value).Order(StringComparer.Ordinal)];

    [GeneratedRegex(@"\{\d+(?::[^}]*)?\}")]
    private static partial Regex PlaceholderPattern();

    private static string FindRepositoryFile(params string[] relativeSegments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LegendLauncherNext.slnx")))
            {
                return Path.Combine([directory.FullName, .. relativeSegments]);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("LegendLauncherNext repository root was not found.");
    }
}
