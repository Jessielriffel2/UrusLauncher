namespace LegendLauncher.Core.Models;

public enum ServerCatalogSource
{
    Remote,
    Cache,
}

/// <summary>
/// A normalized snapshot of all, previously played, and current servers.
/// </summary>
public sealed record ServerCatalog(
    string PlatformId,
    IReadOnlyList<GameServer> All,
    IReadOnlyList<GameServer> Played,
    GameServer? Current,
    DateTimeOffset RetrievedAtUtc,
    ServerCatalogSource Source = ServerCatalogSource.Remote)
{
    public bool IsFromCache => Source == ServerCatalogSource.Cache;

    public ServerCatalog AsCached() => this with { Source = ServerCatalogSource.Cache };
}
