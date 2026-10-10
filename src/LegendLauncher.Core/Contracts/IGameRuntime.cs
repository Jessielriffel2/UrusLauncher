using LegendLauncher.Core.Models;

namespace LegendLauncher.Core.Contracts;

public interface IGameRuntime
{
    /// <summary>
    /// Starts the game for <paramref name="platform"/>. The platform travels with the
    /// launch because more than one runtime can serve it: a game served from the user's
    /// own machine does not run on the legacy Flash ActiveX, while remote providers do.
    /// </summary>
    Task<GameSession> LaunchAsync(
        PlatformDefinition platform,
        LaunchSession session,
        GameRuntimeOptions options,
        CancellationToken cancellationToken = default);
}
