using System.Globalization;
using System.Text.Json.Serialization;

namespace LegendLauncher.Core.Models;

/// <summary>
/// Normalized server metadata exposed to the launcher and runtime.
/// </summary>
[method: JsonConstructor]
public sealed record GameServer(
    string Id,
    long NumericId,
    string Code,
    string Name,
    string FullName,
    Uri? LaunchUri,
    bool IsRecommended,
    bool IsValid,
    string? Merger,
    DateTimeOffset? StartTimeUtc)
{
    /// <summary>
    /// Creates the minimal representation used by manual and cached entries.
    /// </summary>
    public GameServer(string id, string name)
        : this(
            id,
            long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId)
                ? numericId
                : 0,
            string.Empty,
            name,
            name,
            null,
            false,
            true,
            null,
            null)
    {
    }

    /// <summary>
    /// Gets the best label available for display and search.
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? Name : FullName;

    /// <summary>
    /// Indicates whether the server is valid and has already opened.
    /// </summary>
    public bool IsAvailable(DateTimeOffset now) =>
        IsValid && (StartTimeUtc is null || StartTimeUtc <= now);
}
