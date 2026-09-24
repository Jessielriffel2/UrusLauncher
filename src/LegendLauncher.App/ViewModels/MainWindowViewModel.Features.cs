using LegendLauncher.App.Updates;

namespace LegendLauncher.App.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private IReadOnlyList<ReleaseCatalogEntry> _releaseCatalog = [];
    private bool _isFeatureCatalogOpen;

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

    public string FeatureCatalogCurrentVersionText =>
        $"v{_currentVersion.ToString(3)}";

    public string FeatureCatalogMacroGuideText =>
        _localization.Get("FeatureCatalog_MacroGuide");

    public string FeatureCatalogHistoryText =>
        ReleaseCatalog.FormatHistory(_releaseCatalog, _localization.LanguageCode);

    private void InitializeFeatureCatalog()
    {
        _releaseCatalog = ReleaseCatalog.Load();
        OpenFeatureCatalogCommand = new RelayCommand(
            () => IsFeatureCatalogOpen = true);
        CloseFeatureCatalogCommand = new RelayCommand(
            () => IsFeatureCatalogOpen = false,
            () => IsFeatureCatalogOpen);
        RefreshFeatureCatalogProperties();
    }

    private void RefreshFeatureCatalogProperties()
    {
        OnPropertyChanged(nameof(FeatureCatalogCurrentVersionText));
        OnPropertyChanged(nameof(FeatureCatalogMacroGuideText));
        OnPropertyChanged(nameof(FeatureCatalogHistoryText));
    }
}
