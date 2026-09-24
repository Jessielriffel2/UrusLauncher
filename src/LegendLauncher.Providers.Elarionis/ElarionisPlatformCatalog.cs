using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.Elarionis;

/// <summary>
/// Single Elarionis Online platform backed by the public shard catalog.
/// The 15 game servers (S1 RACON through S15 ASGARD) are discovered as one
/// catalog so a single account identity covers every server, and each server
/// keeps an independent launch history entry like the OAS variants do.
/// </summary>
public static class ElarionisPlatformCatalog
{
    private static readonly Uri ServerListEndpoint =
        new("https://elarionis.online/api/shards", UriKind.Absolute);

    public static PlatformDefinition Elarionis { get; } = new(
        "elarionis-lo",
        "Elarionis Online",
        "elo",
        ServerListEndpoint,
        "pt-BR");

    public static IReadOnlyList<PlatformDefinition> All { get; } =
        Array.AsReadOnly([Elarionis]);

    public static PlatformDefinition? Find(string platformId) =>
        string.Equals(platformId, Elarionis.Id, StringComparison.OrdinalIgnoreCase)
            ? Elarionis
            : null;
}
