using LegendLauncher.Core.Models;

namespace LegendLauncher.Core.Contracts;

public interface IServerDirectory
{
    Task<ServerCatalog> GetServersAsync(
        PlatformDefinition platform,
        long userId = 0,
        CancellationToken cancellationToken = default);
}
