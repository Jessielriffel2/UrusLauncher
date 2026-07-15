using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;
using LegendLauncher.Infrastructure.Security;

namespace LegendLauncher.App.Services;

internal sealed class ProfileStorageCoordinator
{
    private readonly IProfileStore _profileStore;
    private readonly ICredentialVault _credentialVault;
    private readonly TimeProvider _timeProvider;

    public ProfileStorageCoordinator(
        IProfileStore profileStore,
        ICredentialVault credentialVault,
        TimeProvider? timeProvider = null)
    {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _credentialVault = credentialVault ?? throw new ArgumentNullException(nameof(credentialVault));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<IReadOnlyList<AccountProfile>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        _profileStore.GetAllAsync(cancellationToken);

    public async Task<ProfileSaveOutcome> SaveAsync(
        ProfileSaveInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.UserName);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.PlatformId);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        AccountProfile? existingProfile = input.ExistingProfile;
        Guid profileId = existingProfile?.Id ?? Guid.NewGuid();
        bool keepsProviderIdentity = existingProfile is { } existing &&
            string.Equals(existing.PlatformId, input.PlatformId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.UserName, input.UserName, StringComparison.OrdinalIgnoreCase);
        bool changesProviderIdentity = existingProfile is not null && !keepsProviderIdentity;
        string credentialKey = changesProviderIdentity
            ? CreateRotatedCredentialKey(existingProfile!.CredentialKey)
            : existingProfile?.CredentialKey ?? CredentialKey.ForProfile(profileId);
        var profile = new AccountProfile(
            profileId,
            input.DisplayName,
            input.PlatformId,
            input.UserName,
            credentialKey,
            keepsProviderIdentity ? existingProfile?.ProviderUserId : null,
            keepsProviderIdentity ? existingProfile?.LastServerId : null,
            existingProfile?.CreatedAtUtc ?? now,
            now)
        {
            RecentServerIds = keepsProviderIdentity
                ? existingProfile?.RecentServerIds ?? []
                : [],
        };

        await _profileStore.SaveAsync(profile, cancellationToken).ConfigureAwait(false);

        bool credentialPersisted = true;
        try
        {
            if (input.RememberPassword && input.TypedPassword.Length > 0)
            {
                await _credentialVault
                    .SetAsync(
                        credentialKey,
                        new CredentialSecret(input.UserName, input.TypedPassword),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (!changesProviderIdentity && (!input.RememberPassword || !keepsProviderIdentity))
            {
                await _credentialVault
                    .DeleteAsync(credentialKey, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            credentialPersisted = false;
        }

        if (changesProviderIdentity)
        {
            try
            {
                await _credentialVault
                    .DeleteAsync(existingProfile!.CredentialKey, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                credentialPersisted = false;
            }
        }

        return new ProfileSaveOutcome(profile, credentialPersisted);
    }

    private static string CreateRotatedCredentialKey(string previousCredentialKey)
    {
        string credentialKey;
        do
        {
            credentialKey = CredentialKey.ForProfile(Guid.NewGuid());
        }
        while (string.Equals(
            credentialKey,
            previousCredentialKey,
            StringComparison.Ordinal));

        return credentialKey;
    }

    public async Task DeleteAsync(
        AccountProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await _credentialVault
            .DeleteAsync(profile.CredentialKey, cancellationToken)
            .ConfigureAwait(false);
        await _profileStore.DeleteAsync(profile.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> HasSavedCredentialAsync(
        AccountProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        CredentialSecret? credential = await _credentialVault
            .GetAsync(profile.CredentialKey, cancellationToken)
            .ConfigureAwait(false);
        return credential is not null &&
            string.Equals(credential.UserName, profile.UserName, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class ProfileSaveInput
{
    public ProfileSaveInput(
        AccountProfile? existingProfile,
        string displayName,
        string platformId,
        string userName,
        string typedPassword,
        bool rememberPassword)
    {
        ExistingProfile = existingProfile;
        DisplayName = displayName;
        PlatformId = platformId;
        UserName = userName;
        TypedPassword = typedPassword;
        RememberPassword = rememberPassword;
    }

    public AccountProfile? ExistingProfile { get; }

    public string DisplayName { get; }

    public string PlatformId { get; }

    public string UserName { get; }

    public string TypedPassword { get; }

    public bool RememberPassword { get; }

    public override string ToString() =>
        $"ProfileSaveInput {{ HasExistingProfile = {ExistingProfile is not null}, PlatformId = {PlatformId}, HasUserName = {UserName.Length > 0}, HasPassword = {TypedPassword.Length > 0}, RememberPassword = {RememberPassword} }}";
}

internal sealed record ProfileSaveOutcome(
    AccountProfile Profile,
    bool WasCredentialPersisted)
{
    public override string ToString() =>
        $"ProfileSaveOutcome {{ ProfileId = {Profile.Id}, WasCredentialPersisted = {WasCredentialPersisted} }}";
}
