using System.Globalization;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.Oas;

/// <summary>
/// Known OAS platform variants supported by the legacy server-list API.
/// </summary>
public static class OasPlatformCatalog
{
    private static readonly Uri ServerListEndpoint =
        new("https://odp3.oasgames.com/api/game/serverlist", UriKind.Absolute);

    public static PlatformDefinition Brazil { get; } = new(
        "oas-lobr",
        "Legend Online Brasil (OAS)",
        "lobr",
        ServerListEndpoint,
        "pt-BR");

    public static PlatformDefinition Turkish { get; } = new(
        "oas-lotr",
        "Legend Online Türkçe (OAS)",
        "lotr",
        ServerListEndpoint,
        "tr-TR");

    public static PlatformDefinition ClassicPortuguese { get; } = new(
        "oas-lorpt",
        "Legend Online Classic Português (OAS)",
        "lorpt",
        ServerListEndpoint,
        "pt-PT");

    public static PlatformDefinition RebornTurkish { get; } = new(
        "oas-lortr",
        "Legend Online Reborn Türkçe (OAS)",
        "lortr",
        ServerListEndpoint,
        "tr-TR");

    public static PlatformDefinition Polish { get; } = new(
        "oas-lopl",
        "Legend Online Polska (OAS)",
        "lopl",
        ServerListEndpoint,
        "pl-PL");

    public static PlatformDefinition Spanish { get; } = new(
        "oas-loes",
        "Legend Online Español (OAS)",
        "loes",
        ServerListEndpoint,
        "es-ES");

    public static PlatformDefinition German { get; } = new(
        "oas-lode",
        "Legend Online Deutsch (OAS)",
        "lode",
        ServerListEndpoint,
        "de-DE");

    public static PlatformDefinition Arabic { get; } = new(
        "oas-loar",
        "Legend Online عربي (OAS)",
        "loar",
        ServerListEndpoint,
        "ar");

    public static IReadOnlyList<PlatformDefinition> All { get; } =
        Array.AsReadOnly(
        [
            Brazil,
            Turkish,
            ClassicPortuguese,
            RebornTurkish,
            Polish,
            Spanish,
            German,
            Arabic,
        ]);

    public static PlatformDefinition? Find(string platformId) =>
        All.FirstOrDefault(platform =>
            string.Equals(platform.Id, platformId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Rebuilds the launch address of a known OAS server from its numeric identifier, so a profile
    /// that already played can keep its target when the remote catalog is unavailable. The address
    /// is only produced for platforms in this verified catalog, using the same platform guard the
    /// server directory applies, and only for identifiers the passport flow accepts, so a caller can
    /// never invent a host, a port or a path.
    /// </summary>
    public static bool TryBuildFallbackLaunchUri(
        PlatformDefinition platform,
        string? serverId,
        out Uri? launchUri)
    {
        ArgumentNullException.ThrowIfNull(platform);

        launchUri = null;
        string trimmedId = serverId?.Trim() ?? string.Empty;
        if (trimmedId.Length == 0 || !trimmedId.All(char.IsAsciiDigit))
        {
            return false;
        }

        PlatformDefinition? knownPlatform = Find(platform.Id);
        if (knownPlatform is null ||
            !string.Equals(platform.GameCode, knownPlatform.GameCode, StringComparison.OrdinalIgnoreCase) ||
            !Uri.Equals(platform.ServerListEndpoint, knownPlatform.ServerListEndpoint))
        {
            return false;
        }

        launchUri = new Uri(
            string.Create(
                CultureInfo.InvariantCulture,
                $"https://{knownPlatform.GameCode}.creaction-network.com/serverlist/s{trimmedId}"),
            UriKind.Absolute);
        return true;
    }
}
