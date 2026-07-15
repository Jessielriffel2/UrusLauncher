using System.Text.RegularExpressions;
using LegendLauncher.App.Localization;

namespace LegendLauncher.Tests.App;

public sealed partial class LocalizationCatalogTests
{
    [Fact]
    public void Catalogs_HaveIdenticalNonEmptyKeysAndPlaceholderSignatures()
    {
        IReadOnlyDictionary<string, string> reference =
            LocalizationService.GetCatalog("pt-BR");
        string[] expectedKeys = reference.Keys.Order(StringComparer.Ordinal).ToArray();

        foreach (string languageCode in new[] { "en-US", "es-ES" })
        {
            IReadOnlyDictionary<string, string> catalog =
                LocalizationService.GetCatalog(languageCode);
            Assert.Equal(expectedKeys, catalog.Keys.Order(StringComparer.Ordinal));

            foreach (string key in expectedKeys)
            {
                Assert.False(string.IsNullOrWhiteSpace(catalog[key]));
                Assert.Equal(
                    GetPlaceholderSignature(reference[key]),
                    GetPlaceholderSignature(catalog[key]));
            }
        }
    }

    [Fact]
    public void AppSource_ReferencesOnlyExistingKeysAndLeavesNoCatalogKeyOrphaned()
    {
        string root = FindRepositoryRoot();
        string appRoot = Path.Combine(root, "src", "LegendLauncher.App");
        string[] files = Directory
            .EnumerateFiles(appRoot, "*.*", SearchOption.AllDirectories)
            .Where(static file =>
                (file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                 file.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) &&
                !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase) &&
                !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();
        string source = string.Join(
            Environment.NewLine,
            files.Select(File.ReadAllText));
        IReadOnlyDictionary<string, string> catalog =
            LocalizationService.GetCatalog("pt-BR");

        var knownPrefixes = catalog.Keys
            .Select(static key => key[..key.IndexOf('_')])
            .ToHashSet(StringComparer.Ordinal);
        string[] referencedKeys = XamlLocalizationRegex()
            .Matches(source)
            .Concat(LocalizationApiRegex().Matches(source))
            .Concat(LocalizationKeyLiteralRegex()
                .Matches(source)
                .Where(match => knownPrefixes.Contains(
                    match.Groups["key"].Value.Split('_', 2)[0])))
            .Select(static match => match.Groups["key"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] missingKeys = referencedKeys
            .Where(key => !catalog.ContainsKey(key))
            .ToArray();
        string[] orphanedKeys = catalog.Keys
            .Where(key => !source.Contains(key, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missingKeys);
        Assert.Empty(orphanedKeys);
    }

    [Theory]
    [InlineData("pt-BR", "J O G U E  D O  S E U  J E I T O")]
    [InlineData("en-US", "P L A Y  Y O U R  W A Y")]
    [InlineData("es-ES", "J U E G A  A  T U  M A N E R A")]
    public void PublicCatalogBrandIsUrusAndContainsNoLegacyPreviewMarkers(
        string languageCode,
        string expectedSubtitle)
    {
        IReadOnlyDictionary<string, string> catalog =
            LocalizationService.GetCatalog(languageCode);

        Assert.Equal("Urus Launcher", catalog["App_WindowTitle"]);
        Assert.Equal(expectedSubtitle, catalog["Brand_Subtitle"]);
        Assert.DoesNotContain("App_WindowTitlePreview", catalog.Keys);
        Assert.DoesNotContain("Brand_TechnicalPreview", catalog.Keys);
        Assert.All(catalog.Values, value =>
            Assert.DoesNotMatch(LegacyVisibleBrandRegex(), value));
    }

    [Theory]
    [InlineData("pt-BR", "INSTALAR", "VERIFICAR NOVAMENTE")]
    [InlineData("en-US", "INSTALL", "CHECK AGAIN")]
    [InlineData("es-ES", "INSTALAR", "VERIFICAR DE NUEVO")]
    public void UpdateCatalogSeparatesPreparedDownloadFromUserInstallation(
        string languageCode,
        string expectedInstallAction,
        string expectedCheckAgainAction)
    {
        var localization = new LocalizationService(languageCode);

        Assert.Equal(expectedInstallAction, localization.Get("Update_Action"));
        Assert.Equal(expectedCheckAgainAction, localization.Get("Update_CheckAgain"));
        Assert.Contains(
            "1.1.3",
            localization.Format("Update_CurrentDetail", "1.1.3"),
            StringComparison.Ordinal);
        Assert.Contains(
            "1.1.3",
            localization.Format("Update_ReadyTitle", "1.1.3"),
            StringComparison.Ordinal);
    }

    private static string[] GetPlaceholderSignature(string value) =>
        CompositePlaceholderRegex()
            .Matches(value)
            .Select(static match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

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

    [GeneratedRegex("\\\"(?<key>[A-Z][A-Za-z0-9]*_[A-Za-z0-9_]+)\\\"")]
    private static partial Regex LocalizationKeyLiteralRegex();

    [GeneratedRegex(@"\{loc:Localize\s+(?<key>[A-Za-z0-9_]+)\}")]
    private static partial Regex XamlLocalizationRegex();

    [GeneratedRegex(
        "(?:Get|Format|SetStatusMessage|SetCatalogStatus|Create)\\s*\\(\\s*\\\"(?<key>[A-Za-z0-9_]+)\\\"")]
    private static partial Regex LocalizationApiRegex();

    [GeneratedRegex(@"(?<!\{)\{\d+(?:,[^}:]+)?(?::[^}]+)?\}(?!\})")]
    private static partial Regex CompositePlaceholderRegex();

    [GeneratedRegex(
        @"Legend Launcher|Launcher Next|\bNext\b|\bPreview\b|\bPrévia\b|Vista previa|\bTechnical\b|\btesting\b|\btests\b|\btestes?\b|\bpruebas\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LegacyVisibleBrandRegex();
}
