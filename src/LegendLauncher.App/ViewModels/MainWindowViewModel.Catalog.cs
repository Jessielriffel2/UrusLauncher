using LegendLauncher.App.Services;
using LegendLauncher.Core.Models;

namespace LegendLauncher.App.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private bool _isCatalogFallbackAvailable;
    private string _manualServerId = string.Empty;

    /// <summary>
    /// True only while the remote catalog is unavailable, which is when the saved and manual
    /// server affordances are offered. A healthy catalog keeps them hidden.
    /// </summary>
    public bool IsCatalogFallbackAvailable
    {
        get => _isCatalogFallbackAvailable;
        private set
        {
            if (SetProperty(ref _isCatalogFallbackAvailable, value) && !value)
            {
                // Do not leave a stale number behind for the next outage to re-offer.
                ManualServerId = string.Empty;
            }
        }
    }

    public string ManualServerId
    {
        get => _manualServerId;
        set => SetProperty(ref _manualServerId, value);
    }

    private async Task LoadServersAsync(bool forceRefresh)
    {
        if (_disposed)
        {
            return;
        }

        _catalogCancellation?.Cancel();
        _catalogCancellation?.Dispose();
        var catalogCancellation = new CancellationTokenSource();
        _catalogCancellation = catalogCancellation;
        CancellationToken cancellationToken = catalogCancellation.Token;
        PlatformItemViewModel requestedPlatform = SelectedPlatform;
        Guid? requestedProfileId = SelectedProfile?.Model.Id;
        string? requestedLastServerId = ServerCatalogPresentation.ResolveLastPlayedServerId(
            SelectedProfile?.Model,
            requestedPlatform.Id);
        long requestedUserId = SelectedProfile?.Model
            .GetProviderUserId(requestedPlatform.Id) ?? 0;

        IsLoading = true;
        SetCatalogStatus(forceRefresh ? "Catalog_Updating" : "Catalog_Consulting");
        CatalogStatusBrush = WarningBrush;
        SetStatusMessage("Catalog_LoadingServers", requestedPlatform.DisplayName);

        try
        {
            ServerCatalog catalog = await _serverDirectory
                .GetServersAsync(
                    requestedPlatform.Model,
                    requestedUserId,
                    cancellationToken)
                .ConfigureAwait(true);

            if (!IsCurrentCatalogRequest(
                    catalogCancellation,
                    requestedPlatform,
                    requestedProfileId,
                    requestedUserId) ||
                cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _allServers.Clear();
            _allServers.AddRange(ServerCatalogPresentation.BuildRows(
                catalog,
                _timeProvider.GetUtcNow(),
                requestedLastServerId,
                _localization));

            RefreshRecentServers();
            ApplyServerFilter();
            RestoreServerSelection(catalog);
            IsCatalogFallbackAvailable = false;
            SetCatalogStatus(catalog.IsFromCache ? "Catalog_Cached" : "Catalog_Online");
            CatalogStatusBrush = catalog.IsFromCache ? WarningBrush : OnlineBrush;
            SetStatusMessage(
                catalog.IsFromCache ? "Catalog_ShowingCache" : "Catalog_UpdatedAt",
                catalog.RetrievedAtUtc.ToLocalTime());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            if (!IsCurrentCatalogRequest(
                    catalogCancellation,
                    requestedPlatform,
                    requestedProfileId,
                    requestedUserId))
            {
                return;
            }

            _allServers.Clear();
            int savedServerCount = ShowSavedServerFallback(requestedPlatform, requestedLastServerId);
            IsCatalogFallbackAvailable = true;
            RefreshRecentServers();
            ApplyServerFilter();
            RestoreServerSelection(catalog: null);
            System.Diagnostics.Debug.WriteLine($"Catalog loading failed: {exception}");
            LogFailure(
                "catalog.load",
                "The server catalog could not be loaded.",
                exception,
                ("platformId", requestedPlatform.Id),
                ("platformName", requestedPlatform.DisplayName),
                ("profileId", requestedProfileId?.ToString()),
                ("providerUserId", requestedUserId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("forceRefresh", forceRefresh.ToString()));
            SetCatalogStatus("Catalog_Unavailable");
            CatalogStatusBrush = ErrorBrush;
            SetStatusMessage(savedServerCount > 0 ? "Catalog_UsingSavedServer" : "Catalog_LoadFailed");
        }
        finally
        {
            if (ReferenceEquals(_catalogCancellation, catalogCancellation))
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Offers the servers the selected profile already used when the remote catalog is
    /// unavailable, so a catalog outage does not strand an account that has a last target.
    /// Returns how many entries became visible.
    /// </summary>
    private int ShowSavedServerFallback(
        PlatformItemViewModel platform,
        string? lastServerId)
    {
        AccountProfile? profile = SelectedProfile?.Model;
        IReadOnlyList<GameServer> savedServers = profile is null
            ? []
            : CatalogFallbackServers.FromSavedIds(
                platform.Model,
                EnumerateRecentServerIds(profile, platform.Id));

        if (savedServers.Count == 0)
        {
            return 0;
        }

        _allServers.AddRange(ServerCatalogPresentation.BuildRows(
            BuildFallbackCatalog(platform.Id, savedServers),
            _timeProvider.GetUtcNow(),
            lastServerId,
            _localization));
        return savedServers.Count;
    }

    /// <summary>
    /// Turns a server identifier typed while the catalog is unavailable into a playable row.
    /// Invalid input is ignored so the field keeps whatever the person typed.
    /// </summary>
    private void UseManualServer()
    {
        IReadOnlyList<GameServer> servers = CatalogFallbackServers.FromSavedIds(
            SelectedPlatform.Model,
            [ManualServerId]);

        if (servers.Count == 0)
        {
            return;
        }

        _allServers.Clear();
        _allServers.AddRange(ServerCatalogPresentation.BuildRows(
            BuildFallbackCatalog(SelectedPlatform.Id, servers),
            _timeProvider.GetUtcNow(),
            servers[0].Id,
            _localization));
        SearchText = string.Empty;
        RefreshRecentServers();
        ApplyServerFilter();
        RestoreServerSelection(catalog: null);
        SetStatusMessage("Catalog_UsingManualServer");
    }

    private ServerCatalog BuildFallbackCatalog(
        string platformId,
        IReadOnlyList<GameServer> servers) =>
        new(
            platformId,
            servers,
            Played: [],
            Current: null,
            _timeProvider.GetUtcNow(),
            ServerCatalogSource.Cache);

    private void ApplyServerFilter()
    {
        VisibleServers = ServerCatalogPresentation.Filter(_allServers, SearchText.Trim());
        if (SelectedServer is not null && !VisibleServers.Contains(SelectedServer))
        {
            SelectedServer = null;
        }

        if (SelectedServer is null && VisibleServers.Count > 0)
        {
            RestoreServerSelection(catalog: null);
        }
    }

    private void RestoreServerSelection(ServerCatalog? catalog)
    {
        string? profileServerId = ServerCatalogPresentation.ResolveLastPlayedServerId(
            SelectedProfile?.Model,
            SelectedPlatform.Id);
        string? desiredId = _pendingServerId ?? profileServerId ?? catalog?.Current?.Id;
        _pendingServerId = null;
        SelectedServer = ServerCatalogPresentation.Choose(VisibleServers, desiredId);
    }

    private void RefreshRecentServers()
    {
        AccountProfile? profile = SelectedProfile?.Model;
        if (profile is null)
        {
            RecentServers = [];
            return;
        }

        var resolved = new List<ServerRowViewModel>(capacity: 5);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string serverId in EnumerateRecentServerIds(profile, SelectedPlatform.Id))
        {
            if (string.IsNullOrWhiteSpace(serverId) || !seen.Add(serverId.Trim()))
            {
                continue;
            }

            ServerRowViewModel? server = _allServers.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, serverId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (server is null)
            {
                continue;
            }

            resolved.Add(server);
            if (resolved.Count == 5)
            {
                break;
            }
        }

        RecentServers = resolved;
    }

    internal void SyncRecentServers() => RefreshRecentServers();

    private static IEnumerable<string> EnumerateRecentServerIds(
        AccountProfile profile,
        string selectedPlatformId)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string serverId in profile.GetRecentServerIds(selectedPlatformId)
            .Concat(profile.GetRecentServerIds(profile.PlatformId))
            .Concat(profile.RecentServerIds)
            .Append(profile.LastServerId ?? string.Empty))
        {
            if (string.IsNullOrWhiteSpace(serverId))
            {
                continue;
            }

            string trimmed = serverId.Trim();
            if (seen.Add(trimmed))
            {
                yield return trimmed;
            }
        }
    }

    private void SelectRecentServer(ServerRowViewModel? server)
    {
        if (server?.CanLaunch != true || !RecentServers.Contains(server))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            SearchText = string.Empty;
        }

        SelectedServer = server;
    }

    private bool IsCurrentCatalogRequest(
        CancellationTokenSource cancellation,
        PlatformItemViewModel platform,
        Guid? profileId,
        long userId)
    {
        AccountProfile? selectedProfile = SelectedProfile?.Model;
        long currentUserId = selectedProfile?.GetProviderUserId(platform.Id) ?? 0;
        return ReferenceEquals(_catalogCancellation, cancellation) &&
            ReferenceEquals(SelectedPlatform, platform) &&
            SelectedProfile?.Model.Id == profileId &&
            currentUserId == userId;
    }
}
