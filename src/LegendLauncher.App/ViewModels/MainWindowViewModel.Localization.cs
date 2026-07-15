using System.IO;
using LegendLauncher.App.Localization;

namespace LegendLauncher.App.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private readonly LocalizationService _localization;
    private LocalizedMessage _catalogStatusMessage =
        LocalizedMessage.Create("Catalog_Waiting");
    private LocalizedMessage _statusMessage =
        LocalizedMessage.Create("Catalog_Preparing");
    private LanguageOption _selectedLanguage = LocalizationService.SupportedLanguages[0];
    private bool _suppressLanguagePersistence;

    public IReadOnlyList<LanguageOption> Languages => LocalizationService.SupportedLanguages;

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null ||
                !Languages.Any(option => string.Equals(
                    option.Code,
                    value.Code,
                    StringComparison.Ordinal)) ||
                !SetProperty(ref _selectedLanguage, value))
            {
                return;
            }

            _localization.SetLanguage(value.Code);
            if (!_suppressLanguagePersistence)
            {
                _ = PersistLanguageAsync(value.Code);
            }
        }
    }

    public string CatalogStatus => _catalogStatusMessage.Resolve(_localization);

    public string StatusMessage => _statusMessage.Resolve(_localization);

    public string SelectedServerDisplayName =>
        SelectedServer?.Name ?? _localization.Get("Servers_ChooseServerFallback");

    internal LocalizationService Localization => _localization;

    private void InitializeLocalization()
    {
        _selectedLanguage = FindLanguage(_localization.LanguageCode);
        _localization.LanguageChanged += LocalizationOnLanguageChanged;
    }

    private void ApplyLanguageSettings(string languageCode)
    {
        _suppressLanguagePersistence = true;
        try
        {
            SelectedLanguage = FindLanguage(languageCode);
        }
        finally
        {
            _suppressLanguagePersistence = false;
        }
    }

    private void SetCatalogStatus(string key, params object?[] arguments)
    {
        _catalogStatusMessage = LocalizedMessage.Create(key, arguments);
        OnPropertyChanged(nameof(CatalogStatus));
    }

    private void SetStatusMessage(string key, params object?[] arguments)
    {
        _statusMessage = LocalizedMessage.Create(key, arguments);
        OnPropertyChanged(nameof(StatusMessage));
    }

    private void LocalizationOnLanguageChanged(object? sender, EventArgs eventArgs)
    {
        LanguageOption resolved = FindLanguage(_localization.LanguageCode);
        if (!ReferenceEquals(_selectedLanguage, resolved))
        {
            _selectedLanguage = resolved;
            OnPropertyChanged(nameof(SelectedLanguage));
        }

        OnPropertyChanged(nameof(CatalogStatus));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(SelectedProfileSummary));
        OnPropertyChanged(nameof(CredentialStatusText));
        OnPropertyChanged(nameof(PrimaryActionLabel));
        OnPropertyChanged(nameof(PasswordPlaceholderText));
        OnPropertyChanged(nameof(ServerCountLabel));
        OnPropertyChanged(nameof(RuntimeStatusShort));
        OnPropertyChanged(nameof(RuntimeStatusDetail));
        OnPropertyChanged(nameof(SelectedServerDisplayName));
        foreach (ServerRowViewModel server in _allServers)
        {
            server.RefreshLocalization();
        }

        RefreshUpdateProperties();
        ApplyServerFilter();
    }

    private async Task PersistLanguageAsync(string languageCode)
    {
        try
        {
            await _settingsService
                .SaveLanguageAsync(languageCode)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // Keep the selected language for this execution.
        }
    }

    private void DisposeLocalization()
    {
        _localization.LanguageChanged -= LocalizationOnLanguageChanged;
    }

    private static LanguageOption FindLanguage(string languageCode)
    {
        string normalized = LocalizationService.NormalizeLanguageCode(languageCode);
        return LocalizationService.SupportedLanguages.First(option =>
            string.Equals(option.Code, normalized, StringComparison.Ordinal));
    }
}
