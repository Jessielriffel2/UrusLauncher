using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.SevenWan;

/// <summary>
/// Wartune and Wartune Reborn variants exposed by the legacy Brov selector.
/// </summary>
public static class SevenWanPlatformCatalog
{
    private static readonly Uri WartuneEndpoint =
        new("https://wartune.wan.com/index/getServerListByGid?gid=25", UriKind.Absolute);
    private static readonly Uri RebornEndpoint =
        new("https://wartunereborn.wan.com/index/getServerListByGid?gid=10", UriKind.Absolute);

    private static readonly SevenWanPlatformVariant[] Variants =
    [
        Wartune("R2-US West", 6),
        Wartune("R2-US East", 7),
        Wartune("R2-Oceanic", 9),
        Wartune("R2-Europe", 8),
        Wartune("Proficient Ct", 1),
        Wartune("Koramgame", 14),
        Wartune("Kongregate", 3),
        Wartune("Kabam", 2),
        Wartune("Ennia", 5),
        Wartune("Armor", 4),
        Wartune("Agame", 15),
        Reborn("R2", 10),
        Reborn("Proficient-Ct", 13),
        Reborn("Kongregate", 16),
    ];

    public static IReadOnlyList<PlatformDefinition> All { get; } =
        Array.AsReadOnly(Variants.Select(static variant => variant.Platform).ToArray());

    public static SevenWanPlatformVariant? Find(string platformId) =>
        Variants.FirstOrDefault(variant =>
            string.Equals(
                variant.Platform.Id,
                platformId,
                StringComparison.OrdinalIgnoreCase));

    private static SevenWanPlatformVariant Wartune(string providerLabel, int providerPlatformId) =>
        Create(
            "wt7wan",
            $"Wartune - {providerLabel} (7wan)",
            providerPlatformId,
            WartuneEndpoint);

    private static SevenWanPlatformVariant Reborn(string providerLabel, int providerPlatformId) =>
        Create(
            "wtr7wan",
            $"Wartune Reborn - {providerLabel} (7wan)",
            providerPlatformId,
            RebornEndpoint);

    private static SevenWanPlatformVariant Create(
        string gameCode,
        string displayName,
        int providerPlatformId,
        Uri endpoint)
    {
        var platform = new PlatformDefinition(
            $"sevenwan-{gameCode}-{providerPlatformId}",
            displayName,
            gameCode,
            endpoint,
            "en-US");
        return new SevenWanPlatformVariant(platform, providerPlatformId);
    }
}

public sealed record SevenWanPlatformVariant(
    PlatformDefinition Platform,
    int ProviderPlatformId);
