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
}
