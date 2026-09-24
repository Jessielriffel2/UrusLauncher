using System.Xml.Linq;
using LegendLauncher.App.Updates;

namespace LegendLauncher.Tests.App;

public sealed class FeatureCatalogTests
{
    [Fact]
    public void ReleaseCatalog_LoadsHistoryNewestFirst()
    {
        IReadOnlyList<ReleaseCatalogEntry> entries = ReleaseCatalog.Load();

        Assert.NotEmpty(entries);
        Assert.Equal(new Version(1, 1, 11), entries[0].Version);
        Assert.Contains(entries, entry => entry.Version == new Version(1, 1, 10));
        Assert.All(entries, entry => Assert.False(string.IsNullOrWhiteSpace(entry.GetTitle("pt-BR"))));
    }

    [Fact]
    public void ReleaseCatalog_FormatsLocalizedHistoryWithCurrentMacroRelease()
    {
        string history = ReleaseCatalog.FormatHistory(
            ReleaseCatalog.Load(),
            "pt-BR");

        Assert.Contains("v1.1.11", history, StringComparison.Ordinal);
        Assert.Contains("v1.1.10", history, StringComparison.Ordinal);
        Assert.Contains("Macro Assistant", history, StringComparison.Ordinal);
        Assert.Contains("Parar macro", history, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainWindow_ProvidesManualClosableFeatureCatalog()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument document = XDocument.Load(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "MainWindow.xaml"));
        XElement button = document
            .Descendants(presentation + "Button")
            .Single(element => element.Attribute(xaml + "Name")?.Value == "FeatureCatalogButton");
        XElement popup = document
            .Descendants(presentation + "Popup")
            .Single(element => element.Attribute(xaml + "Name")?.Value == "FeatureCatalogPopup");

        Assert.Equal("{Binding OpenFeatureCatalogCommand}", button.Attribute("Command")?.Value);
        Assert.Equal("{Binding IsFeatureCatalogOpen, Mode=TwoWay}", popup.Attribute("IsOpen")?.Value);
        Assert.Contains("IsFeatureCatalogUnread", button.ToString(), StringComparison.Ordinal);
        Assert.Contains("FeatureCatalogPulse", button.ToString(), StringComparison.Ordinal);
        Assert.Contains("FeatureCatalogUnreadBadgeText", document.ToString(), StringComparison.Ordinal);
        Assert.Contains(popup.Descendants(presentation + "KeyBinding"), element =>
            element.Attribute("Key")?.Value == "Escape" &&
            element.Attribute("Command")?.Value == "{Binding CloseFeatureCatalogCommand}");
    }

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
