using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;
using LegendLauncher.Tests.Infrastructure;

namespace LegendLauncher.Tests.App;

/// <summary>
/// Contract for the catalog outage fallback: when the remote server list cannot be loaded, a
/// profile that already used a server must keep a playable target instead of a dead button, and a
/// person without any saved target must still be able to type one. A healthy catalog must not show
/// any of it.
/// </summary>
public sealed class CatalogFallbackTests
{
    [Fact]
    public void FallbackServers_BuildAPlayableOasEntryFromASavedIdentifier()
    {
        IReadOnlyList<GameServer> servers = CatalogFallbackServers.FromSavedIds(
            OasPlatformCatalog.Brazil,
            ["100"]);

        GameServer server = Assert.Single(servers);
        Assert.Equal("100", server.Id);
        Assert.Equal(100, server.NumericId);
        Assert.Equal(new Uri("https://lobr.creaction-network.com/serverlist/s100"), server.LaunchUri);
        Assert.True(server.IsValid);
        Assert.True(server.IsAvailable(AppTestData.Now));
        Assert.Equal("Server 100", server.Name);
    }

    [Theory]
    [InlineData("lortr", "oas-lortr", "https://lortr.creaction-network.com/serverlist/s7")]
    [InlineData("lopl", "oas-lopl", "https://lopl.creaction-network.com/serverlist/s7")]
    public void FallbackServers_UsesTheVerifiedGameCodeHostPerPlatform(
        string gameCode,
        string platformId,
        string expectedAddress)
    {
        PlatformDefinition platform = OasPlatformCatalog.All.Single(item => item.Id == platformId);

        IReadOnlyList<GameServer> servers = CatalogFallbackServers.FromSavedIds(platform, ["7"]);

        Assert.Equal(new Uri(expectedAddress), Assert.Single(servers).LaunchUri);
        Assert.Equal(gameCode, platform.GameCode);
    }

    [Fact]
    public void FallbackServers_StaysEmptyForPlatformsWithoutAVerifiedAddressPattern()
    {
        IReadOnlyList<GameServer> servers = CatalogFallbackServers.FromSavedIds(
            new PlatformDefinition(
                "elarionis-lo",
                "Elarionis Online",
                "lo",
                new Uri("https://elarionis.online/api/shards", UriKind.Absolute),
                "pt-BR"),
            ["100"]);

        Assert.Empty(servers);
    }

    [Fact]
    public void FallbackServers_DropsBlanksDuplicatesAndUnusableIdentifiers()
    {
        IReadOnlyList<GameServer> servers = CatalogFallbackServers.FromSavedIds(
            OasPlatformCatalog.Brazil,
            ["", "   ", "100", "100", "abc", "s100", null, " 200 "]);

        Assert.Equal(["100", "200"], servers.Select(server => server.Id));
    }

    [Fact]
    public void FallbackServers_CapsTheOfferedSavedTargets()
    {
        IReadOnlyList<GameServer> servers = CatalogFallbackServers.FromSavedIds(
            OasPlatformCatalog.Brazil,
            ["1", "2", "3", "4", "5"]);

        Assert.Equal(CatalogFallbackServers.MaxSavedServers, servers.Count);
        Assert.Equal(["1", "2", "3"], servers.Select(server => server.Id));
    }

    [Fact]
    public void FallbackServers_ToleratesAMissingIdentifierList()
    {
        Assert.Empty(CatalogFallbackServers.FromSavedIds(OasPlatformCatalog.Brazil, null));
    }

    [Fact]
    public async Task CatalogFailureOffersTheSavedServerAsAPlayableTarget()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", null, "100");
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromException<ServerCatalog>(new InvalidOperationException("403 (Forbidden)")));
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(profile),
            new InMemoryCredentialVault());

        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsCatalogFallbackAvailable);
        Assert.Equal("100", Assert.Single(viewModel.VisibleServers).Id);
        Assert.Equal("100", viewModel.SelectedServer?.Id);
        Assert.True(viewModel.SelectedServer?.CanLaunch);
        Assert.Equal(
            "Catálogo indisponível: usando o servidor que esta conta já usou.",
            viewModel.StatusMessage);
    }

    [Fact]
    public async Task AHealthyCatalogKeepsEverySavedTargetOutOfTheList()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", null, "100");
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog([AppTestData.Server("200")])));
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(profile),
            new InMemoryCredentialVault());

        await viewModel.InitializeAsync();

        Assert.False(viewModel.IsCatalogFallbackAvailable);
        Assert.Equal(["200"], viewModel.VisibleServers.Select(server => server.Id));
    }

    [Fact]
    public async Task ARecoveredCatalogClearsTheNumberTypedDuringTheOutage()
    {
        var directory = new SwitchableServerDirectory(
            (_, _, _) => Task.FromException<ServerCatalog>(
                new InvalidOperationException("403 (Forbidden)")));
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(AppTestData.Profile("player@example.test", null, "100")),
            new InMemoryCredentialVault());
        await viewModel.InitializeAsync();
        viewModel.ManualServerId = "25";
        Assert.Equal("25", viewModel.ManualServerId);

        directory.Load = (_, _, _) =>
            Task.FromResult(AppTestData.Catalog([AppTestData.Server("200")]));
        viewModel.RefreshServersCommand.Execute(null);
        await Task.Yield();

        Assert.False(viewModel.IsCatalogFallbackAvailable);
        Assert.Equal(string.Empty, viewModel.ManualServerId);
    }

    private sealed class SwitchableServerDirectory(
        Func<PlatformDefinition, long, CancellationToken, Task<ServerCatalog>> load)
        : IServerDirectory
    {
        public Func<PlatformDefinition, long, CancellationToken, Task<ServerCatalog>> Load { get; set; } = load;

        public Task<ServerCatalog> GetServersAsync(
            PlatformDefinition platform,
            long userId = 0,
            CancellationToken cancellationToken = default) =>
            Load(platform, userId, cancellationToken);
    }

    [Fact]
    public async Task CatalogFailureWithoutAnySavedTargetStillOffersManualEntry()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", null, null) with
        {
            RecentServerIds = [],
            LastServerId = null,
        };
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromException<ServerCatalog>(new InvalidOperationException("403 (Forbidden)")));
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(profile),
            new InMemoryCredentialVault());

        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsCatalogFallbackAvailable);
        Assert.Empty(viewModel.VisibleServers);
        Assert.Null(viewModel.SelectedServer);
    }

    [Fact]
    public async Task TypedServerNumberBecomesThePlayableTarget()
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", null, null) with
        {
            RecentServerIds = [],
            LastServerId = null,
        };
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromException<ServerCatalog>(new InvalidOperationException("403 (Forbidden)")));
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(profile),
            new InMemoryCredentialVault());
        await viewModel.InitializeAsync();

        viewModel.ManualServerId = " 25 ";
        viewModel.UseManualServerCommand.Execute(null);

        Assert.Equal("25", Assert.Single(viewModel.VisibleServers).Id);
        Assert.Equal("25", viewModel.SelectedServer?.Id);
        Assert.True(viewModel.SelectedServer?.CanLaunch);
        Assert.Equal("S25", viewModel.SelectedServer?.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("25; rm -rf")]
    [InlineData("../../../etc/passwd")]
    public async Task AnUnusableTypedNumberLeavesTheListUntouched(string typed)
    {
        AccountProfile profile = AppTestData.Profile("player@example.test", null, null) with
        {
            RecentServerIds = [],
            LastServerId = null,
        };
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromException<ServerCatalog>(new InvalidOperationException("403 (Forbidden)")));
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            new InMemoryProfileStore(profile),
            new InMemoryCredentialVault());
        await viewModel.InitializeAsync();

        viewModel.ManualServerId = typed;
        viewModel.UseManualServerCommand.Execute(null);

        Assert.Empty(viewModel.VisibleServers);
        Assert.Null(viewModel.SelectedServer);
        Assert.Equal(typed, viewModel.ManualServerId);
    }

    private static MainWindowViewModel CreateViewModel(
        IServerDirectory directory,
        InMemoryProfileStore profiles,
        InMemoryCredentialVault vault)
    {
        var profileStorage = new ProfileStorageCoordinator(profiles, vault);
        var sessionLauncher = new SessionLaunchCoordinator(
            vault,
            new StubAuthenticationService((_, _) =>
                Task.FromResult(AuthenticationResult.Failure("unused"))),
            new StubGameRuntime(),
            "C:\\legacy-runtime",
            profiles);
        return new MainWindowViewModel(
            directory,
            profileStorage,
            sessionLauncher,
            AppTestData.UsableRuntime(),
            OasPlatformCatalog.All,
            new FixedTimeProvider(AppTestData.Now));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
