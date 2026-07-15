using LegendLauncher.App.Services;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;
using LegendLauncher.Providers.SevenWan;

namespace LegendLauncher.Tests.App;

public sealed class SessionLaunchCoordinatorTests
{
    [Fact]
    public async Task LaunchAsync_UsesTypedPasswordLaunchesAndPersistsProfile()
    {
        const string password = "typed-secret";
        AccountProfile profile = AppTestData.Profile("player@example.test", 10, "1") with
        {
            RecentServerIds = ["200", "3257", "300", "200", "400", "500", "600"],
        };
        var store = new InMemoryProfileStore(profile);
        var vault = new InMemoryCredentialVault();
        var runtime = new StubGameRuntime();
        var authentication = SuccessfulAuthentication(providerUserId: 99);
        SessionLaunchCoordinator coordinator = CreateCoordinator(store, vault, authentication, runtime);
        var input = Input(profile, password, rememberPassword: true, serverId: "3257");

        SessionLaunchOutcome outcome = await coordinator.LaunchAsync(input);

        Assert.Equal(SessionLaunchState.Success, outcome.State);
        Assert.Equal(SessionCredentialSource.Typed, outcome.CredentialSource);
        Assert.True(outcome.WasProfilePersisted);
        Assert.True(outcome.WasCredentialPersisted);
        Assert.Single(runtime.Sessions);
        Assert.Single(authentication.Requests);
        Assert.Equal(password, authentication.Requests[0].Secret.Password);
        AccountProfile persisted = Assert.Single(store.Values);
        Assert.Equal(99, persisted.ProviderUserId);
        Assert.Equal("3257", persisted.LastServerId);
        Assert.Equal(
            ["3257", "200", "300", "400", "500"],
            persisted.RecentServerIds);
        Assert.True(vault.Contains(profile.CredentialKey));
        Assert.DoesNotContain(password, input.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(password, outcome.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaunchAsync_AuthenticationRejectionDoesNotStartRuntimeOrPersist()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", 10, "1");
        var store = new InMemoryProfileStore(profile);
        var vault = new InMemoryCredentialVault();
        var runtime = new StubGameRuntime();
        var authentication = new StubAuthenticationService((_, _) =>
            Task.FromResult(AuthenticationResult.Failure("invalid_credentials")));
        SessionLaunchCoordinator coordinator = CreateCoordinator(store, vault, authentication, runtime);

        SessionLaunchOutcome outcome = await coordinator.LaunchAsync(
            Input(profile, "wrong-password", rememberPassword: true));

        Assert.Equal(SessionLaunchState.AuthenticationRejected, outcome.State);
        Assert.Equal(SessionCredentialSource.Typed, outcome.CredentialSource);
        Assert.Empty(runtime.Sessions);
        Assert.Equal(10, store.Values.Single().ProviderUserId);
        Assert.False(vault.Contains(profile.CredentialKey));
    }

    [Fact]
    public async Task LaunchAsync_UsesSavedCredentialAndDeletesItWhenRememberIsOff()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", 10, "1");
        var store = new InMemoryProfileStore(profile);
        var vault = new InMemoryCredentialVault();
        vault.Seed(profile.CredentialKey, new CredentialSecret(profile.UserName, "vault-secret"));
        var runtime = new StubGameRuntime();
        var authentication = SuccessfulAuthentication(providerUserId: 10);
        SessionLaunchCoordinator coordinator = CreateCoordinator(store, vault, authentication, runtime);

        SessionLaunchOutcome outcome = await coordinator.LaunchAsync(
            Input(profile, typedPassword: string.Empty, rememberPassword: false));

        Assert.Equal(SessionLaunchState.Success, outcome.State);
        Assert.Equal(SessionCredentialSource.Stored, outcome.CredentialSource);
        Assert.Equal("vault-secret", authentication.Requests.Single().Secret.Password);
        Assert.False(vault.Contains(profile.CredentialKey));
        Assert.Equal(1, vault.DeleteCount);
    }

    [Fact]
    public async Task LaunchAsync_UsesSavedCredentialAcrossOasVariantsWithoutDuplicatingProfile()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", 10, "1") with
        {
            PlatformId = OasPlatformCatalog.RebornTurkish.Id,
        };
        var store = new InMemoryProfileStore(profile);
        var vault = new InMemoryCredentialVault();
        vault.Seed(profile.CredentialKey, new CredentialSecret(profile.UserName, "shared-oas-secret"));
        var runtime = new StubGameRuntime();
        var authentication = SuccessfulAuthentication(providerUserId: 99);
        SessionLaunchCoordinator coordinator = CreateCoordinator(store, vault, authentication, runtime);

        SessionLaunchOutcome outcome = await coordinator.LaunchAsync(
            Input(profile, typedPassword: string.Empty, rememberPassword: true));

        Assert.Equal(SessionLaunchState.Success, outcome.State);
        Assert.Equal(SessionCredentialSource.Stored, outcome.CredentialSource);
        Assert.Equal("shared-oas-secret", authentication.Requests.Single().Secret.Password);
        Assert.Single(runtime.Sessions);
        AccountProfile persisted = Assert.Single(store.Values);
        Assert.Equal(profile.Id, persisted.Id);
        Assert.Equal(profile.CredentialKey, persisted.CredentialKey);
        Assert.Equal(OasPlatformCatalog.Brazil.Id, persisted.PlatformId);
        Assert.Equal(99, persisted.ProviderUserId);
        Assert.Equal(99, persisted.GetProviderUserId(OasPlatformCatalog.Brazil.Id));
        Assert.Equal(10, persisted.GetProviderUserId(OasPlatformCatalog.RebornTurkish.Id));
        Assert.Equal(["3257"], persisted.GetRecentServerIds(OasPlatformCatalog.Brazil.Id));
        Assert.Equal(["1"], persisted.GetRecentServerIds(OasPlatformCatalog.RebornTurkish.Id));
        Assert.True(vault.Contains(profile.CredentialKey));
    }

    [Fact]
    public async Task LaunchAsync_DoesNotShareOasCredentialWithSevenWanPlatform()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", 10, "1") with
        {
            PlatformId = OasPlatformCatalog.RebornTurkish.Id,
        };
        var store = new InMemoryProfileStore(profile);
        var vault = new InMemoryCredentialVault();
        vault.Seed(profile.CredentialKey, new CredentialSecret(profile.UserName, "oas-only-secret"));
        var runtime = new StubGameRuntime();
        var authentication = SuccessfulAuthentication(providerUserId: 10);
        SessionLaunchCoordinator coordinator = CreateCoordinator(store, vault, authentication, runtime);

        SessionLaunchOutcome outcome = await coordinator.LaunchAsync(
            Input(
                profile,
                typedPassword: string.Empty,
                rememberPassword: true,
                platform: SevenWanPlatformCatalog.All[0]));

        Assert.Equal(SessionLaunchState.CredentialRequired, outcome.State);
        Assert.Equal(SessionCredentialSource.None, outcome.CredentialSource);
        Assert.Empty(authentication.Requests);
        Assert.Empty(runtime.Sessions);
        Assert.Equal(profile, Assert.Single(store.Values));
        Assert.True(vault.Contains(profile.CredentialKey));
    }

    [Fact]
    public async Task LaunchAsync_RememberWithoutExistingProfileCreatesAccountAfterSuccess()
    {
        var store = new InMemoryProfileStore();
        var vault = new InMemoryCredentialVault();
        var runtime = new StubGameRuntime();
        var authentication = SuccessfulAuthentication(providerUserId: 7123);
        SessionLaunchCoordinator coordinator = CreateCoordinator(store, vault, authentication, runtime);
        var input = Input(
            profile: null,
            typedPassword: "new-secret",
            rememberPassword: true,
            profileDisplayName: "Conta principal");

        SessionLaunchOutcome outcome = await coordinator.LaunchAsync(input);

        Assert.Equal(SessionLaunchState.Success, outcome.State);
        AccountProfile created = Assert.Single(store.Values);
        Assert.Equal("Conta principal", created.DisplayName);
        Assert.Equal(7123, created.ProviderUserId);
        Assert.Equal(["3257"], created.RecentServerIds);
        Assert.True(vault.Contains(created.CredentialKey));
        Assert.Equal(created, outcome.EffectiveProfile);
    }

    [Fact]
    public async Task LaunchAsync_WithoutRememberReturnsEphemeralEffectiveProfile()
    {
        var store = new InMemoryProfileStore();
        var vault = new InMemoryCredentialVault();
        var runtime = new StubGameRuntime();
        SessionLaunchCoordinator coordinator = CreateCoordinator(
            store,
            vault,
            SuccessfulAuthentication(providerUserId: 7123),
            runtime);

        SessionLaunchOutcome outcome = await coordinator.LaunchAsync(
            Input(
                profile: null,
                typedPassword: "transient-secret",
                rememberPassword: false,
                profileDisplayName: "Conta temporária"));

        AccountProfile effective = Assert.IsType<AccountProfile>(outcome.EffectiveProfile);
        Assert.Equal(SessionLaunchState.Success, outcome.State);
        Assert.False(outcome.WasProfilePersisted);
        Assert.True(outcome.WasCredentialPersisted);
        Assert.Empty(store.Values);
        Assert.Equal("Conta temporária", effective.DisplayName);
        Assert.Equal("new@example.test", effective.UserName);
        Assert.Equal(OasPlatformCatalog.Brazil.Id, effective.PlatformId);
        Assert.Equal(7123, effective.ProviderUserId);
        Assert.Equal("3257", effective.LastServerId);
        Assert.Equal(["3257"], effective.RecentServerIds);
    }

    [Fact]
    public async Task LaunchAsync_ReportsCredentialFailureWithoutLosingPersistedProfile()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", 10, "1");
        var store = new InMemoryProfileStore(profile);
        var vault = new InMemoryCredentialVault { ThrowOnSet = true };
        var runtime = new StubGameRuntime();
        SessionLaunchCoordinator coordinator = CreateCoordinator(
            store,
            vault,
            SuccessfulAuthentication(providerUserId: 33),
            runtime);

        SessionLaunchOutcome outcome = await coordinator.LaunchAsync(
            Input(profile, "secret", rememberPassword: true, serverId: "3257"));

        Assert.True(outcome.WasProfilePersisted);
        Assert.False(outcome.WasCredentialPersisted);
        Assert.NotNull(outcome.EffectiveProfile);
        Assert.Equal(33, store.Values.Single().ProviderUserId);
        Assert.Equal("3257", store.Values.Single().LastServerId);
    }

    [Fact]
    public async Task LaunchAsync_ProfilePersistenceFailureStillReturnsEffectiveProfile()
    {
        var store = new InMemoryProfileStore { ThrowOnSave = true };
        var vault = new InMemoryCredentialVault();
        var runtime = new StubGameRuntime();
        SessionLaunchCoordinator coordinator = CreateCoordinator(
            store,
            vault,
            SuccessfulAuthentication(providerUserId: 7123),
            runtime);

        SessionLaunchOutcome outcome = await coordinator.LaunchAsync(
            Input(
                profile: null,
                typedPassword: "new-secret",
                rememberPassword: true,
                profileDisplayName: "Conta não persistida"));

        AccountProfile effective = Assert.IsType<AccountProfile>(outcome.EffectiveProfile);
        Assert.Equal(SessionLaunchState.Success, outcome.State);
        Assert.False(outcome.WasProfilePersisted);
        Assert.False(outcome.WasCredentialPersisted);
        Assert.Empty(store.Values);
        Assert.Equal("Conta não persistida", effective.DisplayName);
        Assert.Equal(7123, effective.ProviderUserId);
    }

    [Fact]
    public async Task LaunchAsync_CancellationDuringPersistenceTerminatesStartedGameHost()
    {
        var store = new InMemoryProfileStore();
        var vault = new InMemoryCredentialVault();
        var runtime = new StubGameRuntime();
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.SaveAsyncOverride = async (_, cancellationToken) =>
        {
            saveStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        };
        var terminatedProcessIds = new List<int>();
        SessionLaunchCoordinator coordinator = CreateCoordinator(
            store,
            vault,
            SuccessfulAuthentication(providerUserId: 7123),
            runtime,
            terminatedProcessIds.Add);
        using var cancellation = new CancellationTokenSource();

        Task<SessionLaunchOutcome> launch = coordinator.LaunchAsync(
            Input(
                profile: null,
                typedPassword: "new-secret",
                rememberPassword: true),
            cancellation.Token);
        await saveStarted.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => launch);
        Assert.Equal([4242], terminatedProcessIds);
    }

    private static SessionLaunchCoordinator CreateCoordinator(
        InMemoryProfileStore store,
        InMemoryCredentialVault vault,
        StubAuthenticationService authentication,
        StubGameRuntime runtime,
        Action<int>? terminateUnadoptedProcess = null) =>
        new(
            vault,
            authentication,
            runtime,
            "C:\\legacy-runtime",
            store,
            new FixedTimeProvider(AppTestData.Now),
            terminateUnadoptedProcess);

    private static StubAuthenticationService SuccessfulAuthentication(long providerUserId) =>
        new((request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(AuthenticationResult.Success(
                new LaunchSession(new Uri(
                    $"https://lobr.creaction-network.com/client/Loading.swf?server={request.Server.Id}")),
                providerUserId));
        });

    private static SessionLaunchInput Input(
        AccountProfile? profile,
        string typedPassword,
        bool rememberPassword,
        string serverId = "3257",
        string profileDisplayName = "Conta",
        PlatformDefinition? platform = null) =>
        new(
            profile,
            platform ?? OasPlatformCatalog.Brazil,
            AppTestData.Server(serverId),
            profileDisplayName,
            profile?.UserName ?? "new@example.test",
            typedPassword,
            rememberPassword);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
