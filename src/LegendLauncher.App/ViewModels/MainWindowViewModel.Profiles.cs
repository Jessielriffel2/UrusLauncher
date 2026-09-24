using System.IO;
using System.Windows.Media;
using LegendLauncher.App.Services;
using LegendLauncher.Core.Models;

namespace LegendLauncher.App.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private async Task LoadProfilesAsync(Guid? selectProfileId = null)
    {
        IReadOnlyList<AccountProfile> stored = await _profileStorage.GetAllAsync().ConfigureAwait(true);
        Profiles.Clear();
        foreach (AccountProfile profile in stored.OrderByDescending(static profile => profile.UpdatedAtUtc))
        {
            Profiles.Add(new ProfileItemViewModel(
                profile,
                _avatarStore?.Load(profile.AvatarFileName)));
        }

        Guid? desiredId = selectProfileId ?? SelectedProfile?.Model.Id;
        _suppressProfileCatalogReload = true;
        try
        {
            SelectedProfile = desiredId is null
                ? Profiles.FirstOrDefault()
                : Profiles.FirstOrDefault(profile => profile.Model.Id == desiredId) ?? Profiles.FirstOrDefault();
        }
        finally
        {
            _suppressProfileCatalogReload = false;
        }

        NotifyFilteredProfiles();

        if (SelectedProfile is null)
        {
            IsProfileEditorVisible = true;
        }
    }

    private async Task ApplyLaunchedProfileAsync(
        AccountProfile? requestedProfile,
        AccountProfile launchedProfile,
        bool wasProfilePersisted)
    {
        if (requestedProfile is not null && requestedProfile.Id == launchedProfile.Id)
        {
            ProfileItemViewModel? requestedItem = Profiles
                .FirstOrDefault(profile => profile.Model.Id == requestedProfile.Id);
            ReplaceProfile(requestedItem, launchedProfile);
            if (wasProfilePersisted)
            {
                await LoadServersAsync(forceRefresh: false).ConfigureAwait(true);
            }
            else
            {
                RefreshRecentServers();
            }

            return;
        }

        if (wasProfilePersisted)
        {
            await LoadProfilesAsync(launchedProfile.Id).ConfigureAwait(true);
            await LoadServersAsync(forceRefresh: false).ConfigureAwait(true);
            return;
        }

        RefreshRecentServers();
    }

    public bool HasPendingAvatarSelection =>
        _pendingAvatarSourcePath is not null || _removePendingAvatar;

    public void SetPendingAvatarSourcePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _pendingAvatarSourcePath = path;
        _removePendingAvatar = false;
        OnPropertyChanged(nameof(HasPendingAvatarSelection));
    }

    public void RemovePendingAvatar()
    {
        _pendingAvatarSourcePath = null;
        _removePendingAvatar = true;
        OnPropertyChanged(nameof(HasPendingAvatarSelection));
    }

    public ImageSource? SelectedProfileAvatarImage => _selectedProfile?.AvatarImage;

    public bool HasSelectedProfileAvatar => _selectedProfile?.HasAvatar == true;

    public string SelectedProfileDisplayName => _selectedProfile?.DisplayName ?? string.Empty;

    public string SelectedProfileInitial => _selectedProfile?.Initial ?? "?";

    private void ReplaceProfile(ProfileItemViewModel? original, AccountProfile updated)
    {
        if (original is null)
        {
            return;
        }

        int index = Profiles.IndexOf(original);
        if (index < 0)
        {
            return;
        }

        var replacement = new ProfileItemViewModel(
            updated,
            _avatarStore?.Load(updated.AvatarFileName));
        Profiles[index] = replacement;
        NotifyFilteredProfiles();
        if (ReferenceEquals(_selectedProfile, original))
        {
            _selectedProfile = replacement;
            OnPropertyChanged(nameof(SelectedProfile));
            OnPropertyChanged(nameof(SelectedProfileAvatarImage));
            OnPropertyChanged(nameof(HasSelectedProfileAvatar));
            OnPropertyChanged(nameof(SelectedProfileDisplayName));
            OnPropertyChanged(nameof(SelectedProfileInitial));
            RefreshRecentServers();
            DeleteProfileCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task SaveProfileAsync()
    {
        string displayName = ProfileLabel.Trim();
        string userName = LoginHint.Trim();
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(userName))
        {
            SetStatusMessage("Profile_RequiredFields");
            CatalogStatusBrush = WarningBrush;
            return;
        }

        try
        {
            string? avatarFileName = null;
            if (_pendingAvatarSourcePath is not null)
            {
                if (_avatarStore is null)
                {
                    SetStatusMessage("Profile_ImageUnavailable");
                    return;
                }

                avatarFileName = await _avatarStore
                    .ImportAsync(_pendingAvatarSourcePath)
                    .ConfigureAwait(true);
            }

            var input = new ProfileSaveInput(
                SelectedProfile?.Model,
                displayName,
                SelectedPlatform.Id,
                userName,
                PendingPassword,
                RememberPassword,
                avatarFileName,
                _removePendingAvatar);
            ProfileSaveOutcome outcome = await _profileStorage
                .SaveAsync(input)
                .ConfigureAwait(true);
            PendingPassword = string.Empty;
            ResetPendingAvatarSelection();
            await LoadProfilesAsync(outcome.Profile.Id).ConfigureAwait(true);
            await LoadServersAsync(forceRefresh: false).ConfigureAwait(true);
            IsProfileEditorVisible = false;
            SetStatusMessage(outcome.WasCredentialPersisted
                ? "Profile_SavedWithCredential"
                : "Profile_SavedWithoutCredential");
        }
        catch (Exception exception) when (
            exception is InvalidDataException or IOException or NotSupportedException)
        {
            SetStatusMessage("Profile_ImageUnavailable");
            CatalogStatusBrush = WarningBrush;
        }
        catch (Exception exception)
        {
            LogFailure(
                "profile.save",
                "The account profile could not be saved.",
                exception,
                ("platformId", SelectedPlatform.Id),
                ("login", userName));
            SetStatusMessage("Profile_SaveFailed");
            CatalogStatusBrush = ErrorBrush;
        }
    }

    private async Task DeleteProfileAsync()
    {
        ProfileItemViewModel? profile = SelectedProfile;
        if (profile is null)
        {
            return;
        }

        try
        {
            await _profileStorage.DeleteAsync(profile.Model).ConfigureAwait(true);
            SelectedProfile = null;
            await LoadProfilesAsync().ConfigureAwait(true);
            await LoadServersAsync(forceRefresh: false).ConfigureAwait(true);
            if (Profiles.Count == 0)
            {
                NewProfile();
            }

            SetStatusMessage("Profile_Deleted");
        }
        catch (Exception exception)
        {
            LogFailure(
                "profile.delete",
                "The account profile could not be deleted.",
                exception,
                ("profileId", profile.Model.Id.ToString()),
                ("login", profile.Model.UserName));
            SetStatusMessage("Profile_DeleteFailed");
            CatalogStatusBrush = ErrorBrush;
        }
    }

    private void ResetPendingAvatarSelection()
    {
        _pendingAvatarSourcePath = null;
        _removePendingAvatar = false;
        OnPropertyChanged(nameof(HasPendingAvatarSelection));
    }

    private void NewProfile()
    {
        IsWorkspaceVisible = false;
        SelectedProfile = null;
        ProfileLabel = string.Empty;
        LoginHint = string.Empty;
        PendingPassword = string.Empty;
        RememberPassword = false;
        ResetPendingAvatarSelection();
        _pendingServerId = null;
        SelectedServer = null;
        IsProfileEditorVisible = true;
        SetStatusMessage("Profile_NewMessage");
    }

    private void AddAccount()
    {
        NewProfile();
    }

    private void EditProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        ResetPendingAvatarSelection();
        IsProfileEditorVisible = true;
        SetStatusMessage("Profile_EditMessage");
    }

    private void CancelProfileEdit()
    {
        if (SelectedProfile is null)
        {
            SelectedProfile = Profiles.FirstOrDefault();
        }
        else
        {
            ApplySelectedProfile();
        }

        IsProfileEditorVisible = false;
        ResetPendingAvatarSelection();
        SetStatusMessage("Profile_EditCancelled");
    }

    private async Task PersistSelectedProfileAsync(Guid? profileId)
    {
        try
        {
            await _settingsService
                .SaveLastSelectedProfileAsync(profileId)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogFailure(
                "settings.selected_profile",
                "The last selected profile could not be saved.",
                exception,
                ("profileId", profileId?.ToString()));
        }
    }

    private void ApplySelectedProfile()
    {
        AccountProfile? profile = SelectedProfile?.Model;
        if (profile is null)
        {
            _isCredentialStateLoading = false;
            HasSavedCredential = false;
            RememberPassword = false;
            NotifyGameReadiness();
            return;
        }

        ProfileLabel = profile.DisplayName;
        LoginHint = profile.UserName;
        PendingPassword = string.Empty;
        RememberPassword = false;
        HasSavedCredential = false;
        _pendingServerId = ServerCatalogPresentation.ResolveLastPlayedServerId(
            profile,
            profile.PlatformId);

        PlatformItemViewModel? platform = Platforms.FirstOrDefault(item => item.Id == profile.PlatformId);
        if (platform is not null && platform != SelectedPlatform)
        {
            SelectedPlatform = platform;
        }
        else
        {
            BeginCredentialStateLoad(profile);
            RestoreServerSelection(catalog: null);
        }
    }

    private void BeginCredentialStateLoad(AccountProfile profile)
    {
        HasSavedCredential = false;
        RememberPassword = false;
        _isCredentialStateLoading = true;
        NotifyGameReadiness();
        _ = LoadSavedCredentialStateAsync(profile);
    }

    private bool IsSelectedProfileIdentity(AccountProfile profile) =>
        SelectedProfile?.Model.Id == profile.Id &&
        ProfilePlatformCompatibility.ShareAccountIdentity(
            profile.PlatformId,
            SelectedPlatform.Id) &&
        string.Equals(profile.UserName, LoginHint.Trim(), StringComparison.OrdinalIgnoreCase);

    private async Task LoadSavedCredentialStateAsync(AccountProfile profile)
    {
        try
        {
            bool hasCredential = await _profileStorage
                .HasSavedCredentialAsync(profile)
                .ConfigureAwait(true);
            if (IsSelectedProfileIdentity(profile) && string.IsNullOrEmpty(PendingPassword))
            {
                HasSavedCredential = hasCredential;
                RememberPassword = hasCredential;
            }
        }
        catch (Exception)
        {
            if (IsSelectedProfileIdentity(profile))
            {
                HasSavedCredential = false;
                RememberPassword = false;
            }
        }
        finally
        {
            if (IsSelectedProfileIdentity(profile))
            {
                _isCredentialStateLoading = false;
                NotifyGameReadiness();
            }
        }
    }

    private bool IsProfileVisibleInCurrentFilter(ProfileItemViewModel profile) =>
        profile.MatchesSearch(_profileSearchText);

    private void NotifyFilteredProfiles() =>
        OnPropertyChanged(nameof(FilteredProfiles));
}
