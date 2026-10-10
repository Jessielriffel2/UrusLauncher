using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.SwordOnline;

/// <summary>
/// Exposes the single SwordOnline server. There is no remote listing to fetch and no availability
/// probe: the server is always launchable, so the play button never stays disabled for a reason the
/// player cannot see (a slow PC or connection used to turn a 5 s <c>config.xml</c> probe into a dead
/// button). If the portal really is down, the login itself reports it with a clear message.
/// </summary>
public sealed class SwordOnlineServerDirectory : IServerDirectory
{
    /// <summary>Identifier of the one and only SwordOnline server.</summary>
    public const string ServerId = "swordonline";

    public Task<ServerCatalog> GetServersAsync(
        PlatformDefinition platform,
        long userId = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(platform);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(platform.Id, SwordOnlinePlatformCatalog.SwordOnline.Id, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new ServerCatalog(
                platform.Id,
                Array.Empty<GameServer>(),
                Array.Empty<GameServer>(),
                null,
                DateTimeOffset.UtcNow));
        }

        var server = new GameServer(
            ServerId,
            1L,
            "swo",
            "SwordOnline",
            "SwordOnline (swordonline.tech)",
            SwordOnlinePlatformCatalog.MovieUri,
            IsRecommended: true,
            IsValid: true,
            Merger: null,
            StartTimeUtc: null);

        return Task.FromResult(new ServerCatalog(
            platform.Id,
            Array.AsReadOnly([server]),
            Array.AsReadOnly([server]),
            server,
            DateTimeOffset.UtcNow,
            ServerCatalogSource.Remote));
    }
}
