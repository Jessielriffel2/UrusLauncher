using LegendLauncher.Core.Models;

namespace LegendLauncher.Core.Contracts;

/// <summary>
/// Creates a new game account on a platform that allows it from inside the launcher.
/// </summary>
public interface IAccountRegistrationService
{
    Task<AccountRegistrationResult> RegisterAsync(
        AccountRegistrationRequest request,
        CancellationToken cancellationToken = default);
}
