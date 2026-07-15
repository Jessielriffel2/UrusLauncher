using LegendLauncher.Core.Models;

namespace LegendLauncher.Core.Contracts;

public interface IServerCatalogCache
{
    Task<ServerCatalog?> GetAsync(
        string platformId,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        ServerCatalog catalog,
        CancellationToken cancellationToken = default);
}
