using LegendLauncher.Core.Models;

namespace LegendLauncher.Core.Contracts;

public interface IProfileStore
{
    Task<IReadOnlyList<AccountProfile>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<AccountProfile?> GetAsync(
        Guid profileId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        AccountProfile profile,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid profileId,
        CancellationToken cancellationToken = default);
}
