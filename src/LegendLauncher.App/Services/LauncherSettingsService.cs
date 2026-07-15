using LegendLauncher.Infrastructure.Persistence;
using LegendLauncher.App.Localization;
using System.Text.Json;

namespace LegendLauncher.App.Services;

internal sealed record LauncherSettingsSnapshot(
    bool IsGameMuted,
    GameLayoutMode LayoutMode,
    Guid? LastSelectedProfileId,
    string LanguageCode,
    DateTimeOffset? LastDonationPromptUtc)
{
    public static LauncherSettingsSnapshot Default { get; } =
        new(
            true,
            GameLayoutMode.GridFour,
            null,
            LocalizationService.DefaultLanguageCode,
            null);
}

internal sealed class LauncherSettingsService
{
    private readonly AtomicJsonFileStore<LauncherSettingsDocument>? _store;
    private LauncherSettingsDocument _memoryDocument = LauncherSettingsDocument.Default;

    internal LauncherSettingsService()
    {
    }

    public LauncherSettingsService(string filePath)
    {
        _store = new AtomicJsonFileStore<LauncherSettingsDocument>(filePath);
    }

    public async Task<LauncherSettingsSnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        LauncherSettingsDocument? document;
        try
        {
            document = _store is null
                ? _memoryDocument
                : await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return LauncherSettingsSnapshot.Default;
        }
        if (document is null)
        {
            return LauncherSettingsSnapshot.Default;
        }

        GameLayoutMode layout = Enum.IsDefined(typeof(GameLayoutMode), document.LayoutMode)
            ? (GameLayoutMode)document.LayoutMode
            : GameLayoutMode.GridFour;
        return new LauncherSettingsSnapshot(
            document.IsGameMuted,
            layout,
            document.LastSelectedProfileId,
            LocalizationService.NormalizeLanguageCode(document.LanguageCode),
            document.LastDonationPromptUtc);
    }

    public Task SaveLastSelectedProfileAsync(
        Guid? profileId,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            current => current with { LastSelectedProfileId = profileId },
            cancellationToken);

    public Task SaveGamePreferencesAsync(
        bool isMuted,
        GameLayoutMode layoutMode,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            current => current with
            {
                IsGameMuted = isMuted,
                LayoutMode = (int)layoutMode,
            },
            cancellationToken);

    public Task SaveLanguageAsync(
        string languageCode,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            current => current with
            {
                LanguageCode = LocalizationService.NormalizeLanguageCode(languageCode),
            },
            cancellationToken);

    public Task SaveDonationPromptShownAsync(
        DateTimeOffset shownAtUtc,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            current => current with
            {
                LastDonationPromptUtc = shownAtUtc.ToUniversalTime(),
            },
            cancellationToken);

    private async Task UpdateAsync(
        Func<LauncherSettingsDocument, LauncherSettingsDocument> updater,
        CancellationToken cancellationToken)
    {
        if (_store is not null)
        {
            try
            {
                await _store.UpdateAsync(
                    current => updater(current ?? LauncherSettingsDocument.Default),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                await _store.WriteAsync(
                    updater(LauncherSettingsDocument.Default),
                    cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        _memoryDocument = updater(_memoryDocument);
    }

    private sealed record LauncherSettingsDocument(
        bool IsGameMuted,
        int LayoutMode,
        Guid? LastSelectedProfileId,
        string? LanguageCode,
        DateTimeOffset? LastDonationPromptUtc)
    {
        public static LauncherSettingsDocument Default { get; } =
            new(
                true,
                (int)GameLayoutMode.GridFour,
                null,
                LocalizationService.DefaultLanguageCode,
                null);
    }
}
