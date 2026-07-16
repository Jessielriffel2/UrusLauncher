using System.Xml.Linq;

namespace LegendLauncher.Tests.App;

public sealed class MainWindowLayoutXamlTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Shell =
        "clr-namespace:System.Windows.Shell;assembly=PresentationFramework";

    [Fact]
    public void WorkspaceUsesCompactCaptionRowWithAllWindowControls()
    {
        XDocument document = LoadMainWindow();
        XElement window = Assert.IsType<XElement>(document.Root);
        Assert.Equal("820", window.Attribute("Height")?.Value);
        Assert.Equal("700", window.Attribute("MinHeight")?.Value);

        XElement chrome = window
            .Element(Shell + "WindowChrome.WindowChrome")!
            .Element(Shell + "WindowChrome")!;
        Assert.Equal("44", chrome.Attribute("CaptionHeight")?.Value);

        XElement launcherHeader = FindNamedElement(document, "Grid", "LauncherHeader");
        Assert.Equal("96", launcherHeader.Attribute("Height")?.Value);
        Assert.Contains("IsLauncherVisible", launcherHeader.Attribute("Visibility")?.Value);

        XElement captionButtons = FindNamedElement(
            document,
            "StackPanel",
            "MainWindowCaptionButtons");
        Assert.Equal("2", captionButtons.Attribute("Grid.RowSpan")?.Value);
        XElement[] buttons = captionButtons.Elements(Presentation + "Button").ToArray();
        Assert.Equal(3, buttons.Length);
        Assert.All(buttons, button => Assert.Equal("44", button.Attribute("Height")?.Value));
        Assert.Equal(
            new[] { "MinimizeButton_OnClick", "MaximizeButton_OnClick", "CloseButton_OnClick" },
            buttons
                .Select(button => button.Attribute("Click")?.Value ?? string.Empty)
                .ToArray());
    }

    [Fact]
    public void SessionSetupScrollsWhileRuntimeAndPrimaryActionStayPinned()
    {
        XDocument document = LoadMainWindow();
        XElement scrollViewer = FindNamedElement(
            document,
            "ScrollViewer",
            "SessionSetupScrollViewer");
        Assert.Equal("Auto", scrollViewer.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Equal("0", scrollViewer.Attribute("MinHeight")?.Value);

        XElement outerGrid = Assert.IsType<XElement>(scrollViewer.Parent);
        Assert.Equal("Grid", outerGrid.Name.LocalName);
        Assert.Equal(
            new[] { "*", "16", "72", "16", "54", "10", "Auto" },
            outerGrid
                .Element(Presentation + "Grid.RowDefinitions")!
                .Elements(Presentation + "RowDefinition")
                .Select(row => row.Attribute("Height")?.Value ?? string.Empty)
                .ToArray());

        AssertPinnedOutsideScroll(document, scrollViewer, "PinnedRuntimeStatus", "2");
        AssertPinnedOutsideScroll(document, scrollViewer, "PinnedPrimaryAction", "4");
        AssertPinnedOutsideScroll(document, scrollViewer, "PinnedSessionCaption", "6");
    }

    [Fact]
    public void RuntimeStatusAndDisabledPrimaryActionReflectActualReadiness()
    {
        XDocument document = LoadMainWindow();
        XElement runtimeStatus = FindNamedElement(document, "Border", "PinnedRuntimeStatus");
        XElement statusIndicator = runtimeStatus
            .Descendants(Presentation + "Border")
            .Single(element => element.Attribute("Width")?.Value == "17");
        XElement primaryAction = FindNamedElement(document, "Button", "PinnedPrimaryAction");
        string controls = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "Themes",
            "Controls.xaml"));

        Assert.Equal("{Binding RuntimeStatusBrush}", statusIndicator.Attribute("Background")?.Value);
        Assert.Equal("{Binding CanStartGame}", primaryAction.Attribute("IsEnabled")?.Value);
        Assert.Contains("<Trigger Property=\"IsEnabled\" Value=\"False\">", controls);
        Assert.Contains("Property=\"Background\" Value=\"#152434\"", controls);
        Assert.Contains("Property=\"Opacity\" Value=\"0.72\"", controls);
    }

    [Fact]
    public void LanguageSelector_UsesTheProvenExplicitDropDownInputHandlers()
    {
        XDocument document = LoadMainWindow();
        XElement selector = FindNamedElement(document, "ComboBox", "LanguageSelector");

        Assert.Equal(
            "ComboBoxSelector_OnPreviewMouseLeftButtonDown",
            selector.Attribute("PreviewMouseLeftButtonDown")?.Value);
        Assert.Equal(
            "ComboBoxSelector_OnPreviewKeyDown",
            selector.Attribute("PreviewKeyDown")?.Value);
        Assert.Equal("DisplayName", selector.Attribute("DisplayMemberPath")?.Value);
    }

    [Fact]
    public void ServerListUsesRoleBadgesAndASeparatedRemainingSection()
    {
        XDocument mainWindow = LoadMainWindow();
        XElement serverList = mainWindow
            .Descendants(Presentation + "ListBox")
            .Single(element =>
                element.Attribute("ItemsSource")?.Value == "{Binding VisibleServers}");
        string windowStyles = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "Themes",
            "WindowStyles.xaml"));

        Assert.Equal(
            "{StaticResource ServerRowTemplate}",
            serverList.Attribute("ItemTemplate")?.Value);
        Assert.Null(serverList.Element(Presentation + "ListBox.ItemTemplate"));
        Assert.Contains("x:Key=\"ServerRowTemplate\"", windowStyles, StringComparison.Ordinal);
        Assert.Contains("ShowRecommendedBadge", windowStyles, StringComparison.Ordinal);
        Assert.Contains("ShowLatestBadge", windowStyles, StringComparison.Ordinal);
        Assert.Contains("ShowSectionDivider", windowStyles, StringComparison.Ordinal);
        Assert.Contains("SectionLabelText", windowStyles, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicBrandAndMainArtifactUseUrusWithoutChangingCodeNamespace()
    {
        XDocument mainWindow = LoadMainWindow();
        XElement window = Assert.IsType<XElement>(mainWindow.Root);
        XElement[] headerTexts = mainWindow
            .Descendants(Presentation + "TextBlock")
            .Where(element => element.Ancestors().Any(ancestor =>
                ancestor.Attribute(Xaml + "Name")?.Value == "LauncherHeader"))
            .ToArray();

        Assert.Equal("{loc:Localize App_WindowTitle}", window.Attribute("Title")?.Value);
        Assert.Contains(headerTexts, element =>
            element.Attribute("Text")?.Value == "U R U S  L A U N C H E R");
        Assert.Contains(headerTexts, element =>
            element.Attribute("Text")?.Value == "{loc:Localize Brand_Subtitle}");

        XDocument project = XDocument.Load(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "LegendLauncher.App.csproj"));
        Assert.Equal(
            "UrusLauncher.App",
            project.Descendants("AssemblyName").Single().Value);
        Assert.Equal(
            "LegendLauncher.App",
            project.Descendants("RootNamespace").Single().Value);
        Assert.Equal(
            "Urus Launcher",
            project.Descendants("AssemblyTitle").Single().Value);
        Assert.Equal("Urus Launcher", project.Descendants("Product").Single().Value);
        Assert.Equal("Urus Launcher", project.Descendants("Company").Single().Value);
        Assert.Equal("Urus Launcher", project.Descendants("Description").Single().Value);

        XDocument manifest = XDocument.Load(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "app.manifest"));
        XNamespace assembly = "urn:schemas-microsoft-com:asm.v1";
        Assert.Equal(
            "UrusLauncher.App",
            manifest.Descendants(assembly + "assemblyIdentity").Single().Attribute("name")?.Value);
    }

    [Fact]
    public void PackUrisFollowTheRenamedUrusAssembly()
    {
        string root = FindRepositoryFile("src", "LegendLauncher.App");
        string[] xamlFiles = Directory
            .EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        string source = string.Join(Environment.NewLine, xamlFiles.Select(File.ReadAllText));

        Assert.DoesNotContain("/LegendLauncher.App;component/", source, StringComparison.Ordinal);
        Assert.Contains("/UrusLauncher.App;component/Assets/paypal-donation-qr.jpeg", source,
            StringComparison.Ordinal);
        Assert.Contains("/UrusLauncher.App;component/Assets/castle-background.png", source,
            StringComparison.Ordinal);
        Assert.Contains("/UrusLauncher.App;component/Assets/Branding/urus-logo.png", source,
            StringComparison.Ordinal);
    }

    private static void AssertPinnedOutsideScroll(
        XDocument document,
        XElement scrollViewer,
        string name,
        string expectedRow)
    {
        XElement element = document
            .Descendants()
            .Single(candidate => candidate.Attribute(Xaml + "Name")?.Value == name);
        Assert.Equal(expectedRow, element.Attribute("Grid.Row")?.Value);
        Assert.DoesNotContain(element, scrollViewer.Descendants());
        Assert.Same(scrollViewer.Parent, element.Parent);
    }

    private static XDocument LoadMainWindow() =>
        XDocument.Load(FindRepositoryFile("src", "LegendLauncher.App", "MainWindow.xaml"));

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
