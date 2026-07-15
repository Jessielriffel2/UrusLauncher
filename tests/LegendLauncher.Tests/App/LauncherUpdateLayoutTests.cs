namespace LegendLauncher.Tests.App;

public sealed class LauncherUpdateLayoutTests
{
    [Fact]
    public void MainWindowPlacesUpdateStatusInTheLowerLeftProfilePanel()
    {
        string source = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "MainWindow.xaml"));

        Assert.Contains(
            "xmlns:updates=\"clr-namespace:LegendLauncher.App.Views.Updates\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains("<updates:UpdateStatusView Grid.Row=\"4\" />", source);
        Assert.Contains("<Border Grid.Row=\"6\" Background=\"#C9081626\"", source);
    }

    [Fact]
    public void UpdateCardRequiresExplicitActionAndShowsLocalizedNotes()
    {
        string source = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "Views",
            "Updates",
            "UpdateStatusView.xaml"));

        Assert.Contains("IsUpdateCardVisible", source);
        Assert.Contains("UpdateViewNotesText", source);
        Assert.Contains("ToggleUpdateNotesCommand", source);
        Assert.Contains("InstallUpdateCommand", source);
        Assert.Contains("UpdateNotesText", source);
        Assert.Contains(
            "Value=\"{Binding UpdateProgress, Mode=OneWay}\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains("Update_CardAutomation", source);
        Assert.Contains("Update_NotesAutomation", source);
        Assert.DoesNotContain("WebBrowser", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WebView", source, StringComparison.Ordinal);
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
