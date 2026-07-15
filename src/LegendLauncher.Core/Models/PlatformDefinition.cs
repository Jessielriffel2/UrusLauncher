namespace LegendLauncher.Core.Models;

/// <summary>
/// Describes a game platform whose servers can be discovered by a provider.
/// </summary>
public sealed record PlatformDefinition(
    string Id,
    string DisplayName,
    string GameCode,
    Uri ServerListEndpoint,
    string Locale);
