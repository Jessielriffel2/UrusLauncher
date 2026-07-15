using System.IO;
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
            Profiles.Add(new ProfileItemViewModel(profile));
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

        if (SelectedProfile is null)
        {
            IsProfileEditorVisible = true;
        }
    }

    private void ReplaceProfile(ProfileItemViewModel original, AccountProfile updated)
    {
        int index = Profiles.IndexOf(original);
        if (index < 0)
        {
            return;
        }

        var replacement = new ProfileItemViewModel(updated);
        Profiles[index] = replacement;
        if (ReferenceEquals(_selectedProfile, original))
        {
            _selectedProfile = replacement;
            OnPropertyChanged(nameof(SelectedProfile));
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
            var input = new ProfileSaveInput(
                SelectedProfile?.Model,
                displayName,
                SelectedPlatform.Id,
                userName,
                PendingPassword,
                RememberPassword);
            ProfileSaveOutcome outcome = await _profileStorage
                .SaveAsync(input)
                .ConfigureAwait(true);
            PendingPassword = string.Empty;
            await LoadProfilesAsync(outcome.Profile.Id).ConfigureAwait(true);
            await LoadServersAsync(forceRefresh: false).ConfigureAwait(true);
            IsProfileEditorVisible = false;
            SetStatusMessage(outcome.WasCredentialPersisted
                ? "Profile_SavedWithCredential"
                : "Profile_SavedWithoutCredential");
        }
        catch (Exception)
        {
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
        catch (Exception)
        {
            SetStatusMessage("Profile_DeleteFailed");
            CatalogStatusBrush = ErrorBrush;
        }
    }

    private void NewProfile()
    {
        IsWorkspaceVisible = false;
        SelectedProfile = null;
        ProfileLabel = string.Empty;
        LoginHint = string.Empty;
        PendingPassword = string.Empty;
        RememberPassword = false;
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
            // The current selection remains valid for this run.
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
        string.Equals(profile.PlatformId, SelectedPlatform.Id, StringComparison.OrdinalIgnoreCase) &&
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
}
