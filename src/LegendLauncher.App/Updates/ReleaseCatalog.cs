using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LegendLauncher.App.Updates;

internal sealed record ReleaseCatalogEntry(
    Version Version,
    IReadOnlyDictionary<string, string> Titles,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Notes)
{
    public string GetTitle(string languageCode) =>
        GetLocalized(Titles, languageCode, "pt-BR");

    public IReadOnlyList<string> GetNotes(string languageCode) =>
        GetLocalized(Notes, languageCode, Array.Empty<string>());

    private static TValue GetLocalized<TValue>(
        IReadOnlyDictionary<string, TValue> values,
        string languageCode,
        TValue fallback)
    {
        if (values.TryGetValue(languageCode, out TValue? exact))
        {
            return exact;
        }

        return values.TryGetValue("pt-BR", out TValue? portuguese)
            ? portuguese
            : fallback;
    }
}

internal static class ReleaseCatalog
{
    private const string ResourcePrefix = "UrusLauncher.App.Releases.";

    public static IReadOnlyList<ReleaseCatalogEntry> Load()
    {
        Assembly assembly = typeof(ReleaseCatalog).Assembly;
        return assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(name => TryRead(assembly, name))
            .Where(entry => entry is not null)
            .Cast<ReleaseCatalogEntry>()
            .OrderByDescending(entry => entry.Version)
            .ToArray();
    }

    public static string FormatHistory(
        IEnumerable<ReleaseCatalogEntry> entries,
        string languageCode)
    {
        var builder = new StringBuilder();
        foreach (ReleaseCatalogEntry entry in entries)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append("v").Append(entry.Version.ToString(3)).Append(" · ");
            builder.AppendLine(entry.GetTitle(languageCode));
            foreach (string note in entry.GetNotes(languageCode))
            {
                builder.Append("• ").AppendLine(note);
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static ReleaseCatalogEntry? TryRead(Assembly assembly, string resourceName)
    {
        try
        {
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return null;
            }

            ReleaseDefinition? definition = JsonSerializer.Deserialize<ReleaseDefinition>(stream);
            if (definition is null ||
                definition.SchemaVersion != 1 ||
                !Version.TryParse(definition.Version, out Version? version) ||
                definition.Title is null ||
                definition.Notes is null)
            {
                return null;
            }

            Dictionary<string, IReadOnlyList<string>> notes = definition.Notes
                .Where(pair => pair.Value is not null)
                .ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<string>)pair.Value,
                    StringComparer.OrdinalIgnoreCase);
            return new ReleaseCatalogEntry(
                version,
                new Dictionary<string, string>(definition.Title, StringComparer.OrdinalIgnoreCase),
                notes);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private sealed record ReleaseDefinition(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("title")] Dictionary<string, string>? Title,
        [property: JsonPropertyName("notes")] Dictionary<string, string[]>? Notes);
}
