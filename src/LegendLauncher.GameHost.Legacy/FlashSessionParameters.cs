namespace LegendLauncher.GameHost.Legacy;

internal static class FlashSessionParameters
{
    public static string Encode(IReadOnlyDictionary<string, string> parameters) =>
        string.Join(
            '&',
            parameters
                .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .Select(parameter =>
                    $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));
}
