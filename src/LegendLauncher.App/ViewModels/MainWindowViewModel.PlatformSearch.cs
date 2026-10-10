using System.Globalization;

namespace LegendLauncher.App.ViewModels;

/// <summary>
/// Search box at the top of the provider drop-down: typing narrows the list; closing the drop-down
/// clears the text and brings the full list (and the current selection) back.
/// </summary>
internal sealed partial class MainWindowViewModel
{
    private string _platformSearchText = string.Empty;
    private IReadOnlyList<PlatformItemViewModel>? _filteredPlatforms;

    public IReadOnlyList<PlatformItemViewModel> FilteredPlatforms =>
        _filteredPlatforms ?? Platforms;

    public string PlatformSearchPlaceholder => _localization.Get("Platforms_SearchPlaceholder");

    public string PlatformSearchEmptyText => _localization.Get("Platforms_SearchEmpty");

    public bool ShowPlatformSearchEmpty => FilteredPlatforms.Count == 0;

    public string PlatformSearchText
    {
        get => _platformSearchText;
        set
        {
            string normalized = value ?? string.Empty;
            if (!SetProperty(ref _platformSearchText, normalized))
            {
                return;
            }

            ApplyPlatformFilter();
        }
    }

    /// <summary>Clears the search and re-selects the current provider in the drop-down.</summary>
    internal void ResetPlatformSearch()
    {
        PlatformSearchText = string.Empty;

        // The drop-down drops its selection while the list is filtered; announce the current
        // provider again so it shows up in the selection box.
        OnPropertyChanged(nameof(SelectedPlatform));
    }

    private void ApplyPlatformFilter()
    {
        string query = _platformSearchText.Trim();
        _filteredPlatforms = query.Length == 0
            ? null
            : Array.AsReadOnly(Platforms
                .Where(platform => PlatformMatches(platform, query))
                .ToArray());

        OnPropertyChanged(nameof(FilteredPlatforms));
        OnPropertyChanged(nameof(ShowPlatformSearchEmpty));
    }

    private static bool PlatformMatches(PlatformItemViewModel platform, string query)
    {
        CompareInfo compare = CultureInfo.InvariantCulture.CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase |
            CompareOptions.IgnoreNonSpace |
            CompareOptions.IgnoreSymbols;
        return compare.IndexOf(platform.DisplayName, query, options) >= 0 ||
            compare.IndexOf(platform.Id, query, options) >= 0;
    }
}
