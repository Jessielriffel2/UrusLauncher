using System.Xml.Linq;
using LegendLauncher.App.Localization;
using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.GameColor;
using LegendLauncher.Core.GameDisplay;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.App;

/// <summary>DirectX button of the game toolbar: saved drawing mode, default for older settings and the greyed colors/zoom.</summary>
public sealed class GameWindowModeTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData(GameColorChannel.Original, GameZoom.Automatic, GameWindowMode.Direct)]
    [InlineData(60, GameZoom.Automatic, GameWindowMode.Opaque)]
    [InlineData(GameColorChannel.Original, 150, GameWindowMode.Opaque)]
    [InlineData(0, 300, GameWindowMode.Opaque)]
    public void WithoutASavedModeDirectXIsUsedUnlessColorsOrZoomWereChanged(
        int colorSaturation,
        int gameZoom,
        GameWindowMode expected)
    {
        Assert.Equal(expected, LauncherSettingsService.ResolveWindowMode(null, colorSaturation, gameZoom));
        Assert.Equal(expected, LauncherSettingsService.ResolveWindowMode("gpu", colorSaturation, gameZoom));
    }

    [Theory]
    [InlineData("direct", GameWindowMode.Direct)]
    [InlineData("opaque", GameWindowMode.Opaque)]
    public void ASavedModeWinsOverTheDefault(string saved, GameWindowMode expected)
    {
        Assert.Equal(expected, LauncherSettingsService.ResolveWindowMode(saved, 40, 200));
        Assert.Equal(expected, LauncherSettingsService.ResolveWindowMode(saved, GameColorChannel.Original, GameZoom.Automatic));
    }

    [Fact]
    public async Task SettingsFromBeforeTheButtonKeepTheZoomUserInCompatibleAndTheChoiceIsSaved()
    {
        string directory = Path.Combine(Path.GetTempPath(), "urus-wmode-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "settings.json");
        try
        {
            await File.WriteAllTextAsync(
                file,
                "{\"IsGameMuted\":false,\"LayoutMode\":1,\"LanguageCode\":\"pt-BR\",\"GameZoom\":175}");
            var service = new LauncherSettingsService(file);

            Assert.Equal(GameWindowMode.Opaque, (await service.LoadAsync()).WindowMode);

            await service.SaveGamePreferencesAsync(false, GameLayoutMode.Single, windowMode: GameWindowMode.Direct);
            Assert.Equal(GameWindowMode.Direct, (await service.LoadAsync()).WindowMode);
            Assert.Contains("\"direct\"", await File.ReadAllTextAsync(file), StringComparison.Ordinal);

            // a later save without the mode (an older caller) keeps the saved one
            await service.SaveGamePreferencesAsync(true, GameLayoutMode.Single);
            Assert.Equal(GameWindowMode.Direct, (await service.LoadAsync()).WindowMode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ANewPlayerStartsInDirectX()
    {
        var service = new LauncherSettingsService();

        Assert.Equal(GameWindowMode.Direct, (await service.LoadAsync()).WindowMode);
        Assert.Equal(GameWindowMode.Direct, LauncherSettingsSnapshot.Default.WindowMode);
    }

    [Fact]
    public async Task TheButtonSavesTheModeAndGreysColorsAndZoomOutInDirectX()
    {
        var localization = new LocalizationService("pt-BR");
        var settings = new LauncherSettingsService();
        using GameWorkspaceViewModel workspace = CreateWorkspace(settings, localization);
        workspace.ApplySettings(LauncherSettingsSnapshot.Default with { WindowMode = GameWindowMode.Opaque });

        Assert.False(workspace.IsDirectXMode);
        Assert.True(workspace.AreCompatibleOnlyControlsEnabled);
        Assert.Equal(localization.Get("Workspace_WindowModeCompatible"), workspace.WindowModeLabel);
        Assert.Equal(localization.Get("Workspace_ZoomTooltip"), workspace.ZoomToolTip);
        Assert.Equal(localization.Get("Workspace_ColorSaturationTooltip"), workspace.ColorSaturationToolTip);

        var changed = new List<string?>();
        workspace.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);
        workspace.IsDirectXMode = true;

        Assert.Equal(GameWindowMode.Direct, workspace.WindowMode);
        Assert.False(workspace.AreCompatibleOnlyControlsEnabled);
        Assert.Equal(localization.Get("Workspace_WindowModeDirectX"), workspace.WindowModeLabel);
        Assert.Equal(localization.Get("Workspace_CompatibleOnlyTooltip"), workspace.ZoomToolTip);
        Assert.Equal(localization.Get("Workspace_CompatibleOnlyTooltip"), workspace.ColorSaturationToolTip);
        Assert.Contains(nameof(GameWorkspaceViewModel.AreCompatibleOnlyControlsEnabled), changed);
        Assert.Contains(nameof(GameWorkspaceViewModel.IsDirectXMode), changed);
        Assert.Equal(GameWindowMode.Direct, (await settings.LoadAsync()).WindowMode);

        workspace.IsDirectXMode = false;
        Assert.Equal(GameWindowMode.Opaque, (await settings.LoadAsync()).WindowMode);
    }

    [Fact]
    public void ChangingTheModeWithSessionsOpenSaysItIsForTheNextTabsUntilOneOpens()
    {
        var localization = new LocalizationService("pt-BR");
        using GameWorkspaceViewModel workspace = CreateWorkspace(new LauncherSettingsService(), localization);
        workspace.ApplySettings(LauncherSettingsSnapshot.Default);

        workspace.IsDirectXMode = false;
        Assert.False(workspace.HasWindowModeNotice);

        _ = AddSession(workspace, 1);
        workspace.IsDirectXMode = true;
        Assert.True(workspace.HasWindowModeNotice);
        Assert.Equal(
            localization.Format("Workspace_WindowModeNoticeFormat", localization.Get("Workspace_WindowModeDirectX")),
            workspace.WindowModeNoticeText);

        _ = AddSession(workspace, 2);
        Assert.False(workspace.HasWindowModeNotice);
    }

    [Fact]
    public void TheNextTabsNoticeLivesInTheFooterNotInTheToolbar()
    {
        string workspace = File.ReadAllText(FindRepositoryFile("src", "LegendLauncher.App", "Views", "Game", "GameWorkspaceView.xaml"));
        string displayControls = File.ReadAllText(FindRepositoryFile("src", "LegendLauncher.App", "Views", "Game", "GameDisplayControls.xaml"));

        Assert.Contains("Workspace.WindowModeNoticeText", workspace, StringComparison.Ordinal);
        Assert.Contains("Workspace.HasWindowModeNotice", workspace, StringComparison.Ordinal);
        Assert.DoesNotContain("HasWindowModeNotice", displayControls, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDirectXSwitchSitsBeforeColorsAndZoomWhichFollowTheMode()
    {
        XDocument document = XDocument.Load(FindRepositoryFile("src", "LegendLauncher.App", "Views", "Game", "GameDisplayControls.xaml"));
        XElement group = document
            .Descendants(Presentation + "StackPanel")
            .Single(element => element.Attribute(Xaml + "Name")?.Value == "DisplayControlsGroup");
        XElement[] children = group.Elements().ToArray();

        Assert.Equal(Presentation + "ToggleButton", children[0].Name);
        Assert.Equal("{Binding Workspace.IsDirectXMode, Mode=TwoWay}", children[0].Attribute("IsChecked")?.Value);
        Assert.Equal("ColorSaturationControl", children[1].Attribute(Xaml + "Name")?.Value);
        Assert.Equal("WorkspaceZoomSelector", children[2].Attribute(Xaml + "Name")?.Value);
        foreach (XElement compatibleOnly in children[1..])
        {
            Assert.Equal("{Binding Workspace.AreCompatibleOnlyControlsEnabled}", compatibleOnly.Attribute("IsEnabled")?.Value);
        }
    }

    [Fact]
    public void TheGameHostInstallsColorsAndZoomOnlyInTheCompatibleMode()
    {
        string flash = File.ReadAllText(FindRepositoryFile("src", "LegendLauncher.GameHost.Legacy", "FlashActiveXControl.cs"));
        int condition = flash.IndexOf("if (options.WindowMode == GameWindowMode.Opaque)", StringComparison.Ordinal);

        Assert.True(condition > 0);
        Assert.True(condition < flash.IndexOf("FlashColorFilter.Install(flashOcxPath)", StringComparison.Ordinal));
        Assert.True(condition < flash.IndexOf("FlashDisplayScale.Install(", StringComparison.Ordinal));
        Assert.True(
            flash.IndexOf("FlashPointerIsolation.Install(flashOcxPath)", StringComparison.Ordinal) < condition,
            "the pointer isolation is needed in both modes");
    }

    private static GameWorkspaceViewModel CreateWorkspace(LauncherSettingsService settings, LocalizationService localization)
    {
        var audio = new GameAudioService(static (_, _) => { }, TimeSpan.FromHours(1));
        return new GameWorkspaceViewModel(audio, settings, static (_, _) => null, localization);
    }

    private static GameSessionViewModel AddSession(GameWorkspaceViewModel workspace, int index)
    {
        Guid id = Guid.NewGuid();
        PlatformDefinition platform = OasPlatformCatalog.Brazil;
        string serverId = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var profile = new AccountProfile(
            id,
            $"Perfil {index}",
            platform.Id,
            $"player{index}@example.test",
            $"LegendLauncherNext/profile/{id:N}",
            index,
            serverId,
            AppTestData.Now,
            AppTestData.Now);
        var session = new GameSession(900_100 + index, (nint)(0x2000 + index), AppTestData.Now);
        return workspace.AddSession(profile, platform, AppTestData.Server(serverId), session);
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
