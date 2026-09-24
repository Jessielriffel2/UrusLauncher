using System.Net;
using System.Text.RegularExpressions;

namespace LegendLauncher.Providers.Elarionis;

internal sealed record ElarionisLaunchPageParseResult(
    bool IsSuccess,
    bool IsOriginAllowed,
    Uri? LaunchUri,
    IReadOnlyDictionary<string, string>? Parameters,
    Uri? FollowUpUri)
{
    public static ElarionisLaunchPageParseResult NotFound { get; } =
        new(false, true, null, null, null);

    public static ElarionisLaunchPageParseResult DisallowedOrigin { get; } =
        new(false, false, null, null, null);

    public static ElarionisLaunchPageParseResult Success(
        Uri launchUri,
        IReadOnlyDictionary<string, string> parameters) =>
        new(true, true, launchUri, parameters, null);

    public static ElarionisLaunchPageParseResult FollowUp(Uri followUpUri) =>
        new(false, true, null, null, followUpUri);

    public override string ToString() =>
        $"ElarionisLaunchPageParseResult {{ IsSuccess = {IsSuccess}, IsOriginAllowed = {IsOriginAllowed}, HasLaunch = {LaunchUri is not null}, HasFollowUp = {FollowUpUri is not null} }}";
}

/// <summary>
/// Extracts the Flash movie address and its FlashVars from an Elarionis play document
/// so the internal GameHost can load it instead of a browser.
/// </summary>
internal static class ElarionisLaunchPageParser
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex SwfUrlPattern = new(
        "(?:[\"'])(?<source>https?://[^\"'<>\\s]+\\.swf(?:\\?[^\"'<>]*)?|/[^\"'<>\\s]*\\.swf(?:\\?[^\"'<>]*)?)(?:[\"'])",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.NonBacktracking,
        RegexTimeout);

    private static readonly Regex MovieParamPattern = new(
        "<param\\b[^>]*?\\bname\\s*=\\s*(?:\"(?:movie|src|source)\"|'(?:movie|src|source)'|(?:movie|src|source))[^>]*?\\bvalue\\s*=\\s*(?:\"(?<source>[^\"]*)\"|'(?<source>[^']*)'|(?<source>[^\\s>]+))",
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline |
        RegexOptions.CultureInvariant |
        RegexOptions.NonBacktracking,
        RegexTimeout);

    private static readonly Regex EmbedSourcePattern = new(
        "<embed\\b[^>]*?\\bsrc\\s*=\\s*(?:\"(?<source>[^\"]*)\"|'(?<source>[^']*)'|(?<source>[^\\s>]+))",
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline |
        RegexOptions.CultureInvariant |
        RegexOptions.NonBacktracking,
        RegexTimeout);

    private static readonly Regex FlashVarsParamPattern = new(
        "<param\\b[^>]*?\\bname\\s*=\\s*(?:\"flashvars\"|'flashvars'|flashvars)[^>]*?\\bvalue\\s*=\\s*(?:\"(?<vars>[^\"]*)\"|'(?<vars>[^']*)'|(?<vars>[^\\s>]+))",
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline |
        RegexOptions.CultureInvariant |
        RegexOptions.NonBacktracking,
        RegexTimeout);

    private static readonly Regex EmbedFlashVarsPattern = new(
        "<embed\\b[^>]*?\\bflashvars\\s*=\\s*(?:\"(?<vars>[^\"]*)\"|'(?<vars>[^']*)'|(?<vars>[^\\s>]+))",
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline |
        RegexOptions.CultureInvariant |
        RegexOptions.NonBacktracking,
        RegexTimeout);

    private static readonly Regex JsFlashVarsPattern = new(
        "flashvars\\s*[:=]\\s*(?:\"(?<vars>[^\"]*)\"|'(?<vars>[^']*)')",
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline |
        RegexOptions.CultureInvariant |
        RegexOptions.NonBacktracking,
        RegexTimeout);

    private static readonly Regex FrameSourcePattern = new(
        "<(?:iframe|frame)\\b[^>]*?\\bsrc\\s*=\\s*(?:\"(?<source>[^\"]*)\"|'(?<source>[^']*)'|(?<source>[^\\s>]+))",
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline |
        RegexOptions.CultureInvariant |
        RegexOptions.NonBacktracking,
        RegexTimeout);

    public static ElarionisLaunchPageParseResult Parse(string html, Uri documentUri)
    {
        try
        {
            return ParseCore(html, documentUri);
        }
        catch (RegexMatchTimeoutException)
        {
            return ElarionisLaunchPageParseResult.NotFound;
        }
    }

    private static ElarionisLaunchPageParseResult ParseCore(string html, Uri documentUri)
    {
        if (string.IsNullOrEmpty(html))
        {
            return ElarionisLaunchPageParseResult.NotFound;
        }

        var candidates = new List<Uri>();
        bool sawDisallowedSwf = false;
        foreach (var raw in EnumerateSwfSources(html, documentUri))
        {
            if (!Uri.TryCreate(documentUri, raw, out var resolved))
            {
                continue;
            }

            if (!IsSwfAddress(resolved))
            {
                continue;
            }

            if (!ElarionisOriginPolicy.IsAllowedGameUri(resolved))
            {
                sawDisallowedSwf = true;
                continue;
            }

            if (!candidates.Any(existing => Uri.Equals(existing, resolved)))
            {
                candidates.Add(resolved);
            }
        }

        if (candidates.Count > 0)
        {
            Uri selected = SelectPreferred(candidates);
            var parameters = ExtractFlashVars(html, selected);
            return ElarionisLaunchPageParseResult.Success(selected, parameters);
        }

        if (sawDisallowedSwf)
        {
            return ElarionisLaunchPageParseResult.DisallowedOrigin;
        }

        foreach (Match match in FrameSourcePattern.Matches(html))
        {
            var encodedSource = match.Groups["source"].Value;
            var decodedSource = WebUtility.HtmlDecode(encodedSource).Trim();
            if (!Uri.TryCreate(documentUri, decodedSource, out var frameUri))
            {
                continue;
            }

            if (!ElarionisOriginPolicy.IsAllowedGameUri(frameUri))
            {
                return ElarionisLaunchPageParseResult.DisallowedOrigin;
            }

            return ElarionisLaunchPageParseResult.FollowUp(frameUri);
        }

        return ElarionisLaunchPageParseResult.NotFound;
    }

    private static IEnumerable<string> EnumerateSwfSources(string html, Uri documentUri)
    {
        foreach (Match match in MovieParamPattern.Matches(html))
        {
            yield return WebUtility.HtmlDecode(match.Groups["source"].Value).Trim();
        }

        foreach (Match match in EmbedSourcePattern.Matches(html))
        {
            yield return WebUtility.HtmlDecode(match.Groups["source"].Value).Trim();
        }

        foreach (Match match in SwfUrlPattern.Matches(html))
        {
            yield return WebUtility.HtmlDecode(match.Groups["source"].Value).Trim();
        }

        _ = documentUri;
    }

    private static bool IsSwfAddress(Uri uri) =>
        uri.AbsolutePath.EndsWith(".swf", StringComparison.OrdinalIgnoreCase);

    private static Uri SelectPreferred(List<Uri> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.AbsolutePath.Contains("loading", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    private static IReadOnlyDictionary<string, string> ExtractFlashVars(string html, Uri swfUri)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in ParseQueryPairs(swfUri.Query))
        {
            merged.TryAdd(pair.Key, pair.Value);
        }

        foreach (var encoded in EnumerateFlashVarsBlobs(html))
        {
            var decoded = WebUtility.HtmlDecode(encoded).Trim();
            if (string.IsNullOrEmpty(decoded))
            {
                continue;
            }

            foreach (var pair in ParseQueryPairs(NormalizeVarsBlob(decoded)))
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return merged;
    }

    private static IEnumerable<string> EnumerateFlashVarsBlobs(string html)
    {
        foreach (Match match in FlashVarsParamPattern.Matches(html))
        {
            yield return match.Groups["vars"].Value;
        }

        foreach (Match match in EmbedFlashVarsPattern.Matches(html))
        {
            yield return match.Groups["vars"].Value;
        }

        foreach (Match match in JsFlashVarsPattern.Matches(html))
        {
            yield return match.Groups["vars"].Value;
        }
    }

    private static string NormalizeVarsBlob(string blob)
    {
        string trimmed = blob.Trim();
        if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) ||
            (trimmed.StartsWith('(') && trimmed.EndsWith(')')))
        {
            trimmed = trimmed[1..^1];
        }

        if (trimmed.Contains(':') && !trimmed.Contains('='))
        {
            var pairs = new List<string>();
            foreach (var part in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var colon = part.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                string key = Unquote(part[..colon].Trim());
                string value = Unquote(part[(colon + 1)..].Trim());
                pairs.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            }

            return string.Join("&", pairs);
        }

        return trimmed.StartsWith('?') ? trimmed[1..] : trimmed;
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 &&
            ((value.StartsWith('"') && value.EndsWith('"')) ||
             (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            return value[1..^1];
        }

        return value;
    }

    internal static IEnumerable<KeyValuePair<string, string>> ParseQueryPairs(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            yield break;
        }

        string normalized = query.Trim().TrimStart('?', '&');
        foreach (var part in normalized.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            string encodedKey = separator >= 0 ? part[..separator] : part;
            string encodedValue = separator >= 0 ? part[(separator + 1)..] : string.Empty;
            string key = Decode(encodedKey).Trim();
            if (string.IsNullOrEmpty(key) || key.Length > 256)
            {
                continue;
            }

            string value = Decode(encodedValue);
            if (value.Length > 4096)
            {
                continue;
            }

            yield return new KeyValuePair<string, string>(key, value);
        }
    }

    private static string Decode(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
        catch (UriFormatException)
        {
            return string.Empty;
        }
    }
}
