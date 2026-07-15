using LegendLauncher.App.Localization;
using LegendLauncher.App.Services;
using LegendLauncher.App.Updates;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.App;

public sealed class LauncherUpdateViewModelTests
{
    [Fact]
    public async Task CheckShowsAvailableReleaseWithoutDownloadingOrLaunching()
    {
        var updates = new StubLauncherUpdateService(CreateRelease());
        var localization = new LocalizationService("pt-BR");
        using MainWindowViewModel viewModel = CreateViewModel(updates, localization);

        await viewModel.CheckForUpdatesAsync();

        Assert.True(viewModel.IsUpdateCardVisible);
        Assert.True(viewModel.IsUpdateAvailable);
        Assert.Contains("1.1.0", viewModel.UpdateTitleText, StringComparison.Ordinal);
        Assert.Equal("Notas em português.", viewModel.UpdateNotesText);
        Assert.True(viewModel.InstallUpdateCommand.CanExecute(null));
        Assert.Equal(0, updates.DownloadCount);
        Assert.Equal(0, updates.LaunchCount);
    }

    [Fact]
    public async Task SameOrOlderReleaseLeavesLauncherInCurrentState()
    {
        var updates = new StubLauncherUpdateService(release: null);
        using MainWindowViewModel viewModel = CreateViewModel(
            updates,
            new LocalizationService("en-US"));

        await viewModel.CheckForUpdatesAsync();

        Assert.True(viewModel.IsUpdateCardVisible);
        Assert.False(viewModel.IsUpdateAvailable);
        Assert.Equal("Launcher is up to date", viewModel.UpdateTitleText);
        Assert.False(viewModel.InstallUpdateCommand.CanExecute(null));
    }

    [Fact]
    public async Task InstallDownloadsAndLaunchesOnlyAfterExplicitAction()
    {
        var updates = new StubLauncherUpdateService(CreateRelease());
        using MainWindowViewModel viewModel = CreateViewModel(
            updates,
            new LocalizationService("pt-BR"));
        bool installerStarted = false;
        viewModel.UpdateInstallerStarted += (_, _) => installerStarted = true;
        await viewModel.CheckForUpdatesAsync();

        Assert.Equal(0, updates.DownloadCount);
        Assert.Equal(0, updates.LaunchCount);

        await viewModel.InstallUpdateAsync();

        Assert.Equal(1, updates.DownloadCount);
        Assert.Equal(1, updates.LaunchCount);
        Assert.True(installerStarted);
    }

    [Fact]
    public async Task ActiveGameSessionBlocksUpdateInstallation()
    {
        var updates = new StubLauncherUpdateService(CreateRelease());
        using MainWindowViewModel viewModel = CreateViewModel(
            updates,
            new LocalizationService("pt-BR"));
        await viewModel.CheckForUpdatesAsync();
        AddSession(viewModel.Workspace);

        await viewModel.InstallUpdateAsync();

        Assert.False(viewModel.InstallUpdateCommand.CanExecute(null));
        Assert.Contains("Feche", viewModel.UpdateDetailText, StringComparison.Ordinal);
        Assert.Equal(0, updates.DownloadCount);
        Assert.Equal(0, updates.LaunchCount);
    }

    [Fact]
    public async Task SessionStartedDuringDownloadPreventsInstallerLaunch()
    {
        MainWindowViewModel? viewModel = null;
        var updates = new StubLauncherUpdateService(
            CreateRelease(),
            afterDownload: () => AddSession(viewModel!.Workspace));
        using (viewModel = CreateViewModel(
            updates,
            new LocalizationService("pt-BR")))
        {
            await viewModel.CheckForUpdatesAsync();
            await viewModel.InstallUpdateAsync();

            Assert.Equal(1, updates.DownloadCount);
            Assert.Equal(0, updates.LaunchCount);
            Assert.True(viewModel.IsUpdateAvailable);
            Assert.Contains("Feche", viewModel.UpdateDetailText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task DisposedViewModelDoesNotStartAnotherUpdateCheck()
    {
        var updates = new StubLauncherUpdateService(CreateRelease());
        MainWindowViewModel viewModel = CreateViewModel(
            updates,
            new LocalizationService("pt-BR"));
        viewModel.Dispose();

        await viewModel.CheckForUpdatesAsync();

        Assert.Equal(0, updates.CheckCount);
    }

    [Fact]
    public async Task NetworkFailureIsNonBlockingAndCanBeRetried()
    {
        var updates = new StubLauncherUpdateService(
            release: null,
            checkException: new HttpRequestException("offline"));
        using MainWindowViewModel viewModel = CreateViewModel(
            updates,
            new LocalizationService("es-ES"));

        await viewModel.CheckForUpdatesAsync();

        Assert.True(viewModel.IsUpdateRetryVisible);
        Assert.Contains("No se pudo", viewModel.UpdateTitleText, StringComparison.Ordinal);
        Assert.True(viewModel.CheckForUpdatesCommand.CanExecute(null));
    }

    [Fact]
    public async Task ReleaseNotesFollowTheSelectedLanguageAtRuntime()
    {
        var updates = new StubLauncherUpdateService(CreateRelease());
        var localization = new LocalizationService("pt-BR");
        using MainWindowViewModel viewModel = CreateViewModel(updates, localization);
        await viewModel.CheckForUpdatesAsync();

        localization.SetLanguage("en-US");
        Assert.Equal("English notes.", viewModel.UpdateNotesText);
        Assert.Equal("SEE WHAT'S NEW", viewModel.UpdateViewNotesText);

        localization.SetLanguage("es-ES");
        Assert.Equal("Notas en español.", viewModel.UpdateNotesText);
        Assert.Equal("VER NOVEDADES", viewModel.UpdateViewNotesText);
    }

    private static MainWindowViewModel CreateViewModel(
        ILauncherUpdateService updateService,
        LocalizationService localization)
    {
        var profiles = new InMemoryProfileStore();
        var vault = new InMemoryCredentialVault();
        var profileStorage = new ProfileStorageCoordinator(profiles, vault);
        var authentication = new StubAuthenticationService((_, _) =>
            Task.FromResult(AuthenticationResult.Failure("unused")));
        var launcher = new SessionLaunchCoordinator(
            vault,
            authentication,
            new StubGameRuntime(),
            "C:\\legacy-runtime",
            profiles);
        var workspace = new GameWorkspaceViewModel(
            new GameAudioService(static (_, _) => { }, TimeSpan.FromHours(1)),
            new LauncherSettingsService(),
            static (_, _) => null,
            localization);
        var directory = new StubServerDirectory((_, _, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(AppTestData.Catalog([AppTestData.Server()]));
        });
        return new MainWindowViewModel(
            directory,
            profileStorage,
            launcher,
            AppTestData.UsableRuntime(),
            OasPlatformCatalog.All,
            settingsService: new LauncherSettingsService(),
            workspace: workspace,
            localization: localization,
            updateService: updateService,
            currentVersion: new Version(1, 0, 1));
    }

    private static LauncherUpdateRelease CreateRelease() =>
        new(
            new Version(1, 1, 0),
            "v1.1.0",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["pt-BR"] = "Notas em português.",
                ["en-US"] = "English notes.",
                ["es-ES"] = "Notas en español.",
            },
            new LauncherUpdateInstaller(
                "UrusLauncher-Setup-1.1.0-win-x64.exe",
                123,
                new string('A', 64),
                new Uri("https://github.com/Jessielriffel2/UrusLauncher/releases/download/v1.1.0/UrusLauncher-Setup-1.1.0-win-x64.exe")));

    private static void AddSession(GameWorkspaceViewModel workspace)
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", 1, "3257");
        _ = workspace.AddSession(
            profile,
            OasPlatformCatalog.Brazil,
            AppTestData.Server(),
            new GameSession(987_654, new nint(0x1234), AppTestData.Now));
    }

    private sealed class StubLauncherUpdateService(
        LauncherUpdateRelease? release,
        Exception? checkException = null,
        Action? afterDownload = null) : ILauncherUpdateService
    {
        public int CheckCount { get; private set; }

        public int DownloadCount { get; private set; }

        public int LaunchCount { get; private set; }

        public Task<LauncherUpdateRelease?> CheckForUpdateAsync(
            Version currentVersion,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckCount++;
            return checkException is null
                ? Task.FromResult(release)
                : Task.FromException<LauncherUpdateRelease?>(checkException);
        }

        public Task<DownloadedLauncherInstaller> DownloadInstallerAsync(
            LauncherUpdateRelease availableRelease,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DownloadCount++;
            progress?.Report(1);
            afterDownload?.Invoke();
            return Task.FromResult(new DownloadedLauncherInstaller(
                "C:\\updates\\UrusLauncher-Setup-1.1.0-win-x64.exe",
                availableRelease.Installer.Name,
                availableRelease.Installer.Bytes,
                availableRelease.Installer.Sha256));
        }

        public Task LaunchInstallerAsync(
            DownloadedLauncherInstaller installer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LaunchCount++;
            return Task.CompletedTask;
        }
    }
}
