using System.Text.Json;

namespace LegendLauncher.Tests.App;

public sealed class GitHubReleaseContractTests
{
    [Fact]
    public void ReleaseWorkflowPublishesOnlyVersionedTagArtifacts()
    {
        string source = File.ReadAllText(FindRepositoryFile(
            ".github",
            "workflows",
            "release.yml"));

        Assert.Contains("tags:", source);
        Assert.Contains("\"v*.*.*\"", source);
        Assert.Contains("contents: write", source);
        Assert.Contains("contents: read", source);
        Assert.Contains("persist-credentials: false", source);
        Assert.Contains(
            "actions/checkout@df4cb1c069e1874edd31b4311f1884172cec0e10",
            source);
        Assert.Contains(
            "actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1",
            source);
        Assert.Contains(
            "actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a",
            source);
        Assert.Contains(
            "actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c",
            source);
        Assert.Contains("choco install innosetup --version=6.7.1", source);
        Assert.Contains("needs: build", source);
        Assert.Contains("dotnet test", source);
        Assert.Contains("build-urus-distribution.ps1", source);
        Assert.Contains("update-manifest.json", source);
        Assert.Contains("SHA256SUMS.txt", source);
        Assert.Contains("--notes-file", source);
        Assert.Contains("GH_TOKEN: ${{ github.token }}", source);
        Assert.DoesNotContain("ghp_", source, StringComparison.Ordinal);
        Assert.DoesNotContain("github_pat_", source, StringComparison.Ordinal);
    }

    [Fact]
    public void VersionOneOneZeroHasPatchNotesInEverySupportedLanguage()
    {
        string path = FindRepositoryFile("docs", "releases", "v1.1.0.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("1.1.0", root.GetProperty("version").GetString());
        foreach (string language in new[] { "pt-BR", "en-US", "es-ES" })
        {
            Assert.False(string.IsNullOrWhiteSpace(
                root.GetProperty("title").GetProperty(language).GetString()));
            Assert.NotEmpty(root.GetProperty("notes").GetProperty(language).EnumerateArray());
        }
    }

    private static string FindRepositoryFile(params string[] relativeSegments) =>
        Path.Combine([FindRepositoryRoot(), .. relativeSegments]);

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

        throw new DirectoryNotFoundException(
            "LegendLauncherNext repository root was not found.");
    }
}
