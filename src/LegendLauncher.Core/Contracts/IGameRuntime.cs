using LegendLauncher.Core.Models;

namespace LegendLauncher.Core.Contracts;

public interface IGameRuntime
{
    Task<GameSession> LaunchAsync(
        LaunchSession session,
        GameRuntimeOptions options,
        CancellationToken cancellationToken = default);
}
