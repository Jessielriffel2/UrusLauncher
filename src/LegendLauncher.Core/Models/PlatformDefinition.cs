namespace LegendLauncher.Core.Models;

/// <summary>
/// Describes a game platform whose servers can be discovered by a provider.
/// </summary>
/// <param name="RequiresPassword">
/// Indicates whether a session requires a password. Local providers served by the
/// user's own machine launch without credentials and set this to <see langword="false"/>.
/// </param>
/// <param name="IsHot">
/// Marks the platform the launcher highlights with an animated "HOT" badge next to its name.
/// </param>
/// <param name="SupportsAccountCreation">
/// Indicates whether the launcher can create a new account on this platform from its own UI.
/// </param>
public sealed record PlatformDefinition(
    string Id,
    string DisplayName,
    string GameCode,
    Uri ServerListEndpoint,
    string Locale,
    bool RequiresPassword = true,
    bool IsHot = false,
    bool SupportsAccountCreation = false);
