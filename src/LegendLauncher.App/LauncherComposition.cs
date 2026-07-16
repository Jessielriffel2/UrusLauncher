using System.Net;
using System.Net.Http;
using System.IO;
using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.App.Localization;
using LegendLauncher.App.Updates;
using LegendLauncher.GameHost.Legacy;
using LegendLauncher.Infrastructure.Paths;
using LegendLauncher.Infrastructure.Persistence;
using LegendLauncher.Infrastructure.Runtime;
using LegendLauncher.Infrastructure.Security;
using LegendLauncher.Providers.Oas;
using LegendLauncher.Providers.SevenWan;

namespace LegendLauncher.App;

internal static class LauncherComposition
{
    private static readonly string[] KnownLegacyInstallationNames =
    [
        "Legend Online Client by Brov (H2_x64)",
        "Legend Online Client by Brov (64-bit)",
        "Legend Online Client by Brov"
    ];

    public static MainWindowViewModel CreateMainWindowViewModel(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        var paths = new AppPaths();
        paths.EnsureDirectories();

        var cache = new JsonServerCatalogCache(paths.CatalogCacheFile);
        var profiles = new JsonProfileStore(paths.ProfilesFile);
        var credentialVault = new WindowsCredentialVault();
        LocalizationService localization = LocalizationService.Current;
        var oasServerDirectory = new OasServerDirectory(httpClient, cache);
        var sevenWanServerDirectory = new SevenWanServerDirectory(httpClient, cache);
        var oasAuthentication = new OasAuthenticationService();
        var sevenWanAuthentication = new UnavailablePlatformAuthenticationService(
            "sevenwan_service_unavailable",
            "O catálogo 7wan foi reconhecido, mas seus servidores estão marcados como encerrados pela plataforma.");
        var adapters = OasPlatformCatalog.All
            .Select(platform => new PlatformAdapter(
                platform,
                oasServerDirectory,
                oasAuthentication))
            .Concat(SevenWanPlatformCatalog.All.Select(platform => new PlatformAdapter(
                platform,
                sevenWanServerDirectory,
                sevenWanAuthentication)));
        var platformRegistry = new PlatformAdapterRegistry(adapters);
        var gameRuntime = new LegacyGameRuntime();
        var profileStorage = new ProfileStorageCoordinator(profiles, credentialVault);
        var settings = new LauncherSettingsService(paths.SettingsFile);
        var updateService = new LauncherUpdateService(httpClient, paths.UpdatesDirectory);
        var workspace = new GameWorkspaceViewModel(
            new GameAudioService(),
            settings,
            localization: localization);

        string? configuredRuntime = FindLegacyRuntimeCandidate();
        LegacyRuntimeProbeResult runtime = new LegacyRuntimeProbe().Probe(
            configuredPath: configuredRuntime,
            startDirectory: AppContext.BaseDirectory);
        var sessionLauncher = new SessionLaunchCoordinator(
            credentialVault,
            platformRegistry,
            gameRuntime,
            runtime.RuntimeDirectory,
            profiles);

        return new MainWindowViewModel(
            platformRegistry,
            profileStorage,
            sessionLauncher,
            runtime,
            platformRegistry.Platforms,
            settingsService: settings,
            workspace: workspace,
            localization: localization,
            updateService: updateService);
    }

    public static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(8),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        };
        var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        Version version = typeof(LauncherComposition).Assembly.GetName().Version ??
            new Version(1, 0, 0);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"UrusLauncher/{version.ToString(3)}");
        return client;
    }

    private static string? FindLegacyRuntimeCandidate() => FindLegacyRuntimeCandidate(
        AppContext.BaseDirectory,
        Environment.GetEnvironmentVariable("LEGEND_LEGACY_ROOT"),
        [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        ]);

    internal static string? FindLegacyRuntimeCandidate(
        string applicationDirectory,
        string? configuredPath,
        IEnumerable<string> programFilesRoots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        ArgumentNullException.ThrowIfNull(programFilesRoots);

        string bundledRuntime = Path.Combine(applicationDirectory, "runtime");
        if (Directory.Exists(bundledRuntime))
        {
            return bundledRuntime;
        }

        if (!string.IsNullOrWhiteSpace(configuredPath) && Directory.Exists(configuredPath))
        {
            return configuredPath;
        }

        return programFilesRoots
            .Where(static root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(root => KnownLegacyInstallationNames.Select(name => Path.Combine(root, name)))
            .FirstOrDefault(Directory.Exists);
    }
}
