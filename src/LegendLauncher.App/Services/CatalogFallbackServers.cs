using System.Globalization;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.App.Services;

/// <summary>
/// Rebuilds the minimal playable server entries from identifiers the account already used, so an
/// unavailable remote catalog strands a profile that would otherwise lose its last target. Only the
/// verified OAS catalog has a verifiable address pattern, so every other platform keeps the current
/// empty-catalog behaviour unchanged.
/// </summary>
internal static class CatalogFallbackServers
{
    /// <summary>
    /// Upper bound of saved identifiers offered at once, keeping the list short and predictable.
    /// </summary>
    public const int MaxSavedServers = 3;

    public static IReadOnlyList<GameServer> FromSavedIds(
        PlatformDefinition platform,
        IEnumerable<string?>? serverIds)
    {
        ArgumentNullException.ThrowIfNull(platform);

        if (serverIds is null)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var servers = new List<GameServer>();
        foreach (string? serverId in serverIds)
        {
            if (string.IsNullOrWhiteSpace(serverId))
            {
                continue;
            }

            string trimmed = serverId.Trim();
            if (!seen.Add(trimmed) ||
                !OasPlatformCatalog.TryBuildFallbackLaunchUri(platform, trimmed, out Uri? launchUri) ||
                launchUri is null)
            {
                continue;
            }

            // "Server {id}" is the marker ServerRowViewModel recognizes as a generated label, so the
            // row renders through the localized Server_GenericName template in every language.
            string generatedName = string.Create(CultureInfo.InvariantCulture, $"Server {trimmed}");
            servers.Add(new GameServer(
                trimmed,
                long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out long numericId)
                    ? numericId
                    : 0,
                string.Empty,
                generatedName,
                generatedName,
                launchUri,
                IsRecommended: false,
                IsValid: true,
                Merger: null,
                StartTimeUtc: null));

            if (servers.Count == MaxSavedServers)
            {
                break;
            }
        }

        return servers;
    }
}
