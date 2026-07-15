using System.Text.Json;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;
using LegendLauncher.Infrastructure.Security;

namespace LegendLauncher.Infrastructure.Persistence;

/// <summary>
/// Persists non-secret account profiles. Passwords are intentionally absent from
/// <see cref="AccountProfile"/> and belong only in the credential vault.
/// </summary>
public sealed class JsonProfileStore : IProfileStore
{
    private readonly JsonProfileRepository<AccountProfile, Guid> _repository;

    public JsonProfileStore(
        string filePath,
        JsonSerializerOptions? serializerOptions = null)
    {
        _repository = new JsonProfileRepository<AccountProfile, Guid>(
            filePath,
            static profile => profile.Id,
            serializerOptions: serializerOptions);
    }

    public Task<IReadOnlyList<AccountProfile>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<AccountProfile?> GetAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        ValidateProfileId(profileId);
        return _repository.FindAsync(profileId, cancellationToken);
    }

    public Task SaveAsync(
        AccountProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateProfileId(profile.Id);
        CredentialKey.Validate(profile.CredentialKey);
        return _repository.UpsertAsync(profile, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        ValidateProfileId(profileId);
        await _repository.DeleteAsync(profileId, cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateProfileId(Guid profileId)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("A profile identifier cannot be empty.", nameof(profileId));
        }
    }
}
