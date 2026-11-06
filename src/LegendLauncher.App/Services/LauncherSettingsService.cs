using LegendLauncher.Core.GameColor;
using LegendLauncher.Core.GameDisplay;
using LegendLauncher.Core.Models;
using LegendLauncher.Infrastructure.Persistence;
using LegendLauncher.App.Localization;
using System.Text.Json;

namespace LegendLauncher.App.Services;

internal sealed record LauncherSettingsSnapshot(
    bool IsGameMuted,
    GameLayoutMode LayoutMode,
    Guid? LastSelectedProfileId,
    string LanguageCode,
    DateTimeOffset? LastDonationPromptUtc,
    string? LastSeenFeatureCatalogVersion,
    int ColorSaturation = GameColorChannel.Original,
    int GameZoomChoice = GameZoom.Automatic,
    GameWindowMode WindowMode = GameWindowMode.Direct)
{
    public static LauncherSettingsSnapshot Default { get; } =
        new(
            true,
            GameLayoutMode.GridFour,
            null,
            LocalizationService.DefaultLanguageCode,
            null,
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
        // missing in files written before 1.1.22: original colors, never black and white
        int colorSaturation = GameColorChannel.Clamp(document.ColorSaturation ?? GameColorChannel.Original);
        // missing in files written before 1.1.25: automatic (follows the Windows scale)
        int gameZoom = GameZoom.Normalize(document.GameZoom ?? GameZoom.Automatic);
        return new LauncherSettingsSnapshot(
            document.IsGameMuted,
            layout,
            document.LastSelectedProfileId,
            LocalizationService.NormalizeLanguageCode(document.LanguageCode),
            document.LastDonationPromptUtc,
            document.LastSeenFeatureCatalogVersion,
            colorSaturation,
            gameZoom,
            ResolveWindowMode(document.GameWindowMode, colorSaturation, gameZoom));
    }

    /// <summary>
    /// The saved drawing mode; with none saved yet (files written before the DirectX button), DirectX, unless the player already
    /// changed the colors or the zoom: those only act in the Compatible mode, so that player stays there and keeps the setting.
    /// </summary>
    internal static GameWindowMode ResolveWindowMode(string? savedMode, int colorSaturation, int gameZoom)
    {
        if (GameWindowModeText.TryParse(savedMode, out GameWindowMode saved))
        {
            return saved;
        }

        bool usesCompatibleOnlySettings =
            colorSaturation < GameColorChannel.Original || gameZoom != GameZoom.Automatic;
        return usesCompatibleOnlySettings ? GameWindowMode.Opaque : GameWindowMode.Direct;
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
        int? colorSaturation = null,
        int? gameZoom = null,
        GameWindowMode? windowMode = null,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            current => current with
            {
                IsGameMuted = isMuted,
                LayoutMode = (int)layoutMode,
                ColorSaturation = colorSaturation is { } saturation
                    ? GameColorChannel.Clamp(saturation)
                    : current.ColorSaturation,
                GameZoom = gameZoom is { } zoom
                    ? GameZoom.Normalize(zoom)
                    : current.GameZoom,
                GameWindowMode = windowMode is { } mode
                    ? GameWindowModeText.Format(mode)
                    : current.GameWindowMode,
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

    public Task SaveFeatureCatalogSeenVersionAsync(
        string version,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            current => current with
            {
                LastSeenFeatureCatalogVersion = version,
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
        DateTimeOffset? LastDonationPromptUtc,
        string? LastSeenFeatureCatalogVersion,
        int? ColorSaturation = null,
        int? GameZoom = null,
        // "direct" or "opaque" (GameWindowModeText); missing in files written before the DirectX button
        string? GameWindowMode = null)
    {
        public static LauncherSettingsDocument Default { get; } =
            new(
                true,
                (int)GameLayoutMode.GridFour,
                null,
                LocalizationService.DefaultLanguageCode,
                null,
                null);
    }
}
