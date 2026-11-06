using System.IO.MemoryMappedFiles;
using System.Xml.Linq;
using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.GameDisplay;
using LegendLauncher.GameHost.Legacy;

namespace LegendLauncher.Tests.App;

public sealed class GameZoomTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void AutomaticOnAHundredPercentMonitorChangesNothing()
    {
        Assert.Equal(1, GameZoom.ComputeScale(GameZoom.Automatic, 3440, 1300, monitorScale: 1.0));
        Assert.Equal(1, GameZoom.ComputeScale(GameZoom.Automatic, 1920, 950, monitorScale: 1.0));
    }

    [Fact]
    public void AutomaticOnATvFollowsWindowsButNeverMakesTheGameSmallerThanItsDesignSize()
    {
        // 4K TV at 300%: the game area keeps 600 game pixels of height (the battle fills the screen).
        double tv = GameZoom.ComputeScale(GameZoom.Automatic, 3760, 1550, monitorScale: 3.0);
        Assert.Equal(1550 / 600d, tv, precision: 6);
        Assert.True(3760 / tv >= GameZoom.MinimumLogicalWidth);

        // 1080p TV at 200%: same rule.
        Assert.Equal(777 / 600d, GameZoom.ComputeScale(GameZoom.Automatic, 1880, 777, monitorScale: 2.0), precision: 6);

        // Laptop at 125% with room to spare: exactly the Windows scale, like a browser.
        Assert.Equal(1.25, GameZoom.ComputeScale(GameZoom.Automatic, 1920, 960, monitorScale: 1.25), precision: 6);
    }

    [Theory]
    [InlineData(150, 3000, 1500, 1.5)]
    [InlineData(300, 1600, 900, 1.5)]
    [InlineData(200, 1000, 600, 1.0)]
    [InlineData(100, 3840, 2000, 1.0)]
    [InlineData(103, 3840, 2000, 1.0)]
    public void ManualChoicesAreLimitedByTheGameArea(int choice, int width, int height, double expected)
    {
        Assert.Equal(expected, GameZoom.ComputeScale(choice, width, height, monitorScale: 1.0), precision: 6);
    }

    [Theory]
    [InlineData(0, 0, 1.0)]
    [InlineData(1600, 0, 1.0)]
    [InlineData(-5, 900, 1.0)]
    public void InvalidAreasNeverZoom(int width, int height, double expected)
    {
        Assert.Equal(expected, GameZoom.ComputeScale(200, width, height, monitorScale: 2.0));
        Assert.Equal(1, GameZoom.ComputeScale(GameZoom.Automatic, 3000, 1500, double.NaN));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 100)]
    [InlineData(-1, 100)]
    [InlineData(175, 175)]
    [InlineData(999, 300)]
    public void StoredValuesBecomeValidChoices(int stored, int expected)
    {
        Assert.Equal(expected, GameZoom.Normalize(stored));
    }

    [Fact]
    public void GameHostReadsWhatTheLauncherPublishes()
    {
        string name = @"Local\UrusLauncher.GameZoom.test." + Guid.NewGuid().ToString("N");
        using var reader = new GameZoom.Reader(name);
        Assert.Equal(GameZoom.Automatic, reader.Current);

        using var writer = new GameZoom.Writer(name);
        writer.Publish(150);
        using var lateReader = new GameZoom.Reader(name);
        Assert.Equal(150, lateReader.Current);

        writer.Publish(900);
        Assert.Equal(300, lateReader.Current);

        writer.Publish(GameZoom.Automatic);
        Assert.Equal(GameZoom.Automatic, lateReader.Current);
    }

    [Fact]
    public void AnUnwrittenBlockMeansAutomatic()
    {
        string name = @"Local\UrusLauncher.GameZoom.test." + Guid.NewGuid().ToString("N");
        using MemoryMappedFile created = MemoryMappedFile.CreateOrOpen(name, 8);

        using var reader = new GameZoom.Reader(name);

        Assert.Equal(GameZoom.Automatic, reader.Current);
    }

    [Fact]
    public async Task SettingsFromBeforeTheZoomAreAutomaticAndTheChoiceIsSaved()
    {
        string directory = Path.Combine(Path.GetTempPath(), "urus-zoom-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "settings.json");
        try
        {
            await File.WriteAllTextAsync(
                file,
                "{\"IsGameMuted\":false,\"LayoutMode\":1,\"LanguageCode\":\"pt-BR\",\"ColorSaturation\":60}");
            var service = new LauncherSettingsService(file);

            LauncherSettingsSnapshot before = await service.LoadAsync();
            Assert.Equal(GameZoom.Automatic, before.GameZoomChoice);
            Assert.Equal(60, before.ColorSaturation);

            await service.SaveGamePreferencesAsync(false, GameLayoutMode.Single, 60, 175);
            Assert.Equal(175, (await service.LoadAsync()).GameZoomChoice);

            await service.SaveGamePreferencesAsync(true, GameLayoutMode.Single);
            Assert.Equal(175, (await service.LoadAsync()).GameZoomChoice);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WorkspaceOffersEveryChoiceAndKeepsTheSelection()
    {
        var audio = new GameAudioService(static (_, _) => { }, TimeSpan.FromHours(1));
        using var workspace = new GameWorkspaceViewModel(audio, new LauncherSettingsService(), static (_, _) => null);

        Assert.Equal(GameZoom.Choices, workspace.ZoomOptions.Select(option => option.Value));
        Assert.Equal(GameZoom.Automatic, workspace.SelectedZoomOption?.Value);
        Assert.All(workspace.ZoomOptions, option => Assert.False(string.IsNullOrWhiteSpace(option.DisplayName)));

        workspace.SelectedZoomOption = workspace.ZoomOptions.Single(option => option.Value == 200);

        Assert.Equal(200, workspace.GameZoomChoice);
        Assert.Equal(200, workspace.SelectedZoomOption?.Value);
        Assert.Equal(workspace.SelectedZoomOption?.DisplayName, workspace.SelectedZoomOption?.ToString());
    }

    [Fact]
    public void ZoomSelectorSitsInTheGameToolbarWithExplicitOpening()
    {
        // the zoom lives in the display controls (DirectX, colors, zoom), which sit in the toolbar's action group
        XDocument workspace = XDocument.Load(FindRepositoryFile("src", "LegendLauncher.App", "Views", "Game", "GameWorkspaceView.xaml"));
        XElement displayControls = workspace
            .Descendants()
            .Single(element => element.Name.LocalName == "GameDisplayControls");
        Assert.Equal("WorkspaceActionGroup", displayControls.Parent?.Attribute(Xaml + "Name")?.Value);

        XDocument document = XDocument.Load(FindRepositoryFile("src", "LegendLauncher.App", "Views", "Game", "GameDisplayControls.xaml"));
        XElement selector = document
            .Descendants(Presentation + "ComboBox")
            .Single(element => element.Attribute(Xaml + "Name")?.Value == "WorkspaceZoomSelector");

        Assert.Equal("DisplayControlsGroup", selector.Parent?.Attribute(Xaml + "Name")?.Value);
        Assert.Equal("{Binding Workspace.ZoomOptions}", selector.Attribute("ItemsSource")?.Value);
        Assert.Equal("{Binding Workspace.SelectedZoomOption, Mode=TwoWay}", selector.Attribute("SelectedItem")?.Value);
        Assert.Equal("DisplayName", selector.Attribute("DisplayMemberPath")?.Value);
        Assert.Equal("ComboBoxSelector_OnPreviewMouseLeftButtonDown", selector.Attribute("PreviewMouseLeftButtonDown")?.Value);
        Assert.Equal("ComboBoxSelector_OnPreviewKeyDown", selector.Attribute("PreviewKeyDown")?.Value);
    }

    [Fact]
    public void LogicalRectangleKeepsTheOriginAndDividesTheSize()
    {
        var physical = new FlashDisplayScale.NativeRect(0, 0, 3760, 1550);

        FlashDisplayScale.NativeRect logical = FlashDisplayScale.ToLogical(physical, 1550 / 600d);

        Assert.Equal(0, logical.Left);
        Assert.Equal(0, logical.Top);
        Assert.Equal(1455, logical.Width);
        Assert.Equal(600, logical.Height);
        Assert.Equal(1, FlashDisplayScale.ToLogical(new FlashDisplayScale.NativeRect(0, 0, 1, 1), 3).Width);
    }

    [Theory]
    [InlineData(0x0200, true)]  // WM_MOUSEMOVE
    [InlineData(0x0201, true)]  // WM_LBUTTONDOWN
    [InlineData(0x0209, true)]  // WM_MBUTTONDBLCLK
    [InlineData(0x020B, true)]  // WM_XBUTTONDOWN
    [InlineData(0x02A1, true)]  // WM_MOUSEHOVER
    [InlineData(0x020A, false)] // WM_MOUSEWHEEL: screen coordinates (ScreenToClient converts them)
    [InlineData(0x020E, false)] // WM_MOUSEHWHEEL
    [InlineData(0x0100, false)] // WM_KEYDOWN
    public void OnlyClientMouseMessagesAreTranslated(int message, bool translated)
    {
        Assert.Equal(translated, FlashDisplayScale.IsClientMouseMessage(message));
    }

    [Fact]
    public void ZoomIsInstalledOnlyInTheGdiWindowModeAndRedirectsEveryGeometryCall()
    {
        string flash = File.ReadAllText(FindRepositoryFile("src", "LegendLauncher.GameHost.Legacy", "FlashActiveXControl.cs"));
        string imports = File.ReadAllText(FindRepositoryFile("src", "LegendLauncher.GameHost.Legacy", "FlashDisplayScale.Imports.cs"));
        string filter = File.ReadAllText(FindRepositoryFile("src", "LegendLauncher.GameHost.Legacy", "FlashColorFilter.cs"));

        Assert.Contains("options.WindowMode == GameWindowMode.Opaque", flash, StringComparison.Ordinal);
        Assert.True(
            flash.IndexOf("FlashColorFilter.Install(flashOcxPath)", StringComparison.Ordinal) <
            flash.IndexOf("FlashDisplayScale.Install(", StringComparison.Ordinal));
        Assert.Contains("protected override void DefWndProc(ref Message m)", flash, StringComparison.Ordinal);
        foreach (string function in new[]
        {
            "GetClientRect", "ScreenToClient", "ClientToScreen", "MapWindowPoints", "SetWindowPos", "MoveWindow",
            "InvalidateRect", "RedrawWindow", "BeginPaint", "SetCaretPos", "CreateCaret", "GetClipBox", "RectVisible",
        })
        {
            Assert.Contains($"Patch(module, \"{function}\"", imports, StringComparison.Ordinal);
        }

        Assert.Contains("FlashDisplayScale.TryMapDestination", filter, StringComparison.Ordinal);
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
