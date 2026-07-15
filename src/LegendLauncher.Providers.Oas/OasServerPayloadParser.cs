using System.Globalization;
using System.Text.Json;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.Oas;

internal static class OasServerPayloadParser
{
    private static readonly string[] WrapperNames = ["data", "result", "servers"];

    public static async Task<ServerCatalog> ParseAsync(
        Stream payload,
        string platformId,
        DateTimeOffset retrievedAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument
                .ParseAsync(payload, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var root = document.RootElement;
            var all = FindSection(root, "all") is { } allSection
                ? ParseCollection(allSection)
                : root.ValueKind == JsonValueKind.Array
                    ? ParseCollection(root)
                    : [];
            var played = FindSection(root, "played") is { } playedSection
                ? ParseCollection(playedSection)
                : [];
            var current = FindSection(root, "current") is { } currentSection
                ? ParseCollection(currentSection).FirstOrDefault()
                : null;

            return BuildCatalog(platformId, retrievedAtUtc, all, played, current);
        }
        catch (JsonException exception)
        {
            throw new OasServerDirectoryException(
                "The OAS server-list response is not valid JSON.",
                exception);
        }
    }

    private static ServerCatalog BuildCatalog(
        string platformId,
        DateTimeOffset retrievedAtUtc,
        IReadOnlyList<GameServer> rawAll,
        IReadOnlyList<GameServer> rawPlayed,
        GameServer? rawCurrent)
    {
        var serversById = new Dictionary<string, GameServer>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();

        AddOrMerge(rawAll, serversById, order);
        AddOrMerge(rawPlayed, serversById, order);
        if (rawCurrent is not null)
        {
            AddOrMerge([rawCurrent], serversById, order);
        }

        if (serversById.Count == 0)
        {
            throw new OasServerDirectoryException(
                "The OAS server-list response does not contain any recognizable servers.");
        }

        var all = order.Select(id => serversById[id]).ToArray();
        var played = rawPlayed
            .Select(server => serversById[server.Id])
            .DistinctBy(server => server.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var current = rawCurrent is null ? null : serversById[rawCurrent.Id];

        return new ServerCatalog(
            platformId,
            all,
            played,
            current,
            retrievedAtUtc,
            ServerCatalogSource.Remote);
    }

    private static void AddOrMerge(
        IEnumerable<GameServer> servers,
        IDictionary<string, GameServer> serversById,
        ICollection<string> order)
    {
        foreach (var server in servers)
        {
            if (serversById.TryGetValue(server.Id, out var existing))
            {
                serversById[server.Id] = Merge(existing, server);
                continue;
            }

            serversById.Add(server.Id, server);
            order.Add(server.Id);
        }
    }

    private static GameServer Merge(GameServer first, GameServer second) => new(
        first.Id,
        first.NumericId != 0 ? first.NumericId : second.NumericId,
        Prefer(first.Code, second.Code),
        PreferName(first.Name, second.Name, first.Id),
        PreferName(first.FullName, second.FullName, first.Id),
        first.LaunchUri ?? second.LaunchUri,
        first.IsRecommended || second.IsRecommended,
        first.IsValid && second.IsValid,
        PreferNullable(first.Merger, second.Merger),
        first.StartTimeUtc ?? second.StartTimeUtc);

    private static string Prefer(string first, string second) =>
        string.IsNullOrWhiteSpace(first) ? second : first;

    private static string PreferName(string first, string second, string id)
    {
        var fallback = $"Server {id}";
        return string.IsNullOrWhiteSpace(first) ||
               string.Equals(first, fallback, StringComparison.OrdinalIgnoreCase)
            ? second
            : first;
    }

    private static string? PreferNullable(string? first, string? second) =>
        string.IsNullOrWhiteSpace(first) ? second : first;

    private static JsonElement? FindSection(JsonElement element, string sectionName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryGetProperty(element, sectionName, out var direct))
        {
            return direct;
        }

        foreach (var wrapperName in WrapperNames)
        {
            if (TryGetProperty(element, wrapperName, out var wrapper) &&
                FindSection(wrapper, sectionName) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static IReadOnlyList<GameServer> ParseCollection(JsonElement element)
    {
        var servers = new List<GameServer>();
        AppendServers(element, null, servers);
        return servers
            .DistinctBy(server => server.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AppendServers(
        JsonElement element,
        string? fallbackId,
        ICollection<GameServer> target)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    AppendServers(item, null, target);
                }

                break;

            case JsonValueKind.Object when LooksLikeServer(element):
                if (ParseServer(element, fallbackId) is { } server)
                {
                    target.Add(server);
                }

                break;

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    AppendServers(property.Value, property.Name, target);
                }

                break;

            case JsonValueKind.String:
            case JsonValueKind.Number:
                if (ParseServer(element, fallbackId) is { } primitiveServer)
                {
                    target.Add(primitiveServer);
                }

                break;
        }
    }

    private static GameServer? ParseServer(JsonElement element, string? fallbackId)
    {
        if (element.ValueKind is JsonValueKind.String or JsonValueKind.Number)
        {
            var value = NormalizeText(ReadScalar(element));
            var primitiveId = NormalizeText(fallbackId ?? value);
            if (string.IsNullOrWhiteSpace(primitiveId))
            {
                return null;
            }

            var primitiveName = fallbackId is null ? $"Server {primitiveId}" : value;
            return new GameServer(primitiveId, primitiveName);
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = NormalizeText(GetString(
            element,
            "server_sid",
            "server_id",
            "sid",
            "id") ?? fallbackId);
        var numericId = GetInt64(element, "server_id", "server_sid", "sid", "id") ??
                        ExtractNumericId(id);

        if (string.IsNullOrWhiteSpace(id) && numericId != 0)
        {
            id = numericId.ToString(CultureInfo.InvariantCulture);
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var code = NormalizeText(GetString(
            element,
            "server_prex",
            "server_prefix",
            "server_code",
            "code"));
        var name = NormalizeText(GetString(
            element,
            "name",
            "server_name",
            "servername"));
        var fullName = NormalizeText(GetString(
            element,
            "fullname",
            "full_name",
            "server_fullname"));

        name = Prefer(name, fullName);
        name = string.IsNullOrWhiteSpace(name) ? $"Server {id}" : name;
        fullName = Prefer(fullName, name);

        return new GameServer(
            id,
            numericId,
            code,
            name,
            fullName,
            GetUri(element, "url", "server_url", "login_url", "loginurl"),
            GetBoolean(
                element,
                false,
                "recommand",
                "is_recommend",
                "is_recommended",
                "recommended"),
            GetBoolean(element, true, "is_valid", "valid", "status"),
            NormalizeNullable(GetString(element, "merger", "merge", "merged_to")),
            GetDateTimeOffset(
                element,
                "start_time",
                "startTime",
                "open_time",
                "server_start_time"));
    }

    private static bool LooksLikeServer(JsonElement element)
    {
        string[] identityProperties =
        [
            "server_sid",
            "server_id",
            "sid",
            "id",
            "fullname",
            "server_name",
            "server_prex",
        ];

        return identityProperties.Any(name => TryGetProperty(element, name, out _));
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var value))
            {
                return ReadScalar(value);
            }
        }

        return null;
    }

    private static long? GetInt64(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            {
                return number;
            }

            if (long.TryParse(
                    ReadScalar(value),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out number))
            {
                return number;
            }
        }

        return null;
    }

    private static bool GetBoolean(
        JsonElement element,
        bool defaultValue,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var value))
            {
                continue;
            }

            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return value.GetBoolean();
            }

            var text = NormalizeText(ReadScalar(value));
            if (string.Equals(text, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "open", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "available", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(text, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "closed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "invalid", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return defaultValue;
    }

    private static Uri? GetUri(JsonElement element, params string[] names)
    {
        var value = NormalizeText(GetString(element, names));
        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            value = $"https:{value}";
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static DateTimeOffset? GetDateTimeOffset(
        JsonElement element,
        params string[] names)
    {
        var value = NormalizeText(GetString(element, names));
        if (string.IsNullOrWhiteSpace(value) || value == "0")
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            try
            {
                return Math.Abs(unix) >= 100_000_000_000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(unix)
                    : DateTimeOffset.FromUnixTimeSeconds(unix);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static long ExtractNumericId(string id)
    {
        var digits = new string(id.Where(char.IsAsciiDigit).ToArray());
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var numericId)
            ? numericId
            : 0;
    }

    private static string ReadScalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => bool.TrueString,
        JsonValueKind.False => bool.FalseString,
        _ => string.Empty,
    };

    private static string NormalizeText(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? NormalizeNullable(string? value)
    {
        var normalized = NormalizeText(value);
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
