using LegendLauncher.Core.Models;

namespace LegendLauncher.Core.Contracts;

public interface IGameAuthenticationService
{
    Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken = default);
}
