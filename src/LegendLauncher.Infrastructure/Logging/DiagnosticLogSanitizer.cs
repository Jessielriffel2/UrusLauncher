using System.Text.RegularExpressions;

namespace LegendLauncher.Infrastructure.Logging;

internal static partial class DiagnosticLogSanitizer
{
    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string redacted = SecretAssignmentRegex().Replace(text, "${key}***");
        redacted = BearerRegex().Replace(redacted, "${scheme} ***");
        redacted = JsonSecretRegex().Replace(redacted, "${prefix}\"***\"");
        return redacted;
    }

    [GeneratedRegex(
        """(?in)(?<key>\b(?:password|passwd|pwd|senha|token|cookie|authorization|secret|credential)\s*[=:]\s*)[^\s,;&"]+""",
        RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentRegex();

    [GeneratedRegex(
        """(?in)(?<scheme>\b(?:Bearer|Basic)\s+)\S+""",
        RegexOptions.CultureInvariant)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(
        """(?in)(?<prefix>"(?:password|passwd|pwd|senha|token|cookie|authorization|secret|credential)"\s*:\s*)"[^"]*" """,
        RegexOptions.CultureInvariant)]
    private static partial Regex JsonSecretRegex();
}
