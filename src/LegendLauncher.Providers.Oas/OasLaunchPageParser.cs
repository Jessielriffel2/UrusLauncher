using System.Net;
using System.Text.RegularExpressions;

namespace LegendLauncher.Providers.Oas;

internal static class OasLaunchPageParser
{
    private static readonly Regex NonContentMarkupPattern = new(
        "<!--.*?-->|<script\\b.*?</script\\s*>|<style\\b.*?</style\\s*>",
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline |
        RegexOptions.CultureInvariant |
        RegexOptions.NonBacktracking,
        TimeSpan.FromSeconds(1));

    private static readonly Regex FrameSourcePattern = new(
        "<(?:iframe|frame)\\b[^>]*?\\bsrc\\s*=\\s*(?:\"(?<source>[^\"]*)\"|'(?<source>[^']*)'|(?<source>[^\\s>]+))",
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline |
        RegexOptions.CultureInvariant |
        RegexOptions.NonBacktracking,
        TimeSpan.FromSeconds(1));

    public static LaunchPageParseResult Parse(string html, Uri documentUri)
    {
        try
        {
            return ParseCore(html, documentUri);
        }
        catch (RegexMatchTimeoutException)
        {
            return LaunchPageParseResult.NotFound;
        }
    }

    private static LaunchPageParseResult ParseCore(string html, Uri documentUri)
    {
        var contentHtml = NonContentMarkupPattern.Replace(html, string.Empty);
        foreach (Match match in FrameSourcePattern.Matches(contentHtml))
        {
            var encodedSource = match.Groups["source"].Value;
            var decodedSource = WebUtility.HtmlDecode(encodedSource).Trim();
            if (!Uri.TryCreate(documentUri, decodedSource, out var frameUri) ||
                !IsSupportedFrame(frameUri))
            {
                continue;
            }

            if (!OasOriginPolicy.IsAllowedGameUri(frameUri))
            {
                return LaunchPageParseResult.DisallowedOrigin;
            }

            if (TryCreateGameLaunch(frameUri, out var launchUri))
            {
                return LaunchPageParseResult.Success(launchUri);
            }

            return LaunchPageParseResult.FollowUp(frameUri);
        }

        return LaunchPageParseResult.NotFound;
    }

    public static bool TryCreateGameLaunch(
        Uri uri,
        out Uri launchUri)
    {
        launchUri = uri;
        if (!OasOriginPolicy.IsAllowedGameUri(uri) || !IsGamePage(uri.AbsolutePath))
        {
            return false;
        }

        launchUri = ReplaceGamePageWithLoadingMovie(uri);
        return true;
    }

    private static bool IsSupportedFrame(Uri uri) =>
        IsGamePage(uri.AbsolutePath) ||
        (IsLoginPage(uri.AbsolutePath) &&
         !string.IsNullOrEmpty(GetQueryParameter(uri, "token")));

    private static bool IsGamePage(string absolutePath)
    {
        var lastSlash = absolutePath.LastIndexOf('/');
        var fileName = absolutePath[(lastSlash + 1)..];
        return string.Equals(fileName, "game.jsp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoginPage(string absolutePath)
    {
        var lastSlash = absolutePath.LastIndexOf('/');
        var fileName = absolutePath[(lastSlash + 1)..];
        return string.Equals(fileName, "login", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri ReplaceGamePageWithLoadingMovie(Uri uri)
    {
        if (!IsGamePage(uri.AbsolutePath))
        {
            return uri;
        }

        var builder = new UriBuilder(uri);
        var lastSlash = builder.Path.LastIndexOf('/');
        builder.Path = $"{builder.Path[..(lastSlash + 1)]}Loading.swf";
        return builder.Uri;
    }

    private static string? GetQueryParameter(Uri uri, string expectedName)
    {
        var query = uri.Query.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var encodedName = separator >= 0 ? part[..separator] : part;
            if (!string.Equals(
                    DecodeQueryComponent(encodedName),
                    expectedName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var encodedValue = separator >= 0 ? part[(separator + 1)..] : string.Empty;
            return DecodeQueryComponent(encodedValue);
        }

        return null;
    }

    private static string DecodeQueryComponent(string value)
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

internal sealed record LaunchPageParseResult(
    bool IsSuccess,
    bool IsOriginAllowed,
    Uri? LaunchUri,
    Uri? FollowUpUri)
{
    public static LaunchPageParseResult NotFound { get; } =
        new(false, true, null, null);

    public static LaunchPageParseResult DisallowedOrigin { get; } =
        new(false, false, null, null);

    public static LaunchPageParseResult Success(Uri launchUri) =>
        new(true, true, launchUri, null);

    public static LaunchPageParseResult FollowUp(Uri followUpUri) =>
        new(false, true, null, followUpUri);

    public override string ToString() =>
        $"LaunchPageParseResult {{ IsSuccess = {IsSuccess}, IsOriginAllowed = {IsOriginAllowed}, HasFollowUp = {FollowUpUri is not null} }}";
}
