using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.SwordOnline;

/// <summary>
/// The SwordOnline private server (LOReborn engine) hosted at <c>swordonline.tech</c>.
/// One platform and one server: accounts are created on the website and the launcher
/// exchanges login + password for a short-lived game ticket.
/// </summary>
public static class SwordOnlinePlatformCatalog
{
    /// <summary>Public host serving the website, the assets and the launcher API.</summary>
    public const string Host = "swordonline.tech";

    /// <summary>Movie loaded by the Flash runtime.</summary>
    public const string MoviePath = "/Loading.swf";

    /// <summary>Launcher endpoint that trades login + password for a game ticket.</summary>
    public const string LauncherPath = "/api/launcher";

    /// <summary>Website endpoint that creates a new account (the one behind the "Criar conta" page).</summary>
    public const string RegisterPath = "/api/conta/criar";

    public static Uri PortalBaseUri { get; } = new($"https://{Host}/", UriKind.Absolute);

    public static Uri MovieUri { get; } = new(PortalBaseUri, MoviePath);

    public static Uri ConfigUri { get; } = new(PortalBaseUri, "/config.xml");

    public static Uri LauncherUri { get; } = new(PortalBaseUri, LauncherPath);

    public static Uri RegisterUri { get; } = new(PortalBaseUri, RegisterPath);

    /// <summary>
    /// SwordOnline platform. <c>RequiresPassword</c> keeps its default (<see langword="true"/>):
    /// the account lives on the website, so the launcher always authenticates.
    /// </summary>
    public static PlatformDefinition SwordOnline { get; } = new(
        "swordonline",
        "SwordOnline",
        "swo",
        ConfigUri,
        "pt-BR",
        IsHot: true,
        SupportsAccountCreation: true,
        AcceptsEmailLogin: false);

    /// <summary>Production portal description used by <see cref="SwordOnlineAuthenticationService"/>.</summary>
    public static SwordOnlinePortal Portal { get; } = new(
        "swordonline",
        SwordOnlineServerDirectory.ServerId,
        LauncherUri,
        ConfigUri,
        MovieUri,
        // "https:" (with the colon) makes the client rewrite its http:// config
        // entries to https:// by itself (ComponentSetting.ISFROMHTTPS).
        "https:");

    public static IReadOnlyList<PlatformDefinition> All { get; } =
        Array.AsReadOnly([SwordOnline]);

    public static PlatformDefinition? Find(string platformId) =>
        string.Equals(platformId, SwordOnline.Id, StringComparison.OrdinalIgnoreCase)
            ? SwordOnline
            : null;
}
