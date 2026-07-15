using LegendLauncher.App.ViewModels;
using LegendLauncher.App.Localization;
using LegendLauncher.Core.Models;

namespace LegendLauncher.App.Services;

internal static class ServerCatalogPresentation
{
    public static string? ResolveLastPlayedServerId(
        AccountProfile? profile,
        string platformId)
    {
        if (profile is null ||
            !string.Equals(profile.PlatformId, platformId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? recentServerId = (profile.RecentServerIds ?? [])
            .FirstOrDefault(static serverId => !string.IsNullOrWhiteSpace(serverId));
        string? resolved = recentServerId ?? profile.LastServerId;
        return string.IsNullOrWhiteSpace(resolved) ? null : resolved.Trim();
    }

    public static IReadOnlyList<ServerRowViewModel> BuildRows(
        ServerCatalog catalog,
        DateTimeOffset now,
        string? accountLastServerId = null,
        LocalizationService? localization = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var playedServerIds = new HashSet<string>(
            catalog.Played.Select(static server => server.Id),
            StringComparer.OrdinalIgnoreCase);
        string? currentServerId = string.IsNullOrWhiteSpace(accountLastServerId)
            ? catalog.Current?.Id
            : accountLastServerId.Trim();
        GameServer? latestReleased = catalog.All
            .Where(server => server.IsAvailable(now))
            .OrderByDescending(static server =>
                server.StartTimeUtc ?? DateTimeOffset.MinValue)
            .ThenByDescending(static server => server.NumericId)
            .FirstOrDefault();

        ServerRowViewModel[] rows = catalog.All
            .Select(server => new ServerRowViewModel(
                server,
                now,
                playedServerIds.Contains(server.Id),
                string.Equals(server.Id, currentServerId, StringComparison.OrdinalIgnoreCase),
                string.Equals(server.Id, latestReleased?.Id, StringComparison.OrdinalIgnoreCase),
                localization))
            .OrderByDescending(static server => server.IsCurrent)
            .ThenByDescending(static server => server.IsLatestReleased)
            .ThenByDescending(static server => server.Model.NumericId)
            .ToArray();
        return ConfigureSectionDivider(rows);
    }

    public static IReadOnlyList<ServerRowViewModel> Filter(
        IEnumerable<ServerRowViewModel> servers,
        string query)
    {
        ArgumentNullException.ThrowIfNull(servers);
        ServerRowViewModel[] filtered = servers
            .Where(server => server.Matches(query))
            .ToArray();
        return ConfigureSectionDivider(filtered);
    }

    public static ServerRowViewModel? Choose(
        IReadOnlyList<ServerRowViewModel> visibleServers,
        string? desiredId)
    {
        ArgumentNullException.ThrowIfNull(visibleServers);
        ServerRowViewModel? desired = desiredId is null
            ? null
            : visibleServers.FirstOrDefault(server =>
                string.Equals(server.Id, desiredId, StringComparison.OrdinalIgnoreCase) &&
                server.CanLaunch);
        return desired ??
            visibleServers.FirstOrDefault(static server => server.IsCurrent && server.CanLaunch) ??
            visibleServers.FirstOrDefault(static server => server.IsLatestReleased && server.CanLaunch) ??
            visibleServers.FirstOrDefault(static server => server.CanLaunch);
    }

    private static IReadOnlyList<ServerRowViewModel> ConfigureSectionDivider(
        IReadOnlyList<ServerRowViewModel> rows)
    {
        foreach (ServerRowViewModel row in rows)
        {
            row.SetSectionDivider(visible: false);
        }

        if (rows.Count > 1 && rows[0].IsCurrent)
        {
            rows[1].SetSectionDivider(visible: true);
        }

        return rows;
    }
}
