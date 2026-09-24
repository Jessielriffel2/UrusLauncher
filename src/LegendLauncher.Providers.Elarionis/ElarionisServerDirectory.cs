using System.Text.Json;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.Elarionis;

/// <summary>
/// Reads the public Elarionis shard catalog without sharing authentication state.
/// The shards endpoint requires no token; per-account characters are resolved
/// separately during authentication.
/// </summary>
public sealed class ElarionisServerDirectory : IServerDirectory
{
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(12);
    public const int DefaultMaxResponseBytes = 2 * 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly IServerCatalogCache? _cache;
    private readonly TimeSpan _requestTimeout;
    private readonly TimeProvider _timeProvider;
    private readonly int _maxResponseBytes;
    private readonly Action<Exception, string>? _onRecoverableFailure;

    public ElarionisServerDirectory(
        HttpClient httpClient,
        IServerCatalogCache? cache = null,
        TimeSpan? requestTimeout = null,
        TimeProvider? timeProvider = null,
        int maxResponseBytes = DefaultMaxResponseBytes,
        Action<Exception, string>? onRecoverableFailure = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cache = cache;
        _requestTimeout = requestTimeout ?? DefaultRequestTimeout;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _maxResponseBytes = maxResponseBytes;
        _onRecoverableFailure = onRecoverableFailure;
        if (_requestTimeout != Timeout.InfiniteTimeSpan && _requestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(requestTimeout));
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
            ServerCatalog catalog = await FetchAsync(platform, cancellationToken).ConfigureAwait(false);
            await TryUpdateCacheAsync(catalog, cancellationToken).ConfigureAwait(false);
            return catalog;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            _onRecoverableFailure?.Invoke(exception, platform.Id);
            ServerCatalog? cached = await TryReadCacheAsync(platform.Id, cancellationToken)
                .ConfigureAwait(false);
            if (cached is not null)
            {
                return cached.AsCached();
            }

            throw new ElarionisServerDirectoryException(
                $"Unable to retrieve the Elarionis catalog for platform '{platform.Id}'.",
                exception);
        }
    }

    private async Task<ServerCatalog> FetchAsync(
        PlatformDefinition platform,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_requestTimeout != Timeout.InfiniteTimeSpan)
        {
            timeoutSource.CancelAfter(_requestTimeout);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, platform.ServerListEndpoint);
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        ValidateResponseOrigin(platform.ServerListEndpoint, response);
        if (response.Content.Headers.ContentLength > _maxResponseBytes)
        {
            throw new ElarionisServerDirectoryException(
                "The Elarionis server catalog exceeds the response limit.");
        }

        await response.Content.LoadIntoBufferAsync(_maxResponseBytes, timeoutSource.Token)
            .ConfigureAwait(false);
        await using Stream content = await response.Content.ReadAsStreamAsync(timeoutSource.Token)
            .ConfigureAwait(false);
        return await ElarionisShardPayloadParser.ParseAsync(
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
            await _cache.SetAsync(catalog, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsRecoverableCacheFailure(exception))
        {
            // The remote result is still useful when its optional cache cannot be refreshed.
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
            return await _cache.GetAsync(platformId, cancellationToken).ConfigureAwait(false);
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

    private static void ValidatePlatform(PlatformDefinition platform)
    {
        ArgumentNullException.ThrowIfNull(platform);
        PlatformDefinition? known = ElarionisPlatformCatalog.Find(platform.Id);
        if (known is null || known != platform)
        {
            throw new ArgumentException(
                "The platform is outside the verified Elarionis catalog.",
                nameof(platform));
        }
    }

    private static void ValidateResponseOrigin(Uri requestedUri, HttpResponseMessage response)
    {
        Uri? effectiveUri = response.RequestMessage?.RequestUri;
        if (effectiveUri is null || !Uri.Equals(effectiveUri, requestedUri))
        {
            throw new ElarionisServerDirectoryException(
                "The Elarionis server catalog response came from an unexpected origin.");
        }
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is HttpRequestException or ElarionisServerDirectoryException or IOException ||
        exception is OperationCanceledException;

    private static bool IsRecoverableCacheFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidOperationException ||
        exception is JsonException or NotSupportedException;
}
