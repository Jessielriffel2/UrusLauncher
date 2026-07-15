using LegendLauncher.Core.Models;

namespace LegendLauncher.Core.Contracts;

public interface ICredentialVault
{
    Task<CredentialSecret?> GetAsync(
        string credentialKey,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        string credentialKey,
        CredentialSecret credential,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string credentialKey,
        CancellationToken cancellationToken = default);
}
