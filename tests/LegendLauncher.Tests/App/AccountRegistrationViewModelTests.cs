using LegendLauncher.App.Localization;
using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.App;

public sealed class AccountRegistrationViewModelTests
{
    private static readonly PlatformDefinition RegisterablePlatform = new(
        "registerable-test",
        "Registerable Test",
        "reg",
        new Uri("https://example.test/config.xml", UriKind.Absolute),
        "pt-BR",
        SupportsAccountCreation: true);

    [Fact]
    public async Task CanCreateAccount_IsOfferedOnlyForANewProfileOnAPlatformThatAllowsIt()
    {
        var registration = new StubRegistrationService();
        using MainWindowViewModel viewModel = CreateViewModel(
            new InMemoryProfileStore(),
            new InMemoryCredentialVault(),
            registration,
            [RegisterablePlatform, .. OasPlatformCatalog.All]);

        await viewModel.InitializeAsync();
        await Task.Yield();

        // no profile yet: the editor opens by itself on the first platform
        Assert.True(viewModel.IsProfileEditorVisible);
        Assert.True(viewModel.CanCreateAccount);
        Assert.True(viewModel.OpenRegistrationCommand.CanExecute(null));
        Assert.Contains("Registerable Test", viewModel.CreateAccountLinkText);

        viewModel.SelectedPlatform = viewModel.Platforms.First(platform => platform.Id != RegisterablePlatform.Id);
        Assert.False(viewModel.CanCreateAccount);
        Assert.False(viewModel.OpenRegistrationCommand.CanExecute(null));
    }

    [Fact]
    public async Task CanCreateAccount_IsNeverOfferedWithoutAServiceOrWhileEditingAnExistingProfile()
    {
        AccountProfile existing = AppTestData.Profile("heroi", 1, "1") with
        {
            PlatformId = RegisterablePlatform.Id,
        };

        using MainWindowViewModel withoutService = CreateViewModel(
            new InMemoryProfileStore(),
            new InMemoryCredentialVault(),
            registration: null,
            [RegisterablePlatform]);
        await withoutService.InitializeAsync();
        Assert.False(withoutService.CanCreateAccount);

        using MainWindowViewModel editing = CreateViewModel(
            new InMemoryProfileStore(existing),
            new InMemoryCredentialVault(),
            new StubRegistrationService(),
            [RegisterablePlatform]);
        await editing.InitializeAsync();
        await Task.Yield();
        editing.EditProfileCommand.Execute(null);

        Assert.True(editing.IsProfileEditorVisible);
        Assert.False(editing.CanCreateAccount);
    }

    [Fact]
    public async Task Submit_CreatesTheAccountSavesTheProfileAndKeepsThePasswordInTheVault()
    {
        var registration = new StubRegistrationService();
        var profiles = new InMemoryProfileStore();
        var vault = new InMemoryCredentialVault();
        using MainWindowViewModel viewModel = CreateViewModel(profiles, vault, registration, [RegisterablePlatform]);
        await viewModel.InitializeAsync();
        await Task.Yield();

        viewModel.OpenRegistrationCommand.Execute(null);
        Assert.True(viewModel.IsRegistrationVisible);
        viewModel.RegistrationLogin = "Heroi_01";
        viewModel.RegistrationPassword = "Senha12345";
        viewModel.RegistrationPasswordConfirm = "Senha12345";
        viewModel.RegistrationEmail = "heroi@exemplo.com";

        await SubmitAsync(viewModel);

        AccountRegistrationRequest sent = Assert.Single(registration.Requests);
        Assert.Equal("Heroi_01", sent.Login);
        Assert.Equal("Senha12345", sent.Password);
        Assert.Equal("heroi@exemplo.com", sent.Email);
        Assert.Equal(RegisterablePlatform.Id, sent.Platform.Id);

        Assert.False(viewModel.IsRegistrationVisible);
        Assert.False(viewModel.IsProfileEditorVisible);
        Assert.False(viewModel.IsRegistering);
        AccountProfile saved = Assert.Single(profiles.Values);
        Assert.Equal("Heroi_01", saved.UserName);
        Assert.Equal(RegisterablePlatform.Id, saved.PlatformId);
        Assert.True(vault.Contains(saved.CredentialKey));
        Assert.Equal("Heroi_01", viewModel.SelectedProfile!.Model.UserName);
        Assert.Equal(string.Empty, viewModel.RegistrationPassword);
        Assert.Equal(string.Empty, viewModel.RegistrationPasswordConfirm);
        Assert.Equal(string.Empty, viewModel.PendingPassword);
        Assert.Contains("Heroi_01", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Submit_UsesTheCanonicalLoginTheServiceReturns()
    {
        var registration = new StubRegistrationService
        {
            Result = AccountRegistrationResult.Success("HEROI_01"),
        };
        var profiles = new InMemoryProfileStore();
        using MainWindowViewModel viewModel = CreateViewModel(
            profiles,
            new InMemoryCredentialVault(),
            registration,
            [RegisterablePlatform]);
        await viewModel.InitializeAsync();
        await Task.Yield();
        viewModel.OpenRegistrationCommand.Execute(null);
        viewModel.RegistrationLogin = "heroi_01";
        viewModel.RegistrationPassword = "Senha12345";
        viewModel.RegistrationPasswordConfirm = "Senha12345";

        await SubmitAsync(viewModel);

        Assert.Equal("HEROI_01", Assert.Single(profiles.Values).UserName);
    }

    [Fact]
    public async Task Submit_WithDifferentPasswords_ShowsAMessageAndDoesNotCallTheServer()
    {
        var registration = new StubRegistrationService();
        using MainWindowViewModel viewModel = CreateViewModel(
            new InMemoryProfileStore(),
            new InMemoryCredentialVault(),
            registration,
            [RegisterablePlatform]);
        await viewModel.InitializeAsync();
        await Task.Yield();
        viewModel.OpenRegistrationCommand.Execute(null);
        viewModel.RegistrationLogin = "Heroi_01";
        viewModel.RegistrationPassword = "Senha12345";
        viewModel.RegistrationPasswordConfirm = "Senha12346";

        await SubmitAsync(viewModel);

        Assert.Empty(registration.Requests);
        Assert.True(viewModel.HasRegistrationMessage);
        Assert.True(viewModel.IsRegistrationVisible);

        // typing again clears the stale message
        viewModel.RegistrationPasswordConfirm = "Senha12345";
        Assert.False(viewModel.HasRegistrationMessage);
    }

    [Fact]
    public async Task Submit_WithMissingFields_DoesNotCallTheServer()
    {
        var registration = new StubRegistrationService();
        using MainWindowViewModel viewModel = CreateViewModel(
            new InMemoryProfileStore(),
            new InMemoryCredentialVault(),
            registration,
            [RegisterablePlatform]);
        await viewModel.InitializeAsync();
        await Task.Yield();
        viewModel.OpenRegistrationCommand.Execute(null);

        await SubmitAsync(viewModel);

        Assert.Empty(registration.Requests);
        Assert.True(viewModel.HasRegistrationMessage);
    }

    [Theory]
    [InlineData("registration_login_taken")]
    [InlineData("registration_too_many_requests")]
    [InlineData("registration_invalid_login")]
    [InlineData("registration_invalid_password")]
    [InlineData("registration_invalid_email")]
    [InlineData("network_error")]
    [InlineData("request_timeout")]
    [InlineData("anything_else")]
    public async Task Submit_WhenTheServiceFails_KeepsThePanelOpenSavesNothingAndExplains(string errorCode)
    {
        var registration = new StubRegistrationService
        {
            Result = AccountRegistrationResult.Failure(errorCode),
        };
        var profiles = new InMemoryProfileStore();
        var vault = new InMemoryCredentialVault();
        using MainWindowViewModel viewModel = CreateViewModel(profiles, vault, registration, [RegisterablePlatform]);
        await viewModel.InitializeAsync();
        await Task.Yield();
        viewModel.OpenRegistrationCommand.Execute(null);
        viewModel.RegistrationLogin = "Heroi_01";
        viewModel.RegistrationPassword = "Senha12345";
        viewModel.RegistrationPasswordConfirm = "Senha12345";

        await SubmitAsync(viewModel);

        Assert.True(viewModel.IsRegistrationVisible);
        Assert.True(viewModel.HasRegistrationMessage);
        Assert.False(viewModel.IsRegistering);
        Assert.Empty(profiles.Values);
        Assert.Equal(0, vault.SetCount);
        // typed values stay so the player only fixes what is wrong
        Assert.Equal("Heroi_01", viewModel.RegistrationLogin);
        Assert.Equal("Senha12345", viewModel.RegistrationPassword);
    }

    [Fact]
    public async Task Submit_WhenTheSiteRefuses_ShowsTheSiteExplanation()
    {
        var registration = new StubRegistrationService
        {
            Result = AccountRegistrationResult.Failure("registration_rejected", "Esse login é reservado."),
        };
        using MainWindowViewModel viewModel = CreateViewModel(
            new InMemoryProfileStore(),
            new InMemoryCredentialVault(),
            registration,
            [RegisterablePlatform]);
        await viewModel.InitializeAsync();
        await Task.Yield();
        viewModel.OpenRegistrationCommand.Execute(null);
        viewModel.RegistrationLogin = "admin";
        viewModel.RegistrationPassword = "Senha12345";
        viewModel.RegistrationPasswordConfirm = "Senha12345";

        await SubmitAsync(viewModel);

        Assert.Equal("Esse login é reservado.", viewModel.RegistrationMessage);
    }

    [Fact]
    public async Task Submit_WhenAnUnexpectedExceptionEscapes_ShowsAGenericMessageAndRecovers()
    {
        var registration = new StubRegistrationService { Throw = new InvalidOperationException("boom") };
        using MainWindowViewModel viewModel = CreateViewModel(
            new InMemoryProfileStore(),
            new InMemoryCredentialVault(),
            registration,
            [RegisterablePlatform]);
        await viewModel.InitializeAsync();
        await Task.Yield();
        viewModel.OpenRegistrationCommand.Execute(null);
        viewModel.RegistrationLogin = "Heroi_01";
        viewModel.RegistrationPassword = "Senha12345";
        viewModel.RegistrationPasswordConfirm = "Senha12345";

        await SubmitAsync(viewModel);

        Assert.True(viewModel.HasRegistrationMessage);
        Assert.False(viewModel.IsRegistering);
        Assert.True(viewModel.SubmitRegistrationCommand.CanExecute(null));
    }

    [Fact]
    public async Task CancelRegistration_ClosesThePanelAndWipesTheTypedSecrets()
    {
        using MainWindowViewModel viewModel = CreateViewModel(
            new InMemoryProfileStore(),
            new InMemoryCredentialVault(),
            new StubRegistrationService(),
            [RegisterablePlatform]);
        await viewModel.InitializeAsync();
        await Task.Yield();
        viewModel.OpenRegistrationCommand.Execute(null);
        viewModel.RegistrationLogin = "Heroi_01";
        viewModel.RegistrationPassword = "Senha12345";
        viewModel.RegistrationPasswordConfirm = "Senha12345";

        viewModel.CancelRegistrationCommand.Execute(null);

        Assert.False(viewModel.IsRegistrationVisible);
        Assert.Equal(string.Empty, viewModel.RegistrationLogin);
        Assert.Equal(string.Empty, viewModel.RegistrationPassword);
        Assert.Equal(string.Empty, viewModel.RegistrationPasswordConfirm);
        Assert.True(viewModel.IsProfileEditorVisible);
    }

    private static async Task SubmitAsync(MainWindowViewModel viewModel)
    {
        viewModel.SubmitRegistrationCommand.Execute(null);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (viewModel.IsRegistering)
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static MainWindowViewModel CreateViewModel(
        InMemoryProfileStore profiles,
        InMemoryCredentialVault vault,
        IAccountRegistrationService? registration,
        IReadOnlyList<PlatformDefinition> platforms)
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
            new StubServerDirectory((_, _, _) =>
                Task.FromResult(AppTestData.Catalog([AppTestData.Server("100")]))),
            profileStorage,
            sessionLauncher,
            AppTestData.UsableRuntime(),
            platforms,
            new FixedTimeProvider(AppTestData.Now),
            localization: new LocalizationService("pt-BR"),
            accountRegistration: registration);
    }

    private sealed class StubRegistrationService : IAccountRegistrationService
    {
        public List<AccountRegistrationRequest> Requests { get; } = [];

        public AccountRegistrationResult? Result { get; init; }

        public Exception? Throw { get; init; }

        public Task<AccountRegistrationResult> RegisterAsync(
            AccountRegistrationRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (Throw is not null)
            {
                throw Throw;
            }

            return Task.FromResult(Result ?? AccountRegistrationResult.Success(request.Login));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
