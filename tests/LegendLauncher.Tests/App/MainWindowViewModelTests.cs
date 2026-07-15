using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.App;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task SelectingAnotherProfileOnSamePlatformReloadsItsProviderUserId()
    {
        AccountProfile first = AppTestData.Profile("first@example.test", 101, "100", updatedOffset: 2);
        AccountProfile second = AppTestData.Profile("second@example.test", 202, "200", updatedOffset: 1);
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog(
                [AppTestData.Server("100"), AppTestData.Server("200")])));
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(first, second),
            new InMemoryCredentialVault());

        await viewModel.InitializeAsync();
        viewModel.SelectedProfile = viewModel.Profiles.Single(profile => profile.Model.Id == second.Id);

        Assert.Equal(202, directory.Requests.Last().UserId);
    }

    [Fact]
    public async Task EachProfilePinsItsActualMostRecentPlayedServer()
    {
        AccountProfile first = AppTestData.Profile("first@example.test", 101, "100", updatedOffset: 2) with
        {
            RecentServerIds = ["200", "100"],
        };
        AccountProfile second = AppTestData.Profile("second@example.test", 202, "200", updatedOffset: 1) with
        {
            RecentServerIds = ["100", "200"],
        };
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog(
            [
                AppTestData.Server("100", opensAt: AppTestData.Now.AddDays(-3)),
                AppTestData.Server("200", opensAt: AppTestData.Now.AddDays(-2)),
                AppTestData.Server("300", opensAt: AppTestData.Now.AddDays(-1)),
            ])));
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(first, second),
            new InMemoryCredentialVault());

        await viewModel.InitializeAsync();

        Assert.Equal("200", viewModel.VisibleServers[0].Id);
        Assert.True(viewModel.VisibleServers[0].ShowRecommendedBadge);
        Assert.Equal("300", viewModel.VisibleServers[1].Id);
        Assert.True(viewModel.VisibleServers[1].ShowLatestBadge);
        Assert.True(viewModel.VisibleServers[1].ShowSectionDivider);

        viewModel.SelectedProfile = viewModel.Profiles.Single(profile => profile.Model.Id == second.Id);

        Assert.Equal("100", viewModel.VisibleServers[0].Id);
        Assert.True(viewModel.VisibleServers[0].ShowRecommendedBadge);
        Assert.Equal("300", viewModel.VisibleServers[1].Id);
        Assert.True(viewModel.VisibleServers[1].ShowLatestBadge);
    }

    [Fact]
    public async Task SupersededCatalogResponseCannotReplaceNewProfileServers()
    {
        AccountProfile first = AppTestData.Profile("first@example.test", 101, "100");
        AccountProfile second = AppTestData.Profile("second@example.test", 202, "200");
        var delayedFirst = new TaskCompletionSource<ServerCatalog>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var directory = new StubServerDirectory((_, userId, _) => userId switch
        {
            101 => delayedFirst.Task,
            202 => Task.FromResult(AppTestData.Catalog([AppTestData.Server("200")])),
            _ => Task.FromResult(AppTestData.Catalog([])),
        });
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(),
            new InMemoryCredentialVault());
        var firstItem = new ProfileItemViewModel(first);
        var secondItem = new ProfileItemViewModel(second);
        viewModel.Profiles.Add(firstItem);
        viewModel.Profiles.Add(secondItem);

        viewModel.SelectedProfile = firstItem;
        viewModel.SelectedProfile = secondItem;
        Assert.Equal("200", viewModel.VisibleServers.Single().Id);

        delayedFirst.SetResult(AppTestData.Catalog([AppTestData.Server("100")]));
        await Task.Delay(25);

        Assert.Equal("200", viewModel.VisibleServers.Single().Id);
        Assert.Equal(202, directory.Requests.Last().UserId);
    }

    [Fact]
    public async Task ClearingSearchRestoresSelectionThatWasActiveBeforeFilter()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", null, "100");
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog(
                [AppTestData.Server("100"), AppTestData.Server("200")])));
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(profile),
            new InMemoryCredentialVault());
        await viewModel.InitializeAsync();
        Assert.Equal("100", viewModel.SelectedServer?.Id);

        viewModel.SearchText = "200";
        Assert.Equal("200", viewModel.SelectedServer?.Id);

        viewModel.SearchText = string.Empty;
        Assert.Equal("100", viewModel.SelectedServer?.Id);
    }

    [Fact]
    public async Task RecentServersResolveInHistoryOrderAndCommandSelectsOne()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", null, "100") with
        {
            RecentServerIds = ["200", "missing", "100", "200"],
        };
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog(
                [AppTestData.Server("100"), AppTestData.Server("200")])));
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(profile),
            new InMemoryCredentialVault());

        await viewModel.InitializeAsync();

        Assert.Equal(["200", "100"], viewModel.RecentServers.Select(server => server.Id));
        viewModel.SearchText = "100";
        ServerRowViewModel recent = viewModel.RecentServers[0];
        Assert.True(viewModel.SelectRecentServerCommand.CanExecute(recent));

        viewModel.SelectRecentServerCommand.Execute(recent);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal("200", viewModel.SelectedServer?.Id);
    }

    [Fact]
    public async Task GameReadinessRequiresTypedOrSavedCredential()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", null, "100");
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog([AppTestData.Server("100")])));
        var vault = new InMemoryCredentialVault();
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(profile),
            vault);
        await viewModel.InitializeAsync();
        await Task.Yield();

        Assert.False(viewModel.CanStartGame);
        Assert.False(viewModel.HasSavedCredential);
        Assert.Equal("Digite sua senha", viewModel.PasswordPlaceholderText);
        viewModel.PendingPassword = "typed-secret";
        Assert.True(viewModel.CanStartGame);
        viewModel.PendingPassword = string.Empty;
        Assert.False(viewModel.CanStartGame);

        vault.Seed(profile.CredentialKey, new CredentialSecret(profile.UserName, "vault-secret"));
        viewModel.SelectedProfile = null;
        viewModel.SelectedProfile = viewModel.Profiles.Single();
        await Task.Yield();
        Assert.True(viewModel.CanStartGame);
        Assert.True(viewModel.HasSavedCredential);
        Assert.Equal("Senha salva no Cofre do Windows", viewModel.PasswordPlaceholderText);
    }

    [Fact]
    public async Task SavedRebornProfileCanLaunchClassicPortugueseS100()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", 715, "115") with
        {
            PlatformId = OasPlatformCatalog.RebornTurkish.Id,
            RecentServerIds = ["115"],
        };
        var directory = new StubServerDirectory((platform, _, _) =>
            Task.FromResult(CatalogFor(
                platform,
                platform.Id == OasPlatformCatalog.ClassicPortuguese.Id
                    ? [ServerFor(platform, "100")]
                    : [ServerFor(platform, "115")])));
        var vault = new InMemoryCredentialVault();
        vault.Seed(profile.CredentialKey, new CredentialSecret(profile.UserName, "vault-secret"));
        var authentication = new StubAuthenticationService((request, _) =>
            Task.FromResult(AuthenticationResult.Success(
                new LaunchSession(
                    new Uri("https://s100sqptclas.creaction-network.com/client/Loading.swf")),
                901)));
        var profiles = new InMemoryProfileStore(profile);
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            profiles,
            vault,
            authentication);

        await viewModel.InitializeAsync();
        await WaitUntilAsync(() => viewModel.HasSavedCredential && !viewModel.IsLoading);
        Assert.Equal(OasPlatformCatalog.RebornTurkish.Id, viewModel.SelectedPlatform.Id);

        viewModel.SelectedPlatform = viewModel.Platforms.Single(platform =>
            platform.Id == OasPlatformCatalog.ClassicPortuguese.Id);
        await WaitUntilAsync(() =>
            viewModel.HasSavedCredential &&
            !viewModel.IsLoading &&
            viewModel.SelectedServer?.Id == "100");

        Assert.Equal(0, directory.Requests.Last().UserId);
        Assert.True(viewModel.CanStartGame);

        await viewModel.StartGameAsync();

        AuthenticationRequest request = Assert.Single(authentication.Requests);
        Assert.Equal(OasPlatformCatalog.ClassicPortuguese.Id, request.Platform.Id);
        Assert.Equal("100", request.Server.Id);
        Assert.Equal("lorpt.creaction-network.com", request.Server.LaunchUri?.Host);
        GameSessionViewModel session = Assert.Single(viewModel.Workspace.Sessions);
        Assert.Equal(profile.Id, session.ProfileId);
        Assert.Equal(OasPlatformCatalog.ClassicPortuguese.Id, session.PlatformId);
        Assert.Equal("100", session.ServerId);
        AccountProfile persisted = Assert.Single(profiles.Values);
        Assert.Equal(profile.Id, persisted.Id);
        Assert.Equal("100", persisted.GetLastServerId(OasPlatformCatalog.ClassicPortuguese.Id));
        Assert.Equal("115", persisted.GetLastServerId(OasPlatformCatalog.RebornTurkish.Id));
        Assert.True(vault.Contains(profile.CredentialKey));
    }

    [Fact]
    public async Task RejectedSavedCredentialRequiresRetypeWithoutDeletingVaultEntry()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", null, "100");
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog([AppTestData.Server("100")])));
        var vault = new InMemoryCredentialVault();
        vault.Seed(profile.CredentialKey, new CredentialSecret(profile.UserName, "stale-secret"));
        var authentication = new StubAuthenticationService((_, _) =>
            Task.FromResult(AuthenticationResult.Failure("invalid_credentials")));
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(profile),
            vault,
            authentication);
        await viewModel.InitializeAsync();
        await Task.Yield();
        Assert.True(viewModel.HasSavedCredential);

        await viewModel.StartGameAsync();

        Assert.False(viewModel.HasSavedCredential);
        Assert.False(viewModel.CanStartGame);
        Assert.Equal("Digite sua senha", viewModel.PasswordPlaceholderText);
        Assert.Contains("Digite-a novamente", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.True(vault.Contains(profile.CredentialKey));
    }

    [Fact]
    public async Task SuccessfulLaunchCreatesEmbeddedSessionAndReusesItForSameProfile()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", null, "100");
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog([AppTestData.Server("100")])));
        var vault = new InMemoryCredentialVault();
        var authentication = new StubAuthenticationService((_, _) =>
            Task.FromResult(AuthenticationResult.Success(
                new LaunchSession(new Uri("https://lobr.creaction-network.com/client/Loading.swf")),
                777)));
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(profile),
            vault,
            authentication);
        await viewModel.InitializeAsync();
        viewModel.PendingPassword = "typed-secret";

        await viewModel.StartGameAsync();

        Assert.True(viewModel.IsWorkspaceVisible);
        Assert.Single(viewModel.Workspace.Sessions);
        Assert.Equal(profile.Id, viewModel.Workspace.SelectedSession?.ProfileId);
        Assert.Equal("Sessão ativa neste launcher", viewModel.CredentialStatusText);
        await viewModel.StartGameAsync();
        Assert.Single(viewModel.Workspace.Sessions);
        Assert.Single(authentication.Requests);
    }

    [Fact]
    public async Task ActiveSessionIsReusedOnlyForExactProfilePlatformAndServer()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", null, "100");
        var directory = new StubServerDirectory((platform, _, _) =>
            Task.FromResult(CatalogFor(
                platform,
                platform.Id == OasPlatformCatalog.ClassicPortuguese.Id
                    ? [ServerFor(platform, "100")]
                    : [ServerFor(platform, "100"), ServerFor(platform, "101")])));
        var vault = new InMemoryCredentialVault();
        vault.Seed(profile.CredentialKey, new CredentialSecret(profile.UserName, "vault-secret"));
        var authentication = SuccessfulAuthentication();
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(profile),
            vault,
            authentication);
        await viewModel.InitializeAsync();
        await WaitUntilAsync(() => viewModel.HasSavedCredential && !viewModel.IsLoading);

        await viewModel.StartGameAsync();
        await viewModel.StartGameAsync();
        Assert.Single(authentication.Requests);
        Assert.Single(viewModel.Workspace.Sessions);

        viewModel.SelectedServer = viewModel.VisibleServers.Single(server => server.Id == "101");
        Assert.True(viewModel.CanStartGame);
        await viewModel.StartGameAsync();
        Assert.Equal(2, authentication.Requests.Count);
        Assert.Equal(2, viewModel.Workspace.Sessions.Count);
        Assert.Equal("101", viewModel.Workspace.SelectedSession?.ServerId);

        viewModel.SelectedPlatform = viewModel.Platforms.Single(platform =>
            platform.Id == OasPlatformCatalog.ClassicPortuguese.Id);
        await WaitUntilAsync(() =>
            viewModel.HasSavedCredential &&
            !viewModel.IsLoading &&
            viewModel.SelectedServer?.Id == "100");
        Assert.True(viewModel.CanStartGame);
        await viewModel.StartGameAsync();
        Assert.Equal(3, authentication.Requests.Count);
        Assert.Equal(3, viewModel.Workspace.Sessions.Count);
        Assert.Equal(OasPlatformCatalog.ClassicPortuguese.Id,
            viewModel.Workspace.SelectedSession?.PlatformId);

        await viewModel.StartGameAsync();
        Assert.Equal(3, authentication.Requests.Count);
        Assert.Equal(3, viewModel.Workspace.Sessions.Count);
    }

    [Fact]
    public async Task SuccessfulLaunchWithoutRememberAdoptsEphemeralProfileWithoutTerminatingIt()
    {
        var store = new InMemoryProfileStore();
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog([AppTestData.Server("100")])));
        var terminatedProcessIds = new List<int>();
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            store,
            new InMemoryCredentialVault(),
            SuccessfulAuthentication(),
            terminatedProcessIds.Add);
        await viewModel.InitializeAsync();
        PrepareNewAccount(viewModel, rememberPassword: false);

        await viewModel.StartGameAsync();

        GameSessionViewModel session = Assert.Single(viewModel.Workspace.Sessions);
        Assert.True(viewModel.IsWorkspaceVisible);
        Assert.NotEqual(Guid.Empty, session.ProfileId);
        Assert.Equal("Conta temporária", session.ProfileName);
        Assert.Equal("S100", session.ServerCode);
        Assert.Empty(store.Values);
        Assert.Empty(terminatedProcessIds);
    }

    [Fact]
    public async Task FailureBeforeWorkspaceAdoptionTerminatesPendingGameHost()
    {
        var store = new InMemoryProfileStore();
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog([AppTestData.Server("100")])));
        var terminatedProcessIds = new List<int>();
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            store,
            new InMemoryCredentialVault(),
            SuccessfulAuthentication(),
            terminatedProcessIds.Add);
        await viewModel.InitializeAsync();
        PrepareNewAccount(viewModel, rememberPassword: true);
        store.GetAllAsyncOverride = _ => throw new IOException("profile reload failed");

        await viewModel.StartGameAsync();

        Assert.Empty(viewModel.Workspace.Sessions);
        Assert.Equal([4242], terminatedProcessIds);
        Assert.Equal("Não foi possível iniciar", viewModel.CatalogStatus);
    }

    [Fact]
    public async Task CancellationBeforeWorkspaceAdoptionTerminatesPendingGameHost()
    {
        var store = new InMemoryProfileStore();
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog([AppTestData.Server("100")])));
        var terminatedProcessIds = new List<int>();
        MainWindowViewModel viewModel = CreateViewModel(
            directory,
            store,
            new InMemoryCredentialVault(),
            SuccessfulAuthentication(),
            terminatedProcessIds.Add);
        var reloadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await viewModel.InitializeAsync();
            PrepareNewAccount(viewModel, rememberPassword: true);
            store.GetAllAsyncOverride = async _ =>
            {
                reloadStarted.TrySetResult();
                await releaseReload.Task;
                throw new OperationCanceledException();
            };

            Task launch = viewModel.StartGameAsync();
            await reloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            viewModel.Dispose();
            releaseReload.TrySetResult();
            await launch;

            Assert.Empty(viewModel.Workspace.Sessions);
            Assert.Equal([4242], terminatedProcessIds);
        }
        finally
        {
            releaseReload.TrySetResult();
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task SelectionChangesDuringLaunchDoNotChangeAdoptedPlatformOrServer()
    {
        var authenticationCompletion = new TaskCompletionSource<AuthenticationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var authentication = new StubAuthenticationService((_, _) => authenticationCompletion.Task);
        var directory = new StubServerDirectory((platform, _, _) =>
            Task.FromResult(AppTestData.Catalog(
                platform.Id == OasPlatformCatalog.Brazil.Id
                    ? [AppTestData.Server("100")]
                    : [AppTestData.Server("115")])));
        var terminatedProcessIds = new List<int>();
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(),
            new InMemoryCredentialVault(),
            authentication,
            terminatedProcessIds.Add);
        await viewModel.InitializeAsync();
        PrepareNewAccount(viewModel, rememberPassword: false);
        PlatformDefinition launchedPlatform = viewModel.SelectedPlatform.Model;
        GameServer launchedServer = Assert.IsType<GameServer>(viewModel.SelectedServer?.Model);

        Task launch = viewModel.StartGameAsync();
        Assert.Single(authentication.Requests);
        viewModel.SelectedPlatform = viewModel.Platforms.Single(platform =>
            platform.Id == OasPlatformCatalog.Turkish.Id);
        Assert.Equal("115", viewModel.SelectedServer?.Id);
        authenticationCompletion.SetResult(AuthenticationResult.Success(
            new LaunchSession(new Uri("https://lobr.creaction-network.com/client/Loading.swf")),
            777));
        await launch;

        GameSessionViewModel session = Assert.Single(viewModel.Workspace.Sessions);
        Assert.Equal(launchedPlatform.DisplayName, session.PlatformName);
        Assert.Equal(launchedServer.Code, session.ServerCode);
        Assert.Empty(terminatedProcessIds);
    }

    private static MainWindowViewModel CreateViewModel(
        StubServerDirectory directory,
        InMemoryProfileStore profiles,
        InMemoryCredentialVault vault,
        StubAuthenticationService? authentication = null,
        Action<int>? terminateUnadoptedProcess = null)
    {
        var profileStorage = new ProfileStorageCoordinator(profiles, vault);
        authentication ??= new StubAuthenticationService((_, _) =>
            Task.FromResult(AuthenticationResult.Failure("unused")));
        var sessionLauncher = new SessionLaunchCoordinator(
            vault,
            authentication,
            new StubGameRuntime(),
            "C:\\legacy-runtime",
            profiles,
            terminateUnadoptedProcess: terminateUnadoptedProcess);
        return new MainWindowViewModel(
            directory,
            profileStorage,
            sessionLauncher,
            AppTestData.UsableRuntime(),
            OasPlatformCatalog.All,
            new FixedTimeProvider(AppTestData.Now),
            terminateUnadoptedProcess: terminateUnadoptedProcess);
    }

    private static StubAuthenticationService SuccessfulAuthentication() =>
        new((_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(AuthenticationResult.Success(
                new LaunchSession(new Uri("https://lobr.creaction-network.com/client/Loading.swf")),
                777));
        });

    private static ServerCatalog CatalogFor(
        PlatformDefinition platform,
        IReadOnlyList<GameServer> servers) =>
        new(platform.Id, servers, [], null, AppTestData.Now);

    private static GameServer ServerFor(PlatformDefinition platform, string id) =>
        AppTestData.Server(id) with
        {
            LaunchUri = new Uri(
                $"https://{platform.GameCode}.creaction-network.com/serverlist/s{id}"),
        };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static void PrepareNewAccount(
        MainWindowViewModel viewModel,
        bool rememberPassword)
    {
        viewModel.ProfileLabel = "Conta temporária";
        viewModel.LoginHint = "temporary@example.test";
        viewModel.PendingPassword = "typed-secret";
        viewModel.RememberPassword = rememberPassword;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
