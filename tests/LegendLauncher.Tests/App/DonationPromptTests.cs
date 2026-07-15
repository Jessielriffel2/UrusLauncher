using LegendLauncher.App.Localization;
using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.App;

public sealed class DonationPromptTests
{
    [Fact]
    public async Task FirstOpeningShowsPromptAndPersistsDisplayTime()
    {
        var settings = new LauncherSettingsService();
        using MainWindowViewModel viewModel = CreateViewModel(settings);

        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsDonationPromptVisible);
        Assert.Equal(AppTestData.Now, (await settings.LoadAsync()).LastDonationPromptUtc);
    }

    [Fact]
    public async Task OpeningBeforeFiveHoursDoesNotShowPromptOrChangeTimestamp()
    {
        var settings = new LauncherSettingsService();
        DateTimeOffset previous = AppTestData.Now.AddHours(-5).AddTicks(1);
        await settings.SaveDonationPromptShownAsync(previous);
        using MainWindowViewModel viewModel = CreateViewModel(settings);

        await viewModel.InitializeAsync();

        Assert.False(viewModel.IsDonationPromptVisible);
        Assert.Equal(previous, (await settings.LoadAsync()).LastDonationPromptUtc);
    }

    [Fact]
    public async Task OpeningAtExactlyFiveHoursShowsPrompt()
    {
        var settings = new LauncherSettingsService();
        await settings.SaveDonationPromptShownAsync(AppTestData.Now.AddHours(-5));
        using MainWindowViewModel viewModel = CreateViewModel(settings);

        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsDonationPromptVisible);
        Assert.Equal(AppTestData.Now, (await settings.LoadAsync()).LastDonationPromptUtc);
    }

    [Fact]
    public async Task OpeningAfterTwelveHoursShowsPrompt()
    {
        var settings = new LauncherSettingsService();
        await settings.SaveDonationPromptShownAsync(AppTestData.Now.AddHours(-12));
        using MainWindowViewModel viewModel = CreateViewModel(settings);

        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsDonationPromptVisible);
        Assert.Equal(AppTestData.Now, (await settings.LoadAsync()).LastDonationPromptUtc);
    }

    [Fact]
    public async Task FutureTimestampShowsPromptAndIsRepairedToCurrentTime()
    {
        var settings = new LauncherSettingsService();
        await settings.SaveDonationPromptShownAsync(AppTestData.Now.AddDays(1));
        using MainWindowViewModel viewModel = CreateViewModel(settings);

        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsDonationPromptVisible);
        Assert.Equal(AppTestData.Now, (await settings.LoadAsync()).LastDonationPromptUtc);
    }

    [Fact]
    public async Task ManualCommandAlwaysOpensAndRegistersDisplayWithinInterval()
    {
        var settings = new LauncherSettingsService();
        await settings.SaveDonationPromptShownAsync(AppTestData.Now.AddHours(-1));
        using MainWindowViewModel viewModel = CreateViewModel(settings);
        await viewModel.InitializeAsync();
        Assert.False(viewModel.IsDonationPromptVisible);

        viewModel.OpenDonationPromptCommand.Execute(null);
        await WaitForTimestampAsync(settings, AppTestData.Now);

        Assert.True(viewModel.IsDonationPromptVisible);
    }

    [Fact]
    public async Task CloseCommandHidesPromptWithoutChangingDisplayTime()
    {
        var settings = new LauncherSettingsService();
        using MainWindowViewModel viewModel = CreateViewModel(settings);
        await viewModel.InitializeAsync();
        LauncherSettingsSnapshot beforeClose = await settings.LoadAsync();

        viewModel.CloseDonationPromptCommand.Execute(null);

        Assert.False(viewModel.IsDonationPromptVisible);
        Assert.Equal(
            beforeClose.LastDonationPromptUtc,
            (await settings.LoadAsync()).LastDonationPromptUtc);
    }

    [Fact]
    public async Task DonationIntervalIsEvaluatedOnlyOncePerLauncherOpening()
    {
        var settings = new LauncherSettingsService();
        var time = new MutableTimeProvider(AppTestData.Now);
        await settings.SaveDonationPromptShownAsync(AppTestData.Now.AddHours(-1));
        using MainWindowViewModel viewModel = CreateViewModel(settings, time);
        await viewModel.InitializeAsync();
        Assert.False(viewModel.IsDonationPromptVisible);

        time.UtcNow = AppTestData.Now.AddHours(6);
        await viewModel.InitializeAsync();

        Assert.False(viewModel.IsDonationPromptVisible);
    }

    private static MainWindowViewModel CreateViewModel(
        LauncherSettingsService settings,
        TimeProvider? timeProvider = null)
    {
        var profiles = new InMemoryProfileStore();
        var vault = new InMemoryCredentialVault();
        var profileStorage = new ProfileStorageCoordinator(profiles, vault);
        var authentication = new StubAuthenticationService((_, _) =>
            Task.FromResult(AuthenticationResult.Failure("unused")));
        var sessionLauncher = new SessionLaunchCoordinator(
            vault,
            authentication,
            new StubGameRuntime(),
            "C:\\legacy-runtime",
            profiles);
        var directory = new StubServerDirectory((_, _, _) =>
            Task.FromResult(AppTestData.Catalog([])));
        return new MainWindowViewModel(
            directory,
            profileStorage,
            sessionLauncher,
            AppTestData.UsableRuntime(),
            OasPlatformCatalog.All,
            timeProvider ?? new MutableTimeProvider(AppTestData.Now),
            settingsService: settings,
            localization: new LocalizationService("pt-BR"));
    }

    private static async Task WaitForTimestampAsync(
        LauncherSettingsService settings,
        DateTimeOffset expected)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if ((await settings.LoadAsync()).LastDonationPromptUtc == expected)
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Donation prompt display time was not persisted in time.");
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
