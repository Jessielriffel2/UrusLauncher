using LegendLauncher.App.Localization;
using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.SwordOnline;

namespace LegendLauncher.Tests.App;

/// <summary>
/// The player types a login and a password, saves the profile and expects to be able to play:
/// no extra trip to "Editar conta".
/// </summary>
public sealed class PlayButtonReadinessTests
{
    private static readonly PlatformDefinition Platform = SwordOnlinePlatformCatalog.SwordOnline;

    [Fact]
    public async Task NewProfile_WithLoginPasswordAndSaveToVault_EnablesPlayRightAfterSaving()
    {
        var vault = new InMemoryCredentialVault();
        using MainWindowViewModel viewModel = await OpenAsync(new InMemoryProfileStore(), vault);
        Assert.True(viewModel.IsProfileEditorVisible);

        viewModel.ProfileLabel = "Meu perfil";
        viewModel.LoginHint = "Heroi_01";
        viewModel.PendingPassword = "Senha12345";
        viewModel.RememberPassword = true;
        await SaveAsync(viewModel);

        Assert.False(viewModel.IsProfileEditorVisible);
        Assert.Equal(1, vault.SetCount);
        Assert.NotNull(viewModel.SelectedServer);
        Assert.True(viewModel.HasSavedCredential);
        Assert.True(viewModel.CanStartGame);
        Assert.False(viewModel.ShowInlinePassword);
    }

    [Fact]
    public async Task NewProfile_StartsWithSaveToVaultAlreadyTicked()
    {
        using MainWindowViewModel viewModel = await OpenAsync(new InMemoryProfileStore(), new InMemoryCredentialVault());

        Assert.True(viewModel.IsProfileEditorVisible);
        Assert.True(viewModel.RememberPassword);
    }

    [Fact]
    public async Task NewProfile_WithPasswordButWithoutSavingToVault_StillAllowsPlayingThisSession()
    {
        var vault = new InMemoryCredentialVault();
        using MainWindowViewModel viewModel = await OpenAsync(new InMemoryProfileStore(), vault);

        viewModel.ProfileLabel = "Meu perfil";
        viewModel.LoginHint = "Heroi_01";
        viewModel.PendingPassword = "Senha12345";
        viewModel.RememberPassword = false;
        await SaveAsync(viewModel);

        Assert.Equal(0, vault.SetCount);
        Assert.Equal("Senha12345", viewModel.PendingPassword);
        Assert.True(viewModel.CanStartGame);
    }

    [Fact]
    public async Task NewProfile_WhenTheVaultFails_StillAllowsPlayingWithTheTypedPassword()
    {
        var vault = new InMemoryCredentialVault { ThrowOnSet = true };
        using MainWindowViewModel viewModel = await OpenAsync(new InMemoryProfileStore(), vault);

        viewModel.ProfileLabel = "Meu perfil";
        viewModel.LoginHint = "Heroi_01";
        viewModel.PendingPassword = "Senha12345";
        viewModel.RememberPassword = true;
        await SaveAsync(viewModel);

        Assert.False(viewModel.HasSavedCredential);
        Assert.Equal("Senha12345", viewModel.PendingPassword);
        Assert.True(viewModel.CanStartGame);
    }

    [Fact]
    public async Task ExistingProfileWithoutSavedPassword_IsReadyAsSoonAsThePasswordIsTypedOnTheCard()
    {
        AccountProfile profile = AppTestData.Profile("heroi", 1, "1") with { PlatformId = Platform.Id };
        using MainWindowViewModel viewModel = await OpenAsync(new InMemoryProfileStore(profile), new InMemoryCredentialVault());

        Assert.False(viewModel.IsProfileEditorVisible);
        Assert.True(viewModel.ShowInlinePassword);
        Assert.False(viewModel.CanStartGame);

        viewModel.PendingPassword = "Senha12345";

        Assert.True(viewModel.CanStartGame);
    }

    [Fact]
    public async Task EditingAnExistingProfile_AndSavingWithAPassword_EnablesPlay()
    {
        AccountProfile profile = AppTestData.Profile("heroi", 1, "1") with { PlatformId = Platform.Id };
        var vault = new InMemoryCredentialVault();
        using MainWindowViewModel viewModel = await OpenAsync(new InMemoryProfileStore(profile), vault);

        viewModel.EditProfileCommand.Execute(null);
        viewModel.PendingPassword = "Senha12345";
        viewModel.RememberPassword = true;
        await SaveAsync(viewModel);

        Assert.False(viewModel.IsProfileEditorVisible);
        Assert.True(viewModel.CanStartGame);
    }

    private static async Task SaveAsync(MainWindowViewModel viewModel)
    {
        viewModel.SaveProfileCommand.Execute(null);
        // the command reports itself as not executable until the whole save (profile, catalog) ends
        await WaitUntilAsync(() => viewModel.SaveProfileCommand.CanExecute(null));
        // the vault lookup that follows a save runs in the background
        await Task.Delay(100);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static async Task<MainWindowViewModel> OpenAsync(
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
        var directory = new StubServerDirectory((platform, _, _) =>
            Task.FromResult(new ServerCatalog(
                platform.Id,
                [SwordServer()],
                [],
                null,
                AppTestData.Now)));
        var viewModel = new MainWindowViewModel(
            directory,
            profileStorage,
            sessionLauncher,
            AppTestData.UsableRuntime(),
            [Platform],
            new FixedTimeProvider(AppTestData.Now),
            localization: new LocalizationService("pt-BR"));
        await viewModel.InitializeAsync();
        await Task.Delay(50);
        return viewModel;
    }

    private static GameServer SwordServer() => new(
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
