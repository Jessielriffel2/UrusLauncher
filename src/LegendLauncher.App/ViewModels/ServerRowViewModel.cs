using LegendLauncher.Core.Models;
using LegendLauncher.App.Localization;

namespace LegendLauncher.App.ViewModels;

internal sealed class ServerRowViewModel(
    GameServer model,
    DateTimeOffset now,
    bool isPreviouslyPlayed = false,
    bool isCurrent = false,
    bool isLatestReleased = false,
    LocalizationService? localization = null) : ObservableObject
{
    private readonly LocalizationService _localization =
        localization ?? LocalizationService.Current;
    private bool _showSectionDivider;

    public GameServer Model { get; } = model;

    public string Id => Model.Id;

    public string Code => string.IsNullOrWhiteSpace(Model.Code)
        ? $"S{Model.NumericId}"
        : Model.Code.ToUpperInvariant();

    public string Name => IsGeneratedFallback(Model.Name)
        ? _localization.Format("Server_GenericName", Id)
        : Model.Name;

    public string FullName => IsGeneratedFallback(Model.FullName)
        ? _localization.Format("Server_GenericName", Id)
        : Model.FullName;

    public bool IsAvailable { get; } = model.IsAvailable(now);

    public bool HasSecureLaunchAddress { get; } = IsAllowedLaunchAddress(model.LaunchUri);

    public bool CanLaunch => IsAvailable && HasSecureLaunchAddress;

    public bool IsPreviouslyPlayed { get; } = isPreviouslyPlayed;

    public bool IsCurrent { get; } = isCurrent;

    public bool IsLatestReleased { get; } = isLatestReleased;

    public bool ShowRecommendedBadge => IsCurrent;

    public bool ShowLatestBadge => IsLatestReleased;

    public bool ShowSectionDivider
    {
        get => _showSectionDivider;
        private set => SetProperty(ref _showSectionDivider, value);
    }

    public string RecommendedBadgeText => _localization.Get("Server_Recommended");

    public string RecommendedBadgeToolTip =>
        _localization.Get("Server_RecommendedTooltip");

    public string LatestBadgeText => _localization.Get("Server_Latest");

    public string LatestBadgeToolTip => _localization.Get("Server_LatestTooltip");

    public string SectionLabelText => _localization.Get("Servers_OtherServers");

    public string AvailabilityLabel => !IsAvailable
        ? Model.StartTimeUtc is { } start && start > now
            ? _localization.Format("Server_OpensAt", start.ToLocalTime())
            : _localization.Get("Server_Unavailable")
        : !HasSecureLaunchAddress
            ? _localization.Get("Server_MissingSecureAddress")
            : IsCurrent
                ? _localization.Get("Server_LastAvailable")
                : IsPreviouslyPlayed
                    ? _localization.Get("Server_PreviouslyPlayedAvailable")
                    : _localization.Get("Server_Available");

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(FullName));
        OnPropertyChanged(nameof(RecommendedBadgeText));
        OnPropertyChanged(nameof(RecommendedBadgeToolTip));
        OnPropertyChanged(nameof(LatestBadgeText));
        OnPropertyChanged(nameof(LatestBadgeToolTip));
        OnPropertyChanged(nameof(SectionLabelText));
        OnPropertyChanged(nameof(AvailabilityLabel));
    }

    internal void SetSectionDivider(bool visible) => ShowSectionDivider = visible;

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Code.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            FullName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            Model.NumericId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedLaunchAddress(Uri? uri)
    {
        if (uri is null ||
            !uri.IsAbsoluteUri ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            (!uri.IsDefaultPort && uri.Port != 443))
        {
            return false;
        }

        string host = uri.IdnHost.TrimEnd('.');
        return string.Equals(host, "creaction-network.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".creaction-network.com", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsGeneratedFallback(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        string.Equals(value.Trim(), $"Server {Id}", StringComparison.OrdinalIgnoreCase);
}
