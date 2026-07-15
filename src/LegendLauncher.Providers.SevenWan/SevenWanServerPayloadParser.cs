using System.Globalization;
using System.Text.Json;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.SevenWan;

internal static class SevenWanServerPayloadParser
{
    public static async Task<ServerCatalog> ParseAsync(
        Stream payload,
        SevenWanPlatformVariant variant,
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
                !TryGetProperty(root, "server", out var serverGroups) ||
                serverGroups.ValueKind != JsonValueKind.Object ||
                !TryGetProperty(
                    serverGroups,
                    variant.ProviderPlatformId.ToString(CultureInfo.InvariantCulture),
                    out var selectedGroup) ||
                selectedGroup.ValueKind != JsonValueKind.Array)
            {
                throw new SevenWanServerDirectoryException(
                    "The 7wan response does not contain the selected platform bucket.");
            }

            var servers = selectedGroup
                .EnumerateArray()
                .Select(ParseServer)
                .Where(static server => server is not null)
                .Cast<GameServer>()
                .DistinctBy(static server => server.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (servers.Length == 0)
            {
                throw new SevenWanServerDirectoryException(
                    "The 7wan response does not contain recognizable servers.");
            }

            return new ServerCatalog(
                variant.Platform.Id,
                servers,
                Array.Empty<GameServer>(),
                null,
                retrievedAtUtc,
                ServerCatalogSource.Remote);
        }
        catch (JsonException exception)
        {
            throw new SevenWanServerDirectoryException(
                "The 7wan server-list response is not valid JSON.",
                exception);
        }
    }

    private static GameServer? ParseServer(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !TryGetInt64(element, "sid", out long serverId) ||
            serverId <= 0)
        {
            return null;
        }

        string id = serverId.ToString(CultureInfo.InvariantCulture);
        string name = GetString(element, "servername");
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"Server {id}";
        }

        long displayNumber = TryGetInt64(element, "line", out long line) && line > 0
            ? line
            : serverId;
        bool isValid = HasNumericState(element, "status", 1) &&
            HasNumericState(element, "stop_service", 0) &&
            HasNumericState(element, "server_delete", 0);

        return new GameServer(
            id,
            serverId,
            $"S{displayNumber.ToString(CultureInfo.InvariantCulture)}",
            name.Trim(),
            name.Trim(),
            BuildLaunchUri(serverId),
            false,
            isValid,
            null,
            ReadUnixTime(element, "start_time"));
    }

    private static Uri BuildLaunchUri(long serverId)
    {
        var builder = new UriBuilder("https", "7.wan.com")
        {
            Path = "/game/login/",
            Query = $"sid={serverId.ToString(CultureInfo.InvariantCulture)}",
        };
        return builder.Uri;
    }

    private static DateTimeOffset? ReadUnixTime(JsonElement element, string name)
    {
        if (!TryGetInt64(element, name, out long value) || value <= 0)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static bool HasNumericState(JsonElement element, string name, long expected) =>
        TryGetInt64(element, name, out long value) && value == expected;

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
