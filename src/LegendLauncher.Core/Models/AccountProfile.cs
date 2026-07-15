namespace LegendLauncher.Core.Models;

/// <summary>
/// Non-secret account settings saved by the launcher.
/// </summary>
public sealed record AccountProfile(
    Guid Id,
    string DisplayName,
    string PlatformId,
    string UserName,
    string CredentialKey,
    long? ProviderUserId,
    string? LastServerId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    /// <summary>
    /// Server identifiers ordered from the most recently launched to the oldest.
    /// The initializer keeps profiles written before this field was introduced compatible.
    /// </summary>
    public IReadOnlyList<string> RecentServerIds { get; init; } = [];

    /// <summary>
    /// Provider user identifiers keyed by platform. The legacy scalar remains as the
    /// mirror for <see cref="PlatformId"/> so older launcher versions can still read it.
    /// </summary>
    public IReadOnlyDictionary<string, long> ProviderUserIdsByPlatform { get; init; } =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Recent server identifiers keyed by platform and ordered from newest to oldest.
    /// The legacy list remains as the mirror for <see cref="PlatformId"/>.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> RecentServerIdsByPlatform { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the provider identifier for one platform without leaking the legacy value
    /// to a different platform.
    /// </summary>
    public long? GetProviderUserId(string platformId)
    {
        if (!TryNormalizePlatformId(platformId, out string normalizedPlatformId))
        {
            return null;
        }

        if (TryGetProviderUserIdFromMap(normalizedPlatformId, out long providerUserId))
        {
            return providerUserId;
        }

        return IsLegacyPlatform(normalizedPlatformId) ? ProviderUserId : null;
    }

    /// <summary>
    /// Reads recent servers for one platform. Profiles created before the platform map
    /// use their legacy list, or their legacy last server when no list existed yet.
    /// </summary>
    public IReadOnlyList<string> GetRecentServerIds(string platformId)
    {
        if (!TryNormalizePlatformId(platformId, out string normalizedPlatformId))
        {
            return [];
        }

        if (TryGetRecentServerIdsFromMap(normalizedPlatformId, out IReadOnlyList<string> recentServerIds))
        {
            return recentServerIds;
        }

        return IsLegacyPlatform(normalizedPlatformId)
            ? GetLegacyRecentServerIds()
            : [];
    }

    /// <summary>
    /// Reads the most recently used server for one platform.
    /// </summary>
    public string? GetLastServerId(string platformId) =>
        GetRecentServerIds(platformId).FirstOrDefault();

    /// <summary>
    /// Creates a profile snapshot after a successful launch. Existing legacy state is
    /// first materialized in the platform maps, then the target platform is updated.
    /// Legacy scalar fields mirror the target platform for backward compatibility.
    /// </summary>
    public AccountProfile WithPlatformLaunchState(
        string platformId,
        long? providerUserId,
        IReadOnlyList<string> recentServerIds,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);
        ArgumentNullException.ThrowIfNull(recentServerIds);

        string normalizedPlatformId = platformId.Trim();
        IReadOnlyList<string> normalizedRecentServerIds = NormalizeServerIds(recentServerIds);
        Dictionary<string, long> providerUserIds = CopyProviderUserIds();
        Dictionary<string, IReadOnlyList<string>> recentServerIdsByPlatform = CopyRecentServerIds();
        MaterializeLegacyState(providerUserIds, recentServerIdsByPlatform);

        long? effectiveProviderUserId = providerUserId;
        if (effectiveProviderUserId is null &&
            providerUserIds.TryGetValue(normalizedPlatformId, out long existingProviderUserId))
        {
            effectiveProviderUserId = existingProviderUserId;
        }

        if (effectiveProviderUserId is { } resolvedProviderUserId)
        {
            providerUserIds[normalizedPlatformId] = resolvedProviderUserId;
        }

        recentServerIdsByPlatform[normalizedPlatformId] = normalizedRecentServerIds;
        return this with
        {
            PlatformId = normalizedPlatformId,
            ProviderUserId = effectiveProviderUserId,
            LastServerId = normalizedRecentServerIds.FirstOrDefault(),
            RecentServerIds = normalizedRecentServerIds,
            ProviderUserIdsByPlatform = providerUserIds,
            RecentServerIdsByPlatform = recentServerIdsByPlatform,
            UpdatedAtUtc = updatedAtUtc,
        };
    }

    private bool TryGetProviderUserIdFromMap(string platformId, out long providerUserId)
    {
        if (ProviderUserIdsByPlatform is not null)
        {
            if (ProviderUserIdsByPlatform.TryGetValue(platformId, out providerUserId))
            {
                return true;
            }

            foreach ((string key, long value) in ProviderUserIdsByPlatform)
            {
                if (PlatformIdsEqual(key, platformId))
                {
                    providerUserId = value;
                    return true;
                }
            }
        }

        providerUserId = default;
        return false;
    }

    private bool TryGetRecentServerIdsFromMap(
        string platformId,
        out IReadOnlyList<string> recentServerIds)
    {
        if (RecentServerIdsByPlatform is not null)
        {
            if (RecentServerIdsByPlatform.TryGetValue(platformId, out IReadOnlyList<string>? direct))
            {
                recentServerIds = NormalizeServerIds(direct);
                return true;
            }

            foreach ((string key, IReadOnlyList<string> value) in RecentServerIdsByPlatform)
            {
                if (PlatformIdsEqual(key, platformId))
                {
                    recentServerIds = NormalizeServerIds(value);
                    return true;
                }
            }
        }

        recentServerIds = [];
        return false;
    }

    private Dictionary<string, long> CopyProviderUserIds()
    {
        var copy = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (ProviderUserIdsByPlatform is null)
        {
            return copy;
        }

        foreach ((string platformId, long providerUserId) in ProviderUserIdsByPlatform)
        {
            if (TryNormalizePlatformId(platformId, out string normalizedPlatformId))
            {
                copy[normalizedPlatformId] = providerUserId;
            }
        }

        return copy;
    }

    private Dictionary<string, IReadOnlyList<string>> CopyRecentServerIds()
    {
        var copy = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (RecentServerIdsByPlatform is null)
        {
            return copy;
        }

        foreach ((string platformId, IReadOnlyList<string> serverIds) in RecentServerIdsByPlatform)
        {
            if (TryNormalizePlatformId(platformId, out string normalizedPlatformId))
            {
                copy[normalizedPlatformId] = NormalizeServerIds(serverIds);
            }
        }

        return copy;
    }

    private void MaterializeLegacyState(
        IDictionary<string, long> providerUserIds,
        IDictionary<string, IReadOnlyList<string>> recentServerIdsByPlatform)
    {
        if (!TryNormalizePlatformId(PlatformId, out string legacyPlatformId))
        {
            return;
        }

        if (ProviderUserId is { } legacyProviderUserId &&
            !providerUserIds.ContainsKey(legacyPlatformId))
        {
            providerUserIds[legacyPlatformId] = legacyProviderUserId;
        }

        if (!recentServerIdsByPlatform.ContainsKey(legacyPlatformId))
        {
            IReadOnlyList<string> legacyRecentServerIds = GetLegacyRecentServerIds();
            if (legacyRecentServerIds.Count > 0)
            {
                recentServerIdsByPlatform[legacyPlatformId] = legacyRecentServerIds;
            }
        }
    }

    private IReadOnlyList<string> GetLegacyRecentServerIds()
    {
        IReadOnlyList<string> normalized = NormalizeServerIds(RecentServerIds);
        return normalized.Count > 0
            ? normalized
            : NormalizeServerIds([LastServerId ?? string.Empty]);
    }

    private bool IsLegacyPlatform(string platformId) => PlatformIdsEqual(PlatformId, platformId);

    private static bool PlatformIdsEqual(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) &&
        !string.IsNullOrWhiteSpace(second) &&
        string.Equals(first.Trim(), second.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool TryNormalizePlatformId(string? platformId, out string normalizedPlatformId)
    {
        normalizedPlatformId = platformId?.Trim() ?? string.Empty;
        return normalizedPlatformId.Length > 0;
    }

    private static IReadOnlyList<string> NormalizeServerIds(IEnumerable<string>? serverIds) =>
        serverIds is null
            ? []
            : serverIds
                .Where(static serverId => !string.IsNullOrWhiteSpace(serverId))
                .Select(static serverId => serverId.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
}
