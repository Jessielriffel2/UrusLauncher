using System.Text.Json;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Infrastructure.Persistence;

/// <summary>
/// Stores the latest server catalog for each platform in one atomic JSON document.
/// </summary>
public sealed class JsonServerCatalogCache : IServerCatalogCache
{
    private readonly AtomicJsonFileStore<Dictionary<string, ServerCatalog>> _store;

    public JsonServerCatalogCache(
        string filePath,
        JsonSerializerOptions? serializerOptions = null)
    {
        _store = new AtomicJsonFileStore<Dictionary<string, ServerCatalog>>(
            filePath,
            serializerOptions);
    }

    public async Task<ServerCatalog?> GetAsync(
        string platformId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        var catalogs = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (catalogs is null)
        {
            return null;
        }

        var catalog = catalogs
            .Where(pair => string.Equals(pair.Key, platformId, StringComparison.OrdinalIgnoreCase))
            .Select(static pair => pair.Value)
            .FirstOrDefault();

        return catalog?.AsCached();
    }

    public Task SetAsync(
        ServerCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalog.PlatformId);

        return _store.UpdateAsync(
            current =>
            {
                var updated = current is null
                    ? new Dictionary<string, ServerCatalog>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, ServerCatalog>(current, StringComparer.OrdinalIgnoreCase);
                updated[catalog.PlatformId] = catalog;
                return updated;
            },
            cancellationToken);
    }
}
