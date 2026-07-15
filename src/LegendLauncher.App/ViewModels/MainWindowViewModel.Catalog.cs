using LegendLauncher.App.Services;
using LegendLauncher.Core.Models;

namespace LegendLauncher.App.ViewModels;

internal sealed partial class MainWindowViewModel
{
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
        long requestedUserId = SelectedProfile?.Model is { } profile &&
            string.Equals(profile.PlatformId, requestedPlatform.Id, StringComparison.OrdinalIgnoreCase)
                ? profile.ProviderUserId ?? 0
                : 0;

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
            RefreshRecentServers();
            ApplyServerFilter();
            System.Diagnostics.Debug.WriteLine($"Catalog loading failed: {exception}");
            SetCatalogStatus("Catalog_Unavailable");
            CatalogStatusBrush = ErrorBrush;
            SetStatusMessage("Catalog_LoadFailed");
        }
        finally
        {
            if (ReferenceEquals(_catalogCancellation, catalogCancellation))
            {
                IsLoading = false;
            }
        }
    }

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
        if (profile is null ||
            !string.Equals(profile.PlatformId, SelectedPlatform.Id, StringComparison.OrdinalIgnoreCase))
        {
            RecentServers = [];
            return;
        }

        var resolved = new List<ServerRowViewModel>(capacity: 5);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string serverId in profile.RecentServerIds ?? [])
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
        long currentUserId = selectedProfile is not null &&
            string.Equals(selectedProfile.PlatformId, platform.Id, StringComparison.OrdinalIgnoreCase)
                ? selectedProfile.ProviderUserId ?? 0
                : 0;
        return ReferenceEquals(_catalogCancellation, cancellation) &&
            ReferenceEquals(SelectedPlatform, platform) &&
            SelectedProfile?.Model.Id == profileId &&
            currentUserId == userId;
    }
}
