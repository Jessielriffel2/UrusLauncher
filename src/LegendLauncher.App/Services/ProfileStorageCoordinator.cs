using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;
using LegendLauncher.Infrastructure.Logging;
using LegendLauncher.Infrastructure.Security;

namespace LegendLauncher.App.Services;

internal sealed class ProfileStorageCoordinator
{
    private readonly IProfileStore _profileStore;
    private readonly ICredentialVault _credentialVault;
    private readonly TimeProvider _timeProvider;
    private readonly ProfileAvatarStore? _avatarStore;
    private readonly ProfilePreferencesStore? _preferencesStore;

    public ProfileStorageCoordinator(
        IProfileStore profileStore,
        ICredentialVault credentialVault,
        TimeProvider? timeProvider = null,
        ProfileAvatarStore? avatarStore = null,
        ProfilePreferencesStore? preferencesStore = null)
    {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _credentialVault = credentialVault ?? throw new ArgumentNullException(nameof(credentialVault));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _avatarStore = avatarStore;
        _preferencesStore = preferencesStore;
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
            ProfilePlatformCompatibility.ShareAccountIdentity(
                existing.PlatformId,
                input.PlatformId) &&
            string.Equals(existing.UserName, input.UserName, StringComparison.OrdinalIgnoreCase);
        bool changesProviderIdentity = existingProfile is not null && !keepsProviderIdentity;
        string credentialKey = changesProviderIdentity
            ? CreateRotatedCredentialKey(existingProfile!.CredentialKey)
            : existingProfile?.CredentialKey ?? CredentialKey.ForProfile(profileId);
        string? avatarFileName = input.RemoveAvatar
            ? null
            : input.AvatarFileName ?? existingProfile?.AvatarFileName;
        AccountProfile profile = keepsProviderIdentity
            ? CreateCompatibleProfile(
                existingProfile!,
                input,
                credentialKey,
                avatarFileName,
                now)
            : new AccountProfile(
                profileId,
                input.DisplayName,
                input.PlatformId,
                input.UserName,
                credentialKey,
                null,
                null,
                existingProfile?.CreatedAtUtc ?? now,
                now) with
            {
                AvatarFileName = avatarFileName,
            };

        try
        {
            await _profileStore.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (_avatarStore is not null &&
                !string.IsNullOrWhiteSpace(input.AvatarFileName) &&
                !string.Equals(
                    input.AvatarFileName,
                    existingProfile?.AvatarFileName,
                    StringComparison.Ordinal))
            {
                await _avatarStore
                    .DeleteAsync(input.AvatarFileName, CancellationToken.None)
                    .ConfigureAwait(false);
            }

            throw;
        }
        if (_avatarStore is not null &&
            !string.IsNullOrWhiteSpace(existingProfile?.AvatarFileName) &&
            !string.Equals(
                existingProfile?.AvatarFileName,
                profile.AvatarFileName,
                StringComparison.Ordinal))
        {
            await _avatarStore
                .DeleteAsync(existingProfile!.AvatarFileName, cancellationToken)
                .ConfigureAwait(false);
        }

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
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            DiagnosticLog.Current.WriteFailure(
                "profile.credential_save",
                "The credential vault operation failed after the profile metadata was saved.",
                exception,
                ("profileId", profile.Id.ToString()),
                ("platformId", input.PlatformId),
                ("rememberPassword", input.RememberPassword.ToString()));
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
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                DiagnosticLog.Current.WriteFailure(
                    "profile.credential_rotate",
                    "The previous credential could not be deleted after the account identity changed.",
                    exception,
                    ("profileId", profile.Id.ToString()),
                    ("platformId", input.PlatformId));
                credentialPersisted = false;
            }
        }

        return new ProfileSaveOutcome(profile, credentialPersisted);
    }

    private static AccountProfile CreateCompatibleProfile(
        AccountProfile existingProfile,
        ProfileSaveInput input,
        string credentialKey,
        string? avatarFileName,
        DateTimeOffset updatedAtUtc)
    {
        AccountProfile profileWithSelectedPlatform = existingProfile.WithPlatformLaunchState(
            input.PlatformId,
            existingProfile.GetProviderUserId(input.PlatformId),
            existingProfile.GetRecentServerIds(input.PlatformId),
            updatedAtUtc);

        return profileWithSelectedPlatform with
        {
            DisplayName = input.DisplayName,
            UserName = input.UserName,
            CredentialKey = credentialKey,
            AvatarFileName = avatarFileName,
        };
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
        if (_preferencesStore is not null)
        {
            await _preferencesStore
                .DeleteAsync(profile.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        if (_avatarStore is not null)
        {
            await _avatarStore
                .DeleteAsync(profile.AvatarFileName, cancellationToken)
                .ConfigureAwait(false);
        }
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
        bool rememberPassword,
        string? avatarFileName = null,
        bool removeAvatar = false)
    {
        ExistingProfile = existingProfile;
        DisplayName = displayName;
        PlatformId = platformId;
        UserName = userName;
        TypedPassword = typedPassword;
        RememberPassword = rememberPassword;
        AvatarFileName = avatarFileName;
        RemoveAvatar = removeAvatar;
    }

    public AccountProfile? ExistingProfile { get; }

    public string DisplayName { get; }

    public string PlatformId { get; }

    public string UserName { get; }

    public string TypedPassword { get; }

    public bool RememberPassword { get; }

    public string? AvatarFileName { get; }

    public bool RemoveAvatar { get; }

    public override string ToString() =>
        $"ProfileSaveInput {{ HasExistingProfile = {ExistingProfile is not null}, PlatformId = {PlatformId}, HasUserName = {UserName.Length > 0}, HasPassword = {TypedPassword.Length > 0}, RememberPassword = {RememberPassword}, HasAvatar = {AvatarFileName is not null}, RemoveAvatar = {RemoveAvatar} }}";
}

internal sealed record ProfileSaveOutcome(
    AccountProfile Profile,
    bool WasCredentialPersisted)
{
    public override string ToString() =>
        $"ProfileSaveOutcome {{ ProfileId = {Profile.Id}, WasCredentialPersisted = {WasCredentialPersisted} }}";
}
