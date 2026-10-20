using LegendLauncher.App.Updates;

namespace LegendLauncher.Tests.App;

public sealed class UpdateManifestLanguagesTests
{
    private static UpdateManifestDocument Manifest(Dictionary<string, string?> notes) => new(
        1,
        LauncherUpdateValidation.Repository,
        "1.1.23",
        new UpdateManifestInstallerDocument(
            LauncherUpdateValidation.ExpectedInstallerName(new Version(1, 1, 23)),
            1024,
            new string('a', 64)),
        notes);

    [Fact]
    public void AnExtraTurkishNoteIsAcceptedAndKept()
    {
        ValidatedUpdateManifest validated = UpdateManifestValidator.Validate(
            Manifest(new() { ["pt-BR"] = "a", ["en-US"] = "b", ["es-ES"] = "c", ["tr-TR"] = "d" }),
            new Version(1, 1, 23));

        Assert.Equal("d", validated.LocalizedNotes["tr-TR"]);
        Assert.Equal("b", validated.LocalizedNotes["en-US"]);
    }

    [Fact]
    public void TheThreeOriginalLanguagesAreStillRequired()
    {
        Assert.Throws<InvalidDataException>(() => UpdateManifestValidator.Validate(
            Manifest(new() { ["pt-BR"] = "a", ["en-US"] = "b", ["tr-TR"] = "d" }),
            new Version(1, 1, 23)));
    }

    [Fact]
    public void ReleaseScriptStillWritesOnlyTheThreeLanguagesOldLaunchersAccept()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LegendLauncherNext.slnx")))
        {
            directory = directory.Parent;
        }

        string script = File.ReadAllText(Path.Combine(directory!.FullName, "scripts", "build-urus-distribution.ps1"));
        Assert.Contains("$releaseLanguages = @('pt-BR', 'en-US', 'es-ES')", script, StringComparison.Ordinal);
    }
}
