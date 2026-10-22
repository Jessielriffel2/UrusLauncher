using LegendLauncher.App.Localization;
using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Tests.App;

public sealed class GameCacheTests
{
    [Theory]
    [InlineData("https://swordonline.tech/ui/zh_cn/swf/email.swf?rnd=4e526cbc", true)]
    [InlineData("https://www.swordonline.tech/Loading.swf", true)]
    [InlineData("https://SWORDONLINE.TECH/11.png", true)]
    [InlineData("https://evil-swordonline.tech/11.png", false)]
    [InlineData("https://swordonline.tech.example.com/11.png", false)]
    [InlineData("https://lo.oasgames.com/Loading.swf", false)]
    public void OnlySwordOnlineAddressesAreGameCache(string url, bool expected)
    {
        Assert.Equal(expected, MainWindowViewModel.IsGameCacheUrl(new Uri(url, UriKind.Absolute)));
    }

#if !LOREBORN_LOCAL
    [Fact]
    public void PublicBuildNeverTouchesLocalhostEntries()
    {
        Assert.False(MainWindowViewModel.IsGameCacheUrl(new Uri("http://127.0.0.1:8081/2.png", UriKind.Absolute)));
    }
#endif

    [Fact]
    public async Task FirstClickArmsAndSecondClickClearsOnce()
    {
        using MainWindowViewModel viewModel = CreateViewModel();
        int calls = 0;
        var cleared = new TaskCompletionSource<Func<Uri, bool>>(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.GameCacheClearer = isGameUrl =>
        {
            Interlocked.Increment(ref calls);
            cleared.TrySetResult(isGameUrl);
            return new GameCacheCleanResult(2133, 0, 182_452_224);
        };

        Assert.Equal("Limpar cache do jogo", viewModel.ClearGameCacheButtonText);
        viewModel.ClearGameCacheCommand.Execute(null);
        Assert.Equal("Clique de novo para limpar", viewModel.ClearGameCacheButtonText);
        Assert.Equal(0, Volatile.Read(ref calls));

        viewModel.ClearGameCacheCommand.Execute(null);
        Func<Uri, bool> predicate = await cleared.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilAsync(() => viewModel.ClearGameCacheButtonText == "Limpar cache do jogo");

        Assert.Equal(1, Volatile.Read(ref calls));
        Assert.True(predicate(new Uri("https://swordonline.tech/11.png", UriKind.Absolute)));
        Assert.Contains("2133", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.True(viewModel.ClearGameCacheCommand.CanExecute(null));
    }

    [Fact]
    public void EveryLanguageHasTheGameCacheTexts()
    {
        foreach (string language in new[] { "pt-BR", "en-US", "es-ES", "tr-TR" })
        {
            IReadOnlyDictionary<string, string> catalog = LocalizationService.GetCatalog(language);
            foreach (string key in new[]
            {
                "GameCache_Button", "GameCache_Confirm", "GameCache_Clearing", "GameCache_Tooltip",
                "GameCache_CloseGamesFirst", "GameCache_Cleared", "GameCache_ClearedPartial", "GameCache_Failed",
            })
            {
                Assert.False(string.IsNullOrWhiteSpace(catalog[key]), language + " " + key);
            }
        }
    }

    [Fact]
    public void MainWindowShowsTheClearGameCacheLink()
    {
        string main = File.ReadAllText(FindRepositoryFile("src", "LegendLauncher.App", "MainWindow.xaml"));
        Assert.Contains("Command=\"{Binding ClearGameCacheCommand}\"", main, StringComparison.Ordinal);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime limit = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < limit, "timed out");
            await Task.Delay(20);
        }
    }

    private static MainWindowViewModel CreateViewModel()
    {
        var profiles = new InMemoryProfileStore();
        var vault = new InMemoryCredentialVault();
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
            [new PlatformDefinition(
                "cache-test",
                "Cache Test",
                "cache",
                new Uri("https://example.test/config.xml", UriKind.Absolute),
                "pt-BR")],
            TimeProvider.System,
            localization: new LocalizationService("pt-BR"));
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
}
