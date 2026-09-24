using System.Text.Json;
using LegendLauncher.App.MacroAssistant;
using LegendLauncher.Infrastructure.Persistence;

namespace LegendLauncher.App.Services;

internal sealed class ProfilePreferencesStore
{
    private readonly AtomicJsonFileStore<ProfilePreferencesDocument> _store;

    public ProfilePreferencesStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _store = new AtomicJsonFileStore<ProfilePreferencesDocument>(filePath);
    }

    public async Task<ProfileMacroModes> LoadAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty)
        {
            return ProfileMacroModes.Empty;
        }

        try
        {
            ProfilePreferencesDocument? document = await _store
                .ReadAsync(cancellationToken)
                .ConfigureAwait(false);
            return document?.Profiles is { } profiles &&
                   profiles.TryGetValue(profileId, out ProfileMacroModes? modes)
                ? modes
                : ProfileMacroModes.Empty;
        }
        catch (JsonException)
        {
            return ProfileMacroModes.Empty;
        }
    }

    public async Task SaveAsync(
        Guid profileId,
        MacroMode mode,
        MacroProfilePreferences preferences,
        CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty)
        {
            return;
        }

        MacroProfilePreferences normalized = preferences.Normalized();
        try
        {
            await _store.UpdateAsync(
                current =>
                {
                    Dictionary<Guid, ProfileMacroModes> profiles = current?.Profiles is { } existing
                        ? new Dictionary<Guid, ProfileMacroModes>(existing)
                        : [];
                    ProfileMacroModes modes = profiles.TryGetValue(profileId, out ProfileMacroModes? currentModes) &&
                                             currentModes is not null
                        ? currentModes
                        : ProfileMacroModes.Empty;
                    profiles[profileId] = modes.With(mode, normalized);
                    return new ProfilePreferencesDocument(profiles);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            var profiles = new Dictionary<Guid, ProfileMacroModes>
            {
                [profileId] = ProfileMacroModes.Empty.With(mode, normalized),
            };
            await _store
                .WriteAsync(new ProfilePreferencesDocument(profiles), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task DeleteAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _store.UpdateAsync(
                current =>
                {
                    if (current?.Profiles is not { } existing || !existing.ContainsKey(profileId))
                    {
                        return current ?? ProfilePreferencesDocument.Default;
                    }

                    var profiles = new Dictionary<Guid, ProfileMacroModes>(existing);
                    profiles.Remove(profileId);
                    return new ProfilePreferencesDocument(profiles);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            await _store
                .WriteAsync(ProfilePreferencesDocument.Default, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public static string ModeKey(MacroMode mode) => mode switch
    {
        MacroMode.Cosmo => ProfileMacroPreferencesStoreKeys.Cosmo,
        MacroMode.Clicks => ProfileMacroPreferencesStoreKeys.Clicks,
        _ => ProfileMacroPreferencesStoreKeys.Gems,
    };

    private sealed record ProfilePreferencesDocument(
        Dictionary<Guid, ProfileMacroModes> Profiles)
    {
        public static ProfilePreferencesDocument Default { get; } =
            new(new Dictionary<Guid, ProfileMacroModes>());
    }
}
