using System.Xml.Linq;

namespace LegendLauncher.Tests.App;

public sealed class GameWorkspaceXamlTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void CompactToolbar_UsesOneRowAndReservesMainWindowCaptionSpace()
    {
        XDocument document = XDocument.Load(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "Views",
            "Game",
            "GameWorkspaceView.xaml"));
        XElement toolbar = FindNamedElement(document, "Grid", "CompactGameToolbar");

        Assert.Null(toolbar.Element(Presentation + "Grid.RowDefinitions"));

        XElement[] columns = toolbar
            .Element(Presentation + "Grid.ColumnDefinitions")!
            .Elements(Presentation + "ColumnDefinition")
            .ToArray();
        Assert.Equal(8, columns.Length);
        Assert.Equal("150", columns[^1].Attribute("Width")?.Value);

        XElement reservedSpace = FindNamedElement(
            document,
            "Border",
            "MainWindowCaptionButtonReservedSpace");
        Assert.Same(toolbar, reservedSpace.Parent);
        Assert.Equal("7", reservedSpace.Attribute("Grid.Column")?.Value);
        Assert.Equal("150", reservedSpace.Attribute("Width")?.Value);

        XElement rootGrid = Assert.IsType<XElement>(toolbar.Parent);
        string? toolbarRowHeight = rootGrid
            .Element(Presentation + "Grid.RowDefinitions")!
            .Elements(Presentation + "RowDefinition")
            .First()
            .Attribute("Height")?
            .Value;
        Assert.Equal("44", toolbarRowHeight);
    }

    [Fact]
    public void CompactToolbar_KeepsEveryGameCommandInTheSameToolbar()
    {
        XDocument document = XDocument.Load(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "Views",
            "Game",
            "GameWorkspaceView.xaml"));
        XElement toolbar = FindNamedElement(document, "Grid", "CompactGameToolbar");
        string[] commands = toolbar
            .Descendants(Presentation + "Button")
            .Select(button => button.Attribute("Command")?.Value)
            .Where(static command => command is not null)
            .Cast<string>()
            .ToArray();

        Assert.Contains("{Binding ShowLauncherCommand}", commands);
        Assert.Contains("{Binding AddAccountCommand}", commands);
        Assert.Contains("{Binding Workspace.ToggleMuteCommand}", commands);
        Assert.Contains("{Binding Workspace.SingleLayoutCommand}", commands);
        Assert.Contains("{Binding Workspace.SplitTwoLayoutCommand}", commands);
        Assert.Contains("{Binding Workspace.GridFourLayoutCommand}", commands);
        Assert.Contains("{Binding Workspace.DetachSessionCommand}", commands);

        XElement addAccountButton = toolbar
            .Elements(Presentation + "Button")
            .Single(button => button.Attribute("Command")?.Value == "{Binding AddAccountCommand}");
        Assert.Equal("4", addAccountButton.Attribute("Grid.Column")?.Value);

        XElement tabs = FindNamedElement(document, "ScrollViewer", "SessionTabsScrollViewer");
        Assert.Same(toolbar, tabs.Parent);
        Assert.Equal("2", tabs.Attribute("Grid.Column")?.Value);

        XElement actionGroup = FindNamedElement(document, "StackPanel", "WorkspaceActionGroup");
        Assert.Same(toolbar, actionGroup.Parent);
        Assert.Equal("6", actionGroup.Attribute("Grid.Column")?.Value);
    }

    [Fact]
    public void WorkspaceLanguageSelector_UsesExplicitMouseAndKeyboardOpening()
    {
        XDocument document = XDocument.Load(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "Views",
            "Game",
            "GameWorkspaceView.xaml"));
        XElement selector = FindNamedElement(
            document,
            "ComboBox",
            "WorkspaceLanguageSelector");

        Assert.Equal(
            "ComboBoxSelector_OnPreviewMouseLeftButtonDown",
            selector.Attribute("PreviewMouseLeftButtonDown")?.Value);
        Assert.Equal(
            "ComboBoxSelector_OnPreviewKeyDown",
            selector.Attribute("PreviewKeyDown")?.Value);
        Assert.Equal("DisplayName", selector.Attribute("DisplayMemberPath")?.Value);
    }

    private static XElement FindNamedElement(
        XDocument document,
        string localName,
        string name) =>
        document
            .Descendants(Presentation + localName)
            .Single(element => element.Attribute(Xaml + "Name")?.Value == name);

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
