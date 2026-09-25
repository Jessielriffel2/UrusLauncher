using System.ComponentModel;
using System.Xml.Linq;
using LegendLauncher.App.Localization;
using LegendLauncher.App.MacroAssistant;

namespace LegendLauncher.Tests.App;

public sealed class MacroAssistantContractTests
{
    [Fact]
    public void DirectInput_UsesWindowMessagesInsteadOfGlobalMouseApis()
    {
        string source = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "MacroAssistant",
            "DirectGameInput.cs"));
        string nativeMethods = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "GameHosting",
            "NativeWindowMethods.cs"));

        Assert.Contains("NativeWindowMethods.PostMessage", source, StringComparison.Ordinal);
        Assert.Contains("EntryPoint = \"PostMessage\"", nativeMethods, StringComparison.Ordinal);
        Assert.DoesNotContain("SetCursorPos", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mouse_event", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SendInput", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectInput_PacksCoordinatesInMouseMessagePosition()
    {
        long packed = DirectGameInput.MakePosition(3, 4).ToInt64();

        Assert.Equal(0x00040003, packed);
    }

    [Fact]
    public void ToggleAllCommandStopsEveryActiveSession()
    {
        using var viewModel = new MacroAssistantViewModel(
            new LocalizationService("pt-BR"));
        var first = new FakeMacroSession(isActive: true);
        var second = new FakeMacroSession(isActive: true);
        viewModel.AddController(first);
        viewModel.AddController(second);

        Assert.True(viewModel.HasActiveSessions);
        viewModel.ToggleAllCommand.Execute(null);

        Assert.Equal(1, first.StopCount);
        Assert.Equal(1, second.StopCount);
        Assert.Equal(0, first.StartCount);
        Assert.Equal(0, second.StartCount);
        Assert.False(viewModel.HasActiveSessions);
    }

    [Fact]
    public void WorkspaceAndDetachedWindow_ExposeOneMacroToggleControl()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XDocument workspace = XDocument.Load(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "Views",
            "Game",
            "GameWorkspaceView.xaml"));
        XDocument detached = XDocument.Load(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "Views",
            "Game",
            "DetachedGameWindow.xaml"));

        string workspaceMarkup = workspace.ToString();
        string detachedMarkup = detached.ToString();

        Assert.Contains("Workspace.MacroAssistant.ToggleAllCommand", workspaceMarkup, StringComparison.Ordinal);
        Assert.Contains("Workspace.MacroAssistant.CycleModeCommand", workspaceMarkup, StringComparison.Ordinal);
        Assert.Contains("Workspace.MacroAssistant.ToggleAllCommand", detachedMarkup, StringComparison.Ordinal);
        Assert.Contains("Workspace.MacroAssistant.CycleModeCommand", detachedMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("Workspace.MacroAssistant.StopAllCommand", workspaceMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("Workspace.MacroAssistant.StopAllCommand", detachedMarkup, StringComparison.Ordinal);
        Assert.Equal(
            1,
            workspace.Descendants(presentation + "Button").Count(element =>
                element.Attribute("Command")?.Value ==
                "{Binding Workspace.MacroAssistant.ToggleAllCommand}"));
        Assert.Equal(
            1,
            detached.Descendants(presentation + "Button").Count(element =>
                element.Attribute("Command")?.Value ==
                "{Binding Workspace.MacroAssistant.ToggleAllCommand}"));
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", workspaceMarkup, StringComparison.Ordinal);
        Assert.Contains("Workspace.CloseSessionCommand", workspaceMarkup, StringComparison.Ordinal);
        Assert.NotEmpty(workspace.Descendants(presentation + "Button"));
    }

    [Fact]
    public void MacroSetup_RequiresPlayBeforeWorkerExecution()
    {
        string controller = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "MacroAssistant",
            "MacroSessionController.cs"));
        string setup = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "MacroAssistant",
            "MacroSetupWindow.cs"));

        Assert.Contains("OpenSetup", controller, StringComparison.Ordinal);
        Assert.Contains("SetupOnPlayRequested", controller, StringComparison.Ordinal);
        Assert.Contains("BeginExecution", controller, StringComparison.Ordinal);
        Assert.Contains("RunClickLoopAsync", controller, StringComparison.Ordinal);
        Assert.Contains("GetClickSettings", setup, StringComparison.Ordinal);
        Assert.Contains("PlayRequested", setup, StringComparison.Ordinal);
        Assert.Contains("CreateButton(\"Play\"", setup, StringComparison.Ordinal);
    }

    [Fact]
    public void MacroAssistant_PersistsProfileModePreferencesAndUsesCompactTarget()
    {
        string controller = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "MacroAssistant",
            "MacroSessionController.cs"));
        string setup = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "MacroAssistant",
            "MacroSetupWindow.cs"));
        string preferences = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "Services",
            "ProfilePreferencesStore.cs"));

        Assert.Contains("ProfilePreferencesStore", controller, StringComparison.Ordinal);
        Assert.Contains("GetProfilePreferences", controller, StringComparison.Ordinal);
        Assert.Contains("ScaleDelay", controller, StringComparison.Ordinal);
        Assert.Contains("GetProfilePreferences", setup, StringComparison.Ordinal);
        Assert.Contains("UseLargeFrame", setup, StringComparison.Ordinal);
        Assert.Contains("SettingsChanged", setup, StringComparison.Ordinal);
        Assert.Contains("MacroMode", preferences, StringComparison.Ordinal);
        Assert.Contains("_openingSetup", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void MacroAssistant_UsesLauncherThemedToggleAndKeepsTheAimCenteredInTheFrame()
    {
        string setup = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "MacroAssistant",
            "MacroSetupWindow.cs"));
        string preferences = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "MacroAssistant",
            "ProfileMacroPreferences.cs"));

        // The frame checkbox was replaced by a launcher-themed toggle.
        Assert.DoesNotContain("new CheckBox", setup, StringComparison.Ordinal);
        Assert.Contains("ToggleButton", setup, StringComparison.Ordinal);
        Assert.Contains("CreateLargeFrameToggleTemplate", setup, StringComparison.Ordinal);

        // Gems and Cosmo always show the frame; only Clicks shows the aim marker.
        Assert.Contains("bool showAim = _mode == MacroMode.Clicks", setup, StringComparison.Ordinal);
        Assert.Contains("if (!isClicks)", setup, StringComparison.Ordinal);
        Assert.Contains("_largeFrameToggleRow.Visibility", setup, StringComparison.Ordinal);

        // The aim follows the frame when it is dragged or resized.
        Assert.Contains("RegionCenter", setup, StringComparison.Ordinal);
        Assert.Contains("MoveRegionBy", setup, StringComparison.Ordinal);
        Assert.Contains("_frameMoveThumb", setup, StringComparison.Ordinal);
        Assert.Matches(@"0\.35,\s+true,", preferences);
    }

    [Fact]
    public void MacroAssistant_StopsWhenTheLauncherOrTheGameSurfaceIsUnavailable()
    {
        string controller = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "MacroAssistant",
            "MacroSessionController.cs"));
        string coordinator = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "MacroAssistant",
            "MacroAssistantCoordinator.cs"));
        string nativeMethods = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "GameHosting",
            "NativeWindowMethods.cs"));

        Assert.Contains("IsExecutionSurfaceAvailable", controller, StringComparison.Ordinal);
        Assert.Contains("IsOwnerAvailable", controller, StringComparison.Ordinal);
        Assert.Contains("NativeWindowMethods.IsWindowVisible", controller, StringComparison.Ordinal);
        Assert.Contains("NativeWindowMethods.IsWindowMinimized", controller, StringComparison.Ordinal);
        Assert.Contains("StopIfOwnerUnavailable", coordinator, StringComparison.Ordinal);
        Assert.Contains("OwnerOnStateChanged", coordinator, StringComparison.Ordinal);
        Assert.Contains("OwnerOnIsVisibleChanged", coordinator, StringComparison.Ordinal);
        Assert.Contains("IsWindowVisible", nativeMethods, StringComparison.Ordinal);
        Assert.Contains("IsWindowMinimized", nativeMethods, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeWindowMethods_DeclareTheRealUser32EntryPoints()
    {
        string nativeMethods = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "GameHosting",
            "NativeWindowMethods.cs"));

        // The managed names are suffixed to avoid clashing with the wrappers, so the
        // native entry points must be declared explicitly or user32.dll cannot resolve them.
        Assert.Contains("EntryPoint = \"IsWindowVisible\"", nativeMethods, StringComparison.Ordinal);
        Assert.Contains("EntryPoint = \"IsIconic\"", nativeMethods, StringComparison.Ordinal);
    }

    [Fact]
    public void LauncherAndWorkspace_ExposeDragAvatarAndSelectedSessionRelogControls()
    {
        string main = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "MainWindow.xaml"));
        string mainCode = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "MainWindow.xaml.cs"));
        string workspace = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "Views",
            "Game",
            "GameWorkspaceView.xaml"));
        string workspaceCode = File.ReadAllText(FindRepositoryFile(
            "src",
            "LegendLauncher.App",
            "Views",
            "Game",
            "GameWorkspaceView.xaml.cs"));

        Assert.Contains("LauncherHeader_OnMouseLeftButtonDown", main, StringComparison.Ordinal);
        Assert.Contains("SelectedProfileAvatarImage", main, StringComparison.Ordinal);
        Assert.Contains("ChooseProfileImageButton_OnClick", main, StringComparison.Ordinal);
        Assert.Contains("BorderlessWindowCommands.TryDrag", mainCode, StringComparison.Ordinal);
        Assert.Contains("CompactGameToolbar_OnMouseLeftButtonDown", workspace, StringComparison.Ordinal);
        Assert.Contains("Workspace.RelogSessionCommand", workspace, StringComparison.Ordinal);
        Assert.Contains("Workspace.SelectedSession", workspace, StringComparison.Ordinal);
        Assert.Contains("BorderlessWindowCommands.TryDrag", workspaceCode, StringComparison.Ordinal);
    }

    [Fact]
    public void SurfaceGeometry_RejectsEmptyOrNegativeSurfaces()
    {
        Assert.False(new SurfaceGeometry(0, 0, 0, 100).HasArea);
        Assert.False(new SurfaceGeometry(0, 0, 100, -1).HasArea);
        Assert.True(new SurfaceGeometry(10, 20, 100, 100).HasArea);
        Assert.True(new SurfaceRegion(10, 20, 100, 100).HasArea);
        Assert.False(new SurfaceRegion(10, 20, 0, 100).HasArea);
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

    private sealed class FakeMacroSession(bool isActive) : IMacroSession
    {
        private bool _isActive = isActive;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsActive => _isActive;

        public MacroSessionState State =>
            _isActive ? MacroSessionState.Running : MacroSessionState.Stopped;

        public string StatusText => _isActive ? "Running" : "Stopped";

        public string SessionTitle => "Fake session";

        public bool OverlayVisible { get; set; }

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public void SetMode(MacroMode mode)
        {
        }

        public void Start()
        {
            StartCount++;
            SetActive(true);
        }

        public void Stop()
        {
            StopCount++;
            SetActive(false);
        }

        public void Dispose()
        {
        }

        private void SetActive(bool value)
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
        }
    }
}
