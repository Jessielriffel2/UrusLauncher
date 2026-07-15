using LegendLauncher.App.Localization;
using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;
using LegendLauncher.Tests.Infrastructure;

namespace LegendLauncher.Tests.App;

public sealed class MainWindowViewModelLocalizationTests
{
    [Fact]
    public async Task RuntimeSwitchRetranslatesStoredStatusComputedLabelsAndServerRows()
    {
        var localization = new LocalizationService("pt-BR");
        GameServer server = CreateFallbackServer();
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog([server])));
        using MainWindowViewModel viewModel = CreateViewModel(directory, localization);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) =>
            changedProperties.Add(eventArgs.PropertyName);

        await viewModel.InitializeAsync();
        Assert.Equal("Catálogo online", viewModel.CatalogStatus);
        Assert.Equal("Servidor 3257", viewModel.SelectedServerDisplayName);
        Assert.Equal("1 servidor", viewModel.ServerCountLabel);

        viewModel.SelectedLanguage = viewModel.Languages.Single(language =>
            language.Code == LocalizationService.EnglishLanguageCode);

        Assert.Equal("en-US", localization.LanguageCode);
        Assert.Equal("Online catalog", viewModel.CatalogStatus);
        Assert.StartsWith("Official catalog updated at", viewModel.StatusMessage);
        Assert.Equal("New profile", viewModel.SelectedProfileSummary);
        Assert.Equal("ENTER AND PLAY", viewModel.PrimaryActionLabel);
        Assert.Equal("Enter your password", viewModel.PasswordPlaceholderText);
        Assert.Equal("Server 3257", viewModel.SelectedServerDisplayName);
        Assert.Equal("1 server", viewModel.ServerCountLabel);
        Assert.Equal("Available for selection", viewModel.SelectedServer?.AvailabilityLabel);
        Assert.Contains(nameof(MainWindowViewModel.CatalogStatus), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.StatusMessage), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.SelectedServerDisplayName), changedProperties);
    }

    [Fact]
    public async Task InitializeRestoresPersistedLanguageBeforeCatalogResultIsPresented()
    {
        var settings = new LauncherSettingsService();
        await settings.SaveLanguageAsync("es-MX");
        var localization = new LocalizationService("pt-BR");
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog([AppTestData.Server("100")])));
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            localization,
            settings: settings);

        await viewModel.InitializeAsync();

        Assert.Equal("es-ES", localization.LanguageCode);
        Assert.Equal("es-ES", viewModel.SelectedLanguage.Code);
        Assert.Equal("Catálogo en línea", viewModel.CatalogStatus);
        Assert.StartsWith("Catálogo oficial actualizado", viewModel.StatusMessage);
        Assert.Equal("Listo para jugar", localization.Get("Session_ReadyTitle"));
    }

    [Fact]
    public async Task AuthenticationErrorUsesLocalizedCodeMappingAndNeverProviderText()
    {
        const string providerText = "RAW PROVIDER MESSAGE MUST NOT REACH THE UI";
        var localization = new LocalizationService("pt-BR");
        var authentication = new StubAuthenticationService((_, _) =>
            Task.FromResult(AuthenticationResult.Failure("network_error", providerText)));
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog([AppTestData.Server("100")])));
        using MainWindowViewModel viewModel = CreateViewModel(
            directory,
            localization,
            authentication);
        await viewModel.InitializeAsync();
        viewModel.ProfileLabel = "Conta";
        viewModel.LoginHint = "player@example.test";
        viewModel.PendingPassword = "typed-secret";

        await viewModel.StartGameAsync();

        Assert.Equal("Login não confirmado", viewModel.CatalogStatus);
        Assert.Equal(
            "Não foi possível alcançar a plataforma. Verifique sua conexão e tente novamente.",
            viewModel.StatusMessage);
        Assert.DoesNotContain(providerText, viewModel.StatusMessage, StringComparison.Ordinal);

        localization.SetLanguage("en-US");
        Assert.Equal("Login not confirmed", viewModel.CatalogStatus);
        Assert.Equal(
            "The platform could not be reached. Check your connection and try again.",
            viewModel.StatusMessage);
    }

    [Fact]
    public async Task LanguageSelectedInViewModelIsPersistedWithoutChangingOtherSettings()
    {
        using var directory = new TemporaryDirectory();
        string settingsPath = directory.Combine("settings.json");
        var settings = new LauncherSettingsService(settingsPath);
        await settings.SaveGamePreferencesAsync(false, GameLayoutMode.SplitTwo);
        var localization = new LocalizationService("pt-BR");
        var servers = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog([])));
        using MainWindowViewModel viewModel = CreateViewModel(
            servers,
            localization,
            settings: settings);
        await viewModel.InitializeAsync();

        viewModel.SelectedLanguage = viewModel.Languages.Single(language =>
            language.Code == LocalizationService.SpanishLanguageCode);

        LauncherSettingsSnapshot persisted = await WaitForLanguageAsync(
            settingsPath,
            LocalizationService.SpanishLanguageCode);
        Assert.False(persisted.IsGameMuted);
        Assert.Equal(GameLayoutMode.SplitTwo, persisted.LayoutMode);
    }

    private static MainWindowViewModel CreateViewModel(
        StubServerDirectory directory,
        LocalizationService localization,
        StubAuthenticationService? authentication = null,
        LauncherSettingsService? settings = null)
    {
        var profiles = new InMemoryProfileStore();
        var vault = new InMemoryCredentialVault();
        var profileStorage = new ProfileStorageCoordinator(profiles, vault);
        authentication ??= new StubAuthenticationService((_, _) =>
            Task.FromResult(AuthenticationResult.Failure("unused")));
        var launcher = new SessionLaunchCoordinator(
            vault,
            authentication,
            new StubGameRuntime(),
            "C:\\legacy-runtime",
            profiles);
        return new MainWindowViewModel(
            directory,
            profileStorage,
            launcher,
            AppTestData.UsableRuntime(),
            OasPlatformCatalog.All,
            new FixedTimeProvider(AppTestData.Now),
            settingsService: settings,
            localization: localization);
    }

    private static GameServer CreateFallbackServer() => new(
        "3257",
        3257,
        "S3257",
        "Server 3257",
        "Server 3257",
        new Uri("https://lobr.creaction-network.com/serverlist/s3257"),
        false,
        true,
        null,
        AppTestData.Now.AddDays(-1));

    private static async Task<LauncherSettingsSnapshot> WaitForLanguageAsync(
        string path,
        string expectedLanguage)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            LauncherSettingsSnapshot snapshot = await new LauncherSettingsService(path).LoadAsync();
            if (snapshot.LanguageCode == expectedLanguage)
            {
                return snapshot;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"Language '{expectedLanguage}' was not persisted in time.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
