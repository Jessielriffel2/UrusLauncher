using System.Globalization;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.Oas;

/// <summary>
/// Retrieves OAS server metadata and transparently falls back to cached data.
/// </summary>
public sealed class OasServerDirectory : IServerDirectory
{
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(10);
    public const int DefaultMaxResponseBytes = 8 * 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly IServerCatalogCache? _cache;
    private readonly TimeSpan _requestTimeout;
    private readonly TimeProvider _timeProvider;
    private readonly int _maxResponseBytes;

    public OasServerDirectory(
        HttpClient httpClient,
        IServerCatalogCache? cache = null,
        TimeSpan? requestTimeout = null,
        TimeProvider? timeProvider = null,
        int maxResponseBytes = DefaultMaxResponseBytes)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
        _cache = cache;
        _requestTimeout = requestTimeout ?? DefaultRequestTimeout;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _maxResponseBytes = maxResponseBytes;

        if (_requestTimeout != Timeout.InfiniteTimeSpan && _requestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestTimeout),
                "The request timeout must be positive or infinite.");
        }

        if (_maxResponseBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResponseBytes));
        }
    }

    public async Task<ServerCatalog> GetServersAsync(
        PlatformDefinition platform,
        long userId = 0,
        CancellationToken cancellationToken = default)
    {
        ValidatePlatform(platform);
        if (userId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        try
        {
            var catalog = await FetchAsync(platform, userId, cancellationToken).ConfigureAwait(false);
            await TryUpdateCacheAsync(catalog, cancellationToken).ConfigureAwait(false);
            return catalog;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            var cached = await TryReadCacheAsync(platform.Id, cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                return cached.AsCached();
            }

            throw new OasServerDirectoryException(
                $"Unable to retrieve the server catalog for platform '{platform.Id}'.",
                exception);
        }
    }

    private async Task<ServerCatalog> FetchAsync(
        PlatformDefinition platform,
        long userId,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_requestTimeout != Timeout.InfiniteTimeSpan)
        {
            timeoutSource.CancelAfter(_requestTimeout);
        }

        var requestUri = BuildRequestUri(platform, userId);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        ValidateResponseOrigin(requestUri, response);
        if (response.Content.Headers.ContentLength > _maxResponseBytes)
        {
            throw new OasServerDirectoryException("The OAS server catalog exceeds the response limit.");
        }

        await response.Content
            .LoadIntoBufferAsync(_maxResponseBytes, timeoutSource.Token)
            .ConfigureAwait(false);

        await using var content = await response.Content
            .ReadAsStreamAsync(timeoutSource.Token)
            .ConfigureAwait(false);

        return await OasServerPayloadParser
            .ParseAsync(
                content,
                platform.Id,
                _timeProvider.GetUtcNow(),
                timeoutSource.Token)
            .ConfigureAwait(false);
    }

    private async Task TryUpdateCacheAsync(
        ServerCatalog catalog,
        CancellationToken cancellationToken)
    {
        if (_cache is null)
        {
            return;
        }

        try
        {
            await _cache
                .SetAsync(RemoveAccountHistory(catalog), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsRecoverableCacheFailure(exception))
        {
            // A successful network result remains useful even if its cache cannot be refreshed.
        }
    }

    private async Task<ServerCatalog?> TryReadCacheAsync(
        string platformId,
        CancellationToken cancellationToken)
    {
        if (_cache is null)
        {
            return null;
        }

        try
        {
            ServerCatalog? cached = await _cache
                .GetAsync(platformId, cancellationToken)
                .ConfigureAwait(false);
            return cached is null ? null : RemoveAccountHistory(cached);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsRecoverableCacheFailure(exception))
        {
            return null;
        }
    }

    private static Uri BuildRequestUri(PlatformDefinition platform, long userId)
    {
        var builder = new UriBuilder(platform.ServerListEndpoint);
        var queryPrefix = string.IsNullOrWhiteSpace(builder.Query)
            ? string.Empty
            : $"{builder.Query.TrimStart('?')}&";

        builder.Query = string.Create(
            CultureInfo.InvariantCulture,
            $"{queryPrefix}gamecode={Uri.EscapeDataString(platform.GameCode)}&uid={userId}");

        return builder.Uri;
    }

    private static void ValidatePlatform(PlatformDefinition platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        PlatformDefinition? knownPlatform = OasPlatformCatalog.Find(platform.Id);
        if (knownPlatform is null ||
            !string.Equals(platform.GameCode, knownPlatform.GameCode, StringComparison.OrdinalIgnoreCase) ||
            !Uri.Equals(platform.ServerListEndpoint, knownPlatform.ServerListEndpoint))
        {
            throw new ArgumentException(
                "The platform is outside the verified OAS server-directory catalog.",
                nameof(platform));
        }

        if (string.IsNullOrWhiteSpace(platform.Id) || string.IsNullOrWhiteSpace(platform.GameCode))
        {
            throw new ArgumentException("Platform ID and game code are required.", nameof(platform));
        }
    }

    private static void ValidateResponseOrigin(
        Uri requestedUri,
        HttpResponseMessage response)
    {
        Uri? effectiveUri = response.RequestMessage?.RequestUri;
        if (effectiveUri is null || !Uri.Equals(effectiveUri, requestedUri))
        {
            throw new OasServerDirectoryException(
                "The OAS server catalog response came from an unexpected origin.");
        }
    }

    private static ServerCatalog RemoveAccountHistory(ServerCatalog catalog) =>
        catalog with
        {
            Played = Array.Empty<GameServer>(),
            Current = null,
        };

    private static bool IsRecoverable(Exception exception) =>
        exception is HttpRequestException or OasServerDirectoryException or FormatException ||
        exception is IOException or OperationCanceledException;

    private static bool IsRecoverableCacheFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidOperationException ||
        exception is System.Text.Json.JsonException or NotSupportedException;
}
