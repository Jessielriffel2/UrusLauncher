using System.Net;
using System.Net.Http;
using System.IO;
using LegendLauncher.App.GameHosting;
using LegendLauncher.App.Services;
using LegendLauncher.App.ViewModels;
using LegendLauncher.App.Localization;
using LegendLauncher.App.Updates;
using LegendLauncher.Core.Contracts;
using LegendLauncher.GameHost.Legacy;
using LegendLauncher.Infrastructure.Logging;
using LegendLauncher.Infrastructure.Paths;
using LegendLauncher.Infrastructure.Persistence;
using LegendLauncher.Infrastructure.Runtime;
using LegendLauncher.Infrastructure.Security;
using LegendLauncher.Providers.SwordOnline;
#if LOREBORN_LOCAL
using LegendLauncher.Providers.Local;
#endif

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
        var profilePreferences = new ProfilePreferencesStore(paths.ProfilePreferencesFile);
        var avatarStore = new ProfileAvatarStore(paths.ProfileAvatarsDirectory);

        var profiles = new JsonProfileStore(paths.ProfilesFile);
        var credentialVault = new WindowsCredentialVault();
        LocalizationService localization = LocalizationService.Current;
        var swordOnlineServerDirectory = new SwordOnlineServerDirectory();
        var swordOnlineAuthentication = new SwordOnlineAuthenticationService(httpClient);
        // SwordOnline is the only public provider: the OAS and 7wan catalogs stopped answering (the
        // OAS edge answers 403 and every 7wan server is closed), so they are no longer offered.
        var adapters = SwordOnlinePlatformCatalog.All
            .Select(platform => new PlatformAdapter(
                platform,
                swordOnlineServerDirectory,
                swordOnlineAuthentication));
#if LOREBORN_LOCAL
        // Developer-only: LOReborn running on this machine. Compiled out of public releases
        // (IncludeLocalProvider=false), so it cannot be enabled by an end user.
        adapters = adapters.Concat(LocalPlatformCatalog.All.Select(platform => new PlatformAdapter(
            platform,
            new LocalServerDirectory(httpClient),
            new SwordOnlineAuthenticationService(httpClient, LocalPlatformCatalog.Portal))));
#endif
        var platformRegistry = new PlatformAdapterRegistry(adapters);

        string? configuredRuntime = FindLegacyRuntimeCandidate();
        LegacyRuntimeProbeResult runtime = new LegacyRuntimeProbe().Probe(
            configuredPath: configuredRuntime,
            startDirectory: AppContext.BaseDirectory);

        var gameRuntime = BuildGameRuntime(runtime.RuntimeDirectory);
        var profileStorage = new ProfileStorageCoordinator(
            profiles,
            credentialVault,
            avatarStore: avatarStore,
            preferencesStore: profilePreferences);
        var settings = new LauncherSettingsService(paths.SettingsFile);
        var updateService = new LauncherUpdateService(httpClient, paths.UpdatesDirectory);
        var workspace = new GameWorkspaceViewModel(
            new GameAudioService(),
            settings,
            localization: localization,
            avatarStore: avatarStore);

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
            updateService: updateService,
            diagnosticLog: DiagnosticLog.Current,
            profilePreferences: profilePreferences,
            avatarStore: avatarStore,
            accountRegistration: new SwordOnlineAccountRegistrationService(httpClient));
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

    /// <summary>
    /// Builds the game runtime. Every provider runs on the legacy Flash ActiveX hosted by
    /// <c>LegacyGameRuntime</c>; the platform router only exists for the developer-only local build.
    /// </summary>
    private static IGameRuntime BuildGameRuntime(string? legacyRuntimeDirectory)
    {
        var legacy = new LegacyGameRuntime();
#if LOREBORN_LOCAL
        string[] localPlatformIds = LocalPlatformCatalog.All
            .Select(static platform => platform.Id)
            .ToArray();

        return new PlatformRoutingGameRuntime(
            legacy,
            legacy,
            localPlatformIds);
#else
        return legacy;
#endif
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
