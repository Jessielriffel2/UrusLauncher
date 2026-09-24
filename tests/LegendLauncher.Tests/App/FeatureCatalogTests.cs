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
        Assert.Equal(new Version(1, 1, 13), entries[0].Version);
        Assert.Contains(entries, entry => entry.Version == new Version(1, 1, 12));
        Assert.All(entries, entry => Assert.False(string.IsNullOrWhiteSpace(entry.GetTitle("pt-BR"))));
    }

    [Fact]
    public void ReleaseCatalog_FormatsLocalizedHistoryWithCurrentMacroRelease()
    {
        string history = ReleaseCatalog.FormatHistory(
            ReleaseCatalog.Load(),
            "pt-BR");

        Assert.Contains("v1.1.13", history, StringComparison.Ordinal);
        Assert.Contains("v1.1.12", history, StringComparison.Ordinal);
        Assert.Contains("v1.1.11", history, StringComparison.Ordinal);
        Assert.Contains("Macro Assistant", history, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseCatalog_ExposesStructuredHistoryRowsForTheReleaseModal()
    {
        IReadOnlyList<ReleaseCatalogHistoryItem> items = ReleaseCatalog.FormatHistoryItems(
            ReleaseCatalog.Load(),
            "pt-BR");

        Assert.NotEmpty(items);
        Assert.Equal("v1.1.13", items[0].VersionText);
        Assert.Equal("v1.1.12", items[1].VersionText);
        Assert.All(items, item =>
        {
            Assert.StartsWith("v", item.VersionText, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(item.Title));
            Assert.NotEmpty(item.Notes);
        });
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

    [Fact]
    public void MainWindow_RendersReleaseHistoryWithBoldVersionsAndSeparators()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XDocument document = XDocument.Load(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "MainWindow.xaml"));

        XElement history = document
            .Descendants(presentation + "ItemsControl")
            .Single(element => element.Attribute("ItemsSource")?.Value == "{Binding FeatureCatalogReleases}");

        XElement version = history
            .Descendants(presentation + "TextBlock")
            .Single(element => element.Attribute("Text")?.Value == "{Binding VersionText}");
        Assert.Equal("Bold", version.Attribute("FontWeight")?.Value);

        XElement separator = history
            .Descendants(presentation + "Border")
            .Single(element => element.Attribute("BorderThickness")?.Value == "0,0,0,1");
        Assert.Equal("#31566B", separator.Attribute("BorderBrush")?.Value);
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
