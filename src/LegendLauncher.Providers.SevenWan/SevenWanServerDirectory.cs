using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;
using System.Text.Json;

namespace LegendLauncher.Providers.SevenWan;

/// <summary>
/// Reads the public 7wan Wartune catalogs without sharing authentication state.
/// </summary>
public sealed class SevenWanServerDirectory : IServerDirectory
{
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(12);
    public const int DefaultMaxResponseBytes = 2 * 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly IServerCatalogCache? _cache;
    private readonly TimeSpan _requestTimeout;
    private readonly TimeProvider _timeProvider;
    private readonly int _maxResponseBytes;

    public SevenWanServerDirectory(
        HttpClient httpClient,
        IServerCatalogCache? cache = null,
        TimeSpan? requestTimeout = null,
        TimeProvider? timeProvider = null,
        int maxResponseBytes = DefaultMaxResponseBytes)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cache = cache;
        _requestTimeout = requestTimeout ?? DefaultRequestTimeout;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _maxResponseBytes = maxResponseBytes;
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
        SevenWanPlatformVariant variant = ValidatePlatform(platform);
        if (userId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        try
        {
            ServerCatalog catalog = await FetchAsync(variant, cancellationToken).ConfigureAwait(false);
            await TryUpdateCacheAsync(catalog, cancellationToken).ConfigureAwait(false);
            return catalog;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            ServerCatalog? cached = await TryReadCacheAsync(platform.Id, cancellationToken)
                .ConfigureAwait(false);
            if (cached is not null)
            {
                return cached.AsCached();
            }

            throw new SevenWanServerDirectoryException(
                $"Unable to retrieve the 7wan catalog for platform '{platform.Id}'.",
                exception);
        }
    }

    private async Task<ServerCatalog> FetchAsync(
        SevenWanPlatformVariant variant,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_requestTimeout != Timeout.InfiniteTimeSpan)
        {
            timeoutSource.CancelAfter(_requestTimeout);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, variant.Platform.ServerListEndpoint);
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        ValidateResponseOrigin(variant.Platform.ServerListEndpoint, response);
        if (response.Content.Headers.ContentLength > _maxResponseBytes)
        {
            throw new SevenWanServerDirectoryException(
                "The 7wan server catalog exceeds the response limit.");
        }

        await response.Content.LoadIntoBufferAsync(_maxResponseBytes, timeoutSource.Token)
            .ConfigureAwait(false);
        await using Stream content = await response.Content.ReadAsStreamAsync(timeoutSource.Token)
            .ConfigureAwait(false);
        return await SevenWanServerPayloadParser.ParseAsync(
                content,
                variant,
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

    private static SevenWanPlatformVariant ValidatePlatform(PlatformDefinition platform)
    {
        ArgumentNullException.ThrowIfNull(platform);
        SevenWanPlatformVariant? variant = SevenWanPlatformCatalog.Find(platform.Id);
        if (variant is null || variant.Platform != platform)
        {
            throw new ArgumentException(
                "The platform is outside the verified 7wan catalog.",
                nameof(platform));
        }

        return variant;
    }

    private static void ValidateResponseOrigin(Uri requestedUri, HttpResponseMessage response)
    {
        Uri? effectiveUri = response.RequestMessage?.RequestUri;
        if (effectiveUri is null || !Uri.Equals(effectiveUri, requestedUri))
        {
            throw new SevenWanServerDirectoryException(
                "The 7wan server catalog response came from an unexpected origin.");
        }
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is HttpRequestException or SevenWanServerDirectoryException or IOException ||
        exception is OperationCanceledException;

    private static bool IsRecoverableCacheFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidOperationException ||
        exception is JsonException or NotSupportedException;
}
