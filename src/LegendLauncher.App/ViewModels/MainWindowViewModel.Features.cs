using System.IO;
using LegendLauncher.App.Updates;

namespace LegendLauncher.App.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private IReadOnlyList<ReleaseCatalogEntry> _releaseCatalog = [];
    private bool _isFeatureCatalogOpen;
    private bool _featureCatalogUnread = true;
    private bool _featureCatalogUpdateAcknowledged;
    private bool _featureCatalogWasOpened;

    public RelayCommand OpenFeatureCatalogCommand { get; private set; } = null!;

    public RelayCommand CloseFeatureCatalogCommand { get; private set; } = null!;

    public bool IsFeatureCatalogOpen
    {
        get => _isFeatureCatalogOpen;
        set
        {
            if (SetProperty(ref _isFeatureCatalogOpen, value))
            {
                CloseFeatureCatalogCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsFeatureCatalogUnread =>
        _featureCatalogUnread ||
        IsUpdateReadyToInstall && !_featureCatalogUpdateAcknowledged;

    public string FeatureCatalogCurrentVersionText =>
        $"v{_currentVersion.ToString(3)}";

    public string FeatureCatalogUnreadBadgeText =>
        _localization.Get("FeatureCatalog_UnreadBadge");

    public string FeatureCatalogMacroGuideText =>
        _localization.Get("FeatureCatalog_MacroGuide");

    public string FeatureCatalogHistoryText =>
        ReleaseCatalog.FormatHistory(_releaseCatalog, _localization.LanguageCode);

    private void InitializeFeatureCatalog()
    {
        _releaseCatalog = ReleaseCatalog.Load();
        OpenFeatureCatalogCommand = new RelayCommand(OpenFeatureCatalog);
        CloseFeatureCatalogCommand = new RelayCommand(
            () => IsFeatureCatalogOpen = false,
            () => IsFeatureCatalogOpen);
        RefreshFeatureCatalogProperties();
    }

    private void OpenFeatureCatalog()
    {
        _featureCatalogWasOpened = true;
        _featureCatalogUnread = false;
        _featureCatalogUpdateAcknowledged = true;
        IsFeatureCatalogOpen = true;
        OnPropertyChanged(nameof(IsFeatureCatalogUnread));
        _ = PersistFeatureCatalogSeenVersionAsync();
    }

    private void ApplyFeatureCatalogSeenVersion(string? seenVersion)
    {
        if (_featureCatalogWasOpened)
        {
            _featureCatalogUnread = false;
            OnPropertyChanged(nameof(IsFeatureCatalogUnread));
            return;
        }

        _featureCatalogUnread = !IsCurrentVersion(seenVersion);
        OnPropertyChanged(nameof(IsFeatureCatalogUnread));
    }

    private async Task PersistFeatureCatalogSeenVersionAsync()
    {
        try
        {
            await _settingsService
                .SaveFeatureCatalogSeenVersionAsync(_currentVersion.ToString(3))
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            LogFailure(
                "features.seen_persist",
                "The current feature catalog could not be marked as seen.",
                exception,
                ("version", _currentVersion.ToString(3)));
        }
    }

    private bool IsCurrentVersion(string? seenVersion)
    {
        if (string.IsNullOrWhiteSpace(seenVersion))
        {
            return false;
        }

        string normalized = seenVersion.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        Version normalizedCurrent = new(
            _currentVersion.Major,
            _currentVersion.Minor,
            Math.Max(_currentVersion.Build, 0));
        return Version.TryParse(normalized, out Version? parsed) && parsed == normalizedCurrent;
    }

    private void RefreshFeatureCatalogProperties()
    {
        OnPropertyChanged(nameof(FeatureCatalogCurrentVersionText));
        OnPropertyChanged(nameof(FeatureCatalogUnreadBadgeText));
        OnPropertyChanged(nameof(IsFeatureCatalogUnread));
        OnPropertyChanged(nameof(FeatureCatalogMacroGuideText));
        OnPropertyChanged(nameof(FeatureCatalogHistoryText));
    }
}
