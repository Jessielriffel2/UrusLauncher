using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.SwordOnline;

/// <summary>
/// Exposes the single SwordOnline server. There is no remote listing to fetch: the only
/// runtime question is whether the portal still serves <c>config.xml</c>, which decides
/// <see cref="GameServer.IsValid"/> so the UI can warn the player instead of failing
/// halfway through a launch.
/// </summary>
public sealed class SwordOnlineServerDirectory : IServerDirectory
{
    /// <summary>Identifier of the one and only SwordOnline server.</summary>
    public const string ServerId = "swordonline";

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;

    public SwordOnlineServerDirectory(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<ServerCatalog> GetServersAsync(
        PlatformDefinition platform,
        long userId = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(platform);

        if (!string.Equals(platform.Id, SwordOnlinePlatformCatalog.SwordOnline.Id, StringComparison.OrdinalIgnoreCase))
        {
            return new ServerCatalog(
                platform.Id,
                Array.Empty<GameServer>(),
                Array.Empty<GameServer>(),
                null,
                DateTimeOffset.UtcNow);
        }

        var server = new GameServer(
            ServerId,
            1L,
            "swo",
            "SwordOnline",
            "SwordOnline (swordonline.tech)",
            SwordOnlinePlatformCatalog.MovieUri,
            IsRecommended: true,
            IsValid: await IsPortalReachableAsync(cancellationToken).ConfigureAwait(false),
            Merger: null,
            StartTimeUtc: null);

        return new ServerCatalog(
            platform.Id,
            Array.AsReadOnly([server]),
            Array.AsReadOnly([server]),
            server,
            DateTimeOffset.UtcNow,
            ServerCatalogSource.Remote);
    }

    private async Task<bool> IsPortalReachableAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        try
        {
            // config.xml is the file the game itself reads first, so a 200 here means the
            // portal can really serve a session.
            using var response = await _httpClient
                .GetAsync(SwordOnlinePlatformCatalog.ConfigUri, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // An unreachable portal is an expected state: the server is reported invalid
            // and the player is told the game is offline.
            return false;
        }
    }
}
