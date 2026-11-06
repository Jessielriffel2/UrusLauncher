using LegendLauncher.App.Services;
using LegendLauncher.Core.GameColor;
using LegendLauncher.Core.GameDisplay;
using LegendLauncher.Core.Models;

namespace LegendLauncher.App.ViewModels;

/// <summary>
/// How the game is shown, shared by every open session: drawing mode (DirectX or Compatible), color saturation (1.1.22) and
/// game zoom (1.1.25).
/// </summary>
internal sealed partial class GameWorkspaceViewModel
{
    // Drawing mode: read by SessionLaunchCoordinator when a session opens (Flash takes WMode before loading the game), so a change
    // only reaches the sessions opened after it.
    private GameWindowMode _windowMode = LauncherSettingsSnapshot.Default.WindowMode;
    private bool _hasWindowModeNotice;
    // Color saturation slider (1.1.22): one value for every open session, read by each GameHost on every frame.
    private readonly GameColorChannel.Writer _colorChannel = new();
    private int _colorSaturation = GameColorChannel.Original;
    private CancellationTokenSource? _colorSaveDelay;
    // Game zoom (1.1.25): one choice for every open session; each GameHost computes its own scale from its area.
    private readonly GameZoom.Writer _zoomChannel = new();
    private int _gameZoom = GameZoom.Automatic;
    private IReadOnlyList<GameZoomOption> _zoomOptions = [];

    /// <summary>
    /// How Flash draws the sessions opened from now on: <see cref="GameWindowMode.Direct"/> (DirectX button on: smoother, no color
    /// filter or zoom) or <see cref="GameWindowMode.Opaque"/> (Compatible). Saved at once; open sessions keep the mode they opened with.
    /// </summary>
    public GameWindowMode WindowMode
    {
        get => _windowMode;
        set
        {
            if (!Enum.IsDefined(value) || !SetProperty(ref _windowMode, value))
            {
                return;
            }

            // open sessions keep the mode they opened with: say so until the next session opens
            HasWindowModeNotice = HasSessions;
            OnWindowModeChanged();
            _ = PersistGamePreferencesAsync();
        }
    }

    /// <summary>The DirectX button (on = <see cref="GameWindowMode.Direct"/>, off = Compatible).</summary>
    public bool IsDirectXMode
    {
        get => _windowMode == GameWindowMode.Direct;
        set => WindowMode = value ? GameWindowMode.Direct : GameWindowMode.Opaque;
    }

    /// <summary>Colors and zoom only act in the Compatible mode; in DirectX they are greyed out.</summary>
    public bool AreCompatibleOnlyControlsEnabled => _windowMode == GameWindowMode.Opaque;

    public string WindowModeLabel => _localization.Get(
        IsDirectXMode ? "Workspace_WindowModeDirectX" : "Workspace_WindowModeCompatible");

    public string ColorSaturationToolTip => _localization.Get(
        AreCompatibleOnlyControlsEnabled ? "Workspace_ColorSaturationTooltip" : "Workspace_CompatibleOnlyTooltip");

    public string ZoomToolTip => _localization.Get(
        AreCompatibleOnlyControlsEnabled ? "Workspace_ZoomTooltip" : "Workspace_CompatibleOnlyTooltip");

    /// <summary>Footer line shown with <see cref="HasWindowModeNotice"/>: the chosen mode applies to the tabs opened from now on.</summary>
    public string WindowModeNoticeText => _localization.Format("Workspace_WindowModeNoticeFormat", WindowModeLabel);

    /// <summary>True after the mode changed with sessions open, until a new session opens (it is the one using the new mode).</summary>
    public bool HasWindowModeNotice
    {
        get => _hasWindowModeNotice;
        private set => SetProperty(ref _hasWindowModeNotice, value);
    }

    /// <summary>Zoom choices of the toolbar (Automatic, 100%..300%), labeled in the current language.</summary>
    public IReadOnlyList<GameZoomOption> ZoomOptions => _zoomOptions;

    /// <summary>
    /// Game zoom: <see cref="GameZoom.Automatic"/> (follows the Windows scale, the default) or a percentage. Applied live by every
    /// GameHost (no reload) and saved at once.
    /// </summary>
    public int GameZoomChoice
    {
        get => _gameZoom;
        set
        {
            int normalized = GameZoom.Normalize(value);
            if (!SetProperty(ref _gameZoom, normalized))
            {
                return;
            }

            _zoomChannel.Publish(normalized);
            OnPropertyChanged(nameof(SelectedZoomOption));
            _ = PersistGamePreferencesAsync();
        }
    }

    public GameZoomOption? SelectedZoomOption
    {
        get => _zoomOptions.FirstOrDefault(option => option.Value == _gameZoom);
        set
        {
            if (value is not null)
            {
                GameZoomChoice = value.Value;
            }
        }
    }

    public RelayCommand ResetColorSaturationCommand { get; }

    /// <summary>
    /// Saturation of the game colors, 0 (black and white) to 100 (original). Applied live by the GameHosts (filter on the frames
    /// Flash shows); saved a moment after the slider stops moving.
    /// </summary>
    public int ColorSaturation
    {
        get => _colorSaturation;
        set
        {
            int clamped = GameColorChannel.Clamp(value);
            if (!SetProperty(ref _colorSaturation, clamped))
            {
                return;
            }

            _colorChannel.Publish(clamped);
            OnPropertyChanged(nameof(ColorSaturationText));
            OnPropertyChanged(nameof(IsColorFilterActive));
            _ = PersistColorSaturationSoonAsync();
        }
    }

    public string ColorSaturationText => _localization.Format("Workspace_ColorSaturationValue", _colorSaturation);

    public bool IsColorFilterActive => _colorSaturation < GameColorChannel.Original;

    private void ApplyDisplaySettings(LauncherSettingsSnapshot settings)
    {
        _windowMode = Enum.IsDefined(settings.WindowMode) ? settings.WindowMode : LauncherSettingsSnapshot.Default.WindowMode;
        OnPropertyChanged(nameof(WindowMode));
        OnWindowModeChanged();
        _colorSaturation = GameColorChannel.Clamp(settings.ColorSaturation);
        _colorChannel.Publish(_colorSaturation);
        _gameZoom = GameZoom.Normalize(settings.GameZoomChoice);
        _zoomChannel.Publish(_gameZoom);
        OnPropertyChanged(nameof(GameZoomChoice));
        OnPropertyChanged(nameof(SelectedZoomOption));
        OnPropertyChanged(nameof(ColorSaturation));
        OnPropertyChanged(nameof(ColorSaturationText));
        OnPropertyChanged(nameof(IsColorFilterActive));
    }

    private void OnWindowModeChanged()
    {
        OnPropertyChanged(nameof(IsDirectXMode));
        OnPropertyChanged(nameof(AreCompatibleOnlyControlsEnabled));
        OnPropertyChanged(nameof(WindowModeLabel));
        OnPropertyChanged(nameof(WindowModeNoticeText));
        OnPropertyChanged(nameof(ColorSaturationToolTip));
        OnPropertyChanged(nameof(ZoomToolTip));
    }

    /// <summary>A new session opened: it uses the current mode, so the "next tabs" notice is done.</summary>
    private void OnSessionOpenedForDisplay() => HasWindowModeNotice = false;

    private void RefreshDisplayTexts()
    {
        OnPropertyChanged(nameof(WindowModeLabel));
        OnPropertyChanged(nameof(WindowModeNoticeText));
        OnPropertyChanged(nameof(ColorSaturationToolTip));
        OnPropertyChanged(nameof(ZoomToolTip));
        OnPropertyChanged(nameof(ColorSaturationText));
        _zoomOptions = BuildZoomOptions();
        OnPropertyChanged(nameof(ZoomOptions));
        OnPropertyChanged(nameof(SelectedZoomOption));
    }

    private void DisposeDisplay()
    {
        _colorSaveDelay?.Cancel();
        _colorSaveDelay?.Dispose();
        _colorChannel.Dispose();
        _zoomChannel.Dispose();
    }

    private IReadOnlyList<GameZoomOption> BuildZoomOptions() =>
        GameZoom.Choices
            .Select(choice => new GameZoomOption(
                choice,
                choice == GameZoom.Automatic
                    ? _localization.Get("Workspace_ZoomAutomatic")
                    : _localization.Format("Workspace_ZoomPercent", choice)))
            .ToArray();

    private async Task PersistColorSaturationSoonAsync()
    {
        _colorSaveDelay?.Cancel();
        _colorSaveDelay?.Dispose();
        var delay = new CancellationTokenSource();
        _colorSaveDelay = delay;
        try
        {
            // dragging the slider fires dozens of changes: save once it settles
            await Task.Delay(TimeSpan.FromMilliseconds(500), delay.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await PersistGamePreferencesAsync().ConfigureAwait(false);
    }
}

/// <summary>One entry of the toolbar zoom selector: the stored value (0 = automatic, else a percentage) and its label.</summary>
internal sealed record GameZoomOption(int Value, string DisplayName)
{
    // The selection box of the combo template shows the item itself (like LanguageOption).
    public override string ToString() => DisplayName;
}
