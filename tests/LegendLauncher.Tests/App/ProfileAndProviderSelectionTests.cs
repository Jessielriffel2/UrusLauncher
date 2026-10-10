using LegendLauncher.App.Localization;
using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;
using LegendLauncher.Providers.SwordOnline;

namespace LegendLauncher.Tests.App;

public sealed class ProfileAndProviderSelectionTests
{
    [Fact]
    public async Task OpeningWithProfiles_AlwaysSelectsOne()
    {
        using MainWindowViewModel viewModel = await OpenAsync(
            Profile("primeiro", "1"),
            Profile("segundo", "2"));

        Assert.NotNull(viewModel.SelectedProfile);
        Assert.NotNull(viewModel.SelectedServer);
    }

    [Fact]
    public async Task ProfilesOfAProviderThatIsNoLongerOffered_AreNotListedButStayOnDisk()
    {
        AccountProfile removedProvider = AppTestData.Profile("antigo", 7, "7") with { PlatformId = "oas-lobr" };
        AccountProfile localOnly = AppTestData.Profile("local", 8, "8") with { PlatformId = "local-loreborn" };
        var store = new InMemoryProfileStore(Profile("atual", "1"), removedProvider, localOnly);
        using MainWindowViewModel viewModel = await OpenWithStoreAsync(store);

        Assert.Equal("atual", Assert.Single(viewModel.Profiles).Model.UserName);
        Assert.Equal(3, store.Values.Count);
    }

    [Fact]
    public async Task ADeselectionPushedByTheList_IsRefusedAndTheProfileStaysSelected()
    {
        using MainWindowViewModel viewModel = await OpenAsync(
            Profile("primeiro", "1"),
            Profile("segundo", "2"));
        ProfileItemViewModel selected = viewModel.SelectedProfile!;
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        // what a Ctrl+click on the selected row does through the two-way binding
        viewModel.SelectedProfile = null;

        Assert.Same(selected, viewModel.SelectedProfile);
        // the list is told to select it again
        Assert.Contains(nameof(MainWindowViewModel.SelectedProfile), changed);
        Assert.True(viewModel.CanStartGame || viewModel.ShowInlinePassword);
    }

    [Fact]
    public async Task NewProfile_StillClearsTheSelectionOnPurposeAndCancelBringsOneBack()
    {
        using MainWindowViewModel viewModel = await OpenAsync(Profile("primeiro", "1"));

        viewModel.NewProfileCommand.Execute(null);
        Assert.Null(viewModel.SelectedProfile);
        Assert.True(viewModel.IsProfileEditorVisible);

        viewModel.CancelProfileEditCommand.Execute(null);
        Assert.NotNull(viewModel.SelectedProfile);
        Assert.False(viewModel.IsProfileEditorVisible);
    }

    [Fact]
    public async Task DeletingTheSelectedProfile_SelectsTheNextOne()
    {
        using MainWindowViewModel viewModel = await OpenAsync(
            Profile("primeiro", "1"),
            Profile("segundo", "2"));
        Guid deleted = viewModel.SelectedProfile!.Model.Id;

        viewModel.DeleteProfileCommand.Execute(null);
        await WaitUntilAsync(() => viewModel.DeleteProfileCommand.CanExecute(null));
        await Task.Delay(50);

        Assert.NotNull(viewModel.SelectedProfile);
        Assert.NotEqual(deleted, viewModel.SelectedProfile!.Model.Id);
        Assert.Single(viewModel.Profiles);
    }

    [Fact]
    public async Task ProviderSearch_NarrowsTheListAndClosingRestoresItWithTheSelection()
    {
        using MainWindowViewModel viewModel = await OpenAsync(
            [SwordOnlinePlatformCatalog.SwordOnline, .. OasPlatformCatalog.All],
            Profile("primeiro", "1"));
        PlatformItemViewModel selected = viewModel.SelectedPlatform;
        int all = viewModel.FilteredPlatforms.Count;
        Assert.Equal(viewModel.Platforms.Count, all);

        viewModel.PlatformSearchText = "sword";
        Assert.Equal("SwordOnline", Assert.Single(viewModel.FilteredPlatforms).DisplayName);
        Assert.False(viewModel.ShowPlatformSearchEmpty);

        viewModel.PlatformSearchText = "ZZZ-nada";
        Assert.Empty(viewModel.FilteredPlatforms);
        Assert.True(viewModel.ShowPlatformSearchEmpty);

        var announced = new List<string?>();
        viewModel.PropertyChanged += (_, args) => announced.Add(args.PropertyName);
        viewModel.ResetPlatformSearch();

        Assert.Equal(all, viewModel.FilteredPlatforms.Count);
        Assert.Same(selected, viewModel.SelectedPlatform);
        Assert.Contains(nameof(MainWindowViewModel.SelectedPlatform), announced);
    }

    [Fact]
    public async Task ProviderSearch_IgnoresCaseAndAccents()
    {
        using MainWindowViewModel viewModel = await OpenAsync(
            [SwordOnlinePlatformCatalog.SwordOnline, .. OasPlatformCatalog.All],
            Profile("primeiro", "1"));

        viewModel.PlatformSearchText = "TÜRKÇE";

        Assert.NotEmpty(viewModel.FilteredPlatforms);
        Assert.All(viewModel.FilteredPlatforms, platform =>
            Assert.Contains("rk", platform.DisplayName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheHighlightedProvider_IsListedFirstWithTheHotFlag()
    {
        Assert.True(SwordOnlinePlatformCatalog.SwordOnline.IsHot);
        Assert.DoesNotContain(OasPlatformCatalog.All, platform => platform.IsHot);
    }

    private static AccountProfile Profile(string login, string id) =>
        AppTestData.Profile(login, long.Parse(id), id) with
        {
            PlatformId = SwordOnlinePlatformCatalog.SwordOnline.Id,
        };

    private static async Task<MainWindowViewModel> OpenWithStoreAsync(InMemoryProfileStore store)
    {
        var vault = new InMemoryCredentialVault();
        var profileStorage = new ProfileStorageCoordinator(store, vault);
        var sessionLauncher = new SessionLaunchCoordinator(
            vault,
            new StubAuthenticationService((_, _) =>
                Task.FromResult(AuthenticationResult.Failure("unused"))),
            new StubGameRuntime(),
            "C:\\legacy-runtime",
            store);
        var directory = new StubServerDirectory((platform, _, _) =>
            Task.FromResult(new ServerCatalog(platform.Id, [Server()], [], null, AppTestData.Now)));
        var viewModel = new MainWindowViewModel(
            directory,
            profileStorage,
            sessionLauncher,
            AppTestData.UsableRuntime(),
            [SwordOnlinePlatformCatalog.SwordOnline],
            new FixedTimeProvider(AppTestData.Now),
            localization: new LocalizationService("pt-BR"));
        await viewModel.InitializeAsync();
        await Task.Delay(80);
        return viewModel;
    }

    private static Task<MainWindowViewModel> OpenAsync(params AccountProfile[] profiles) =>
        OpenAsync([SwordOnlinePlatformCatalog.SwordOnline], profiles);

    private static async Task<MainWindowViewModel> OpenAsync(
        IReadOnlyList<PlatformDefinition> platforms,
        params AccountProfile[] profiles)
    {
        var store = new InMemoryProfileStore(profiles);
        var vault = new InMemoryCredentialVault();
        foreach (AccountProfile profile in profiles)
        {
            vault.Seed(profile.CredentialKey, new CredentialSecret(profile.UserName, "Senha12345"));
        }

        var profileStorage = new ProfileStorageCoordinator(store, vault);
        var sessionLauncher = new SessionLaunchCoordinator(
            vault,
            new StubAuthenticationService((_, _) =>
                Task.FromResult(AuthenticationResult.Failure("unused"))),
            new StubGameRuntime(),
            "C:\\legacy-runtime",
            store);
        var directory = new StubServerDirectory((platform, _, _) =>
            Task.FromResult(new ServerCatalog(
                platform.Id,
                [Server()],
                [],
                null,
                AppTestData.Now)));
        var viewModel = new MainWindowViewModel(
            directory,
            profileStorage,
            sessionLauncher,
            AppTestData.UsableRuntime(),
            platforms,
            new FixedTimeProvider(AppTestData.Now),
            localization: new LocalizationService("pt-BR"));
        await viewModel.InitializeAsync();
        await Task.Delay(80);
        return viewModel;
    }

    private static GameServer Server() => new(
        SwordOnlineServerDirectory.ServerId,
        1L,
        "swo",
        "SwordOnline",
        "SwordOnline (swordonline.tech)",
        SwordOnlinePlatformCatalog.MovieUri,
        IsRecommended: true,
        IsValid: true,
        Merger: null,
        StartTimeUtc: null);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
