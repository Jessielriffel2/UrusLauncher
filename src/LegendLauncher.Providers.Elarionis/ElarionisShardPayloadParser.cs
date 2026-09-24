using System.Globalization;
using System.Text.Json;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.Elarionis;

internal static class ElarionisShardPayloadParser
{
    public static async Task<ServerCatalog> ParseAsync(
        Stream payload,
        string platformId,
        DateTimeOffset retrievedAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                    payload,
                    new JsonDocumentOptions { MaxDepth = 32 },
                    cancellationToken)
                .ConfigureAwait(false);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryGetProperty(root, "shards", out var shards) ||
                shards.ValueKind != JsonValueKind.Array)
            {
                throw new ElarionisServerDirectoryException(
                    "The Elarionis server-list response does not contain a shard array.");
            }

            var servers = shards
                .EnumerateArray()
                .Select(ParseServer)
                .Where(static server => server is not null)
                .Cast<GameServer>()
                .DistinctBy(static server => server.Id, StringComparer.OrdinalIgnoreCase)
                .OrderBy(static server => server.NumericId)
                .ToArray();
            if (servers.Length == 0)
            {
                throw new ElarionisServerDirectoryException(
                    "The Elarionis server-list response does not contain recognizable servers.");
            }

            return new ServerCatalog(
                platformId,
                servers,
                Array.Empty<GameServer>(),
                null,
                retrievedAtUtc,
                ServerCatalogSource.Remote);
        }
        catch (JsonException exception)
        {
            throw new ElarionisServerDirectoryException(
                "The Elarionis server-list response is not valid JSON.",
                exception);
        }
    }

    private static GameServer? ParseServer(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!TryGetInt64(element, "number", out long number) || number <= 0)
        {
            return null;
        }

        string site = GetString(element, "site");
        string path = GetString(element, "path");
        if (string.IsNullOrWhiteSpace(site) || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        site = site.Trim();
        path = path.Trim();
        string id = number.ToString(CultureInfo.InvariantCulture);
        string name = GetString(element, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"Server {id}";
        }

        string label = GetString(element, "label");
        if (string.IsNullOrWhiteSpace(label))
        {
            label = $"S{id} - {name.Trim()}";
        }

        string state = GetString(element, "state");
        bool isValid = !string.Equals(state.Trim(), "offline", StringComparison.OrdinalIgnoreCase);

        Uri? launchUri = BuildPlayDocumentUri(path, site);
        if (launchUri is null)
        {
            return null;
        }

        return new GameServer(
            id,
            number,
            $"S{id}",
            name.Trim(),
            label.Trim(),
            launchUri,
            false,
            isValid,
            null,
            null);
    }

    internal static Uri? BuildPlayDocumentUri(string path, string site)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(site))
        {
            return null;
        }

        string normalizedPath = path.Trim();
        if (!normalizedPath.StartsWith('/'))
        {
            normalizedPath = "/" + normalizedPath;
        }

        if (!normalizedPath.EndsWith('/'))
        {
            normalizedPath += "/";
        }

        string escapedSite = Uri.EscapeDataString(site.Trim());
        string address = $"https://{ElarionisOriginPolicy.AllowedHost}{normalizedPath}play?site={escapedSite}";
        return Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static bool TryGetInt64(JsonElement element, string name, out long value)
    {
        value = 0;
        if (!TryGetProperty(element, name, out var property))
        {
            return false;
        }

        return property.ValueKind == JsonValueKind.Number
            ? property.TryGetInt64(out value)
            : long.TryParse(
                property.ValueKind == JsonValueKind.String ? property.GetString() : null,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
    }

    private static string GetString(JsonElement element, string name) =>
        TryGetProperty(element, name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static bool TryGetProperty(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
