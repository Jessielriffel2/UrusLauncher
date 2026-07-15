using System.Globalization;
using System.Net;
using System.Text;

namespace LegendLauncher.Providers.Oas;

internal static class OasCurlResponseParser
{
    internal const int MaximumHeaderBytes = 64 * 1024;

    private const string InvalidResponseMessage =
        "The compatible OAS transport returned an invalid HTTP response.";

    public static HttpResponseMessage Parse(
        byte[] output,
        Uri requestUri,
        int maximumBodyBytes)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(requestUri);
        if (maximumBodyBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBodyBytes));
        }

        var offset = 0;
        ParsedHeaderBlock? finalBlock = null;
        while (TryParseHeaderBlock(output, offset, out var currentBlock))
        {
            finalBlock = currentBlock;
            offset = currentBlock.BodyOffset;
            if (offset > MaximumHeaderBytes)
            {
                throw new OasResponseTooLargeException();
            }

            if (!IsInterimBlock(currentBlock) ||
                !LooksLikeHttpStatusLine(output, offset))
            {
                break;
            }
        }

        if (finalBlock is null)
        {
            throw new HttpRequestException(InvalidResponseMessage);
        }

        if (finalBlock.Locations.Count > 1)
        {
            throw new HttpRequestException(InvalidResponseMessage);
        }

        var bodyLength = output.Length - offset;
        if (bodyLength > maximumBodyBytes)
        {
            throw new OasResponseTooLargeException();
        }

        var body = new byte[bodyLength];
        Buffer.BlockCopy(output, offset, body, 0, bodyLength);
        var response = new HttpResponseMessage((HttpStatusCode)finalBlock.StatusCode)
        {
            Content = new ByteArrayContent(body),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri),
            Version = finalBlock.Version,
        };

        foreach (var location in finalBlock.Locations)
        {
            response.Headers.TryAddWithoutValidation("Location", location);
        }

        foreach (var setCookie in finalBlock.SetCookies)
        {
            response.Headers.TryAddWithoutValidation("Set-Cookie", setCookie);
        }

        return response;
    }

    private static bool TryParseHeaderBlock(
        byte[] output,
        int offset,
        out ParsedHeaderBlock parsed)
    {
        parsed = null!;
        if (!LooksLikeHttpStatusLine(output, offset) ||
            !TryFindHeaderTerminator(output, offset, out var headerEnd, out var terminatorLength))
        {
            return false;
        }

        if (headerEnd + terminatorLength > MaximumHeaderBytes)
        {
            throw new OasResponseTooLargeException();
        }

        var headerText = Encoding.Latin1.GetString(output, offset, headerEnd - offset);
        var lines = headerText.Split('\n');
        if (!TryParseStatusLine(
                lines[0].TrimEnd('\r'),
                out var version,
                out var statusCode,
                out var reasonPhrase))
        {
            return false;
        }

        var locations = new List<string>();
        var setCookies = new List<string>();
        for (var index = 1; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (string.Equals(name, "Location", StringComparison.OrdinalIgnoreCase))
            {
                locations.Add(value);
            }
            else if (string.Equals(name, "Set-Cookie", StringComparison.OrdinalIgnoreCase))
            {
                setCookies.Add(value);
            }
        }

        parsed = new ParsedHeaderBlock(
            version,
            statusCode,
            reasonPhrase,
            headerEnd + terminatorLength,
            locations,
            setCookies);
        return true;
    }

    private static bool TryParseStatusLine(
        string statusLine,
        out Version version,
        out int statusCode,
        out string reasonPhrase)
    {
        version = HttpVersion.Version11;
        statusCode = 0;
        reasonPhrase = string.Empty;

        var parts = statusLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 ||
            !parts[0].StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase) ||
            !Version.TryParse(parts[0][5..], out var parsedVersion) ||
            parsedVersion is null ||
            !int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out statusCode) ||
            statusCode is < 100 or > 599)
        {
            return false;
        }

        version = parsedVersion;
        reasonPhrase = parts.Length == 3 ? parts[2] : string.Empty;
        return true;
    }

    private static bool IsInterimBlock(ParsedHeaderBlock block) =>
        block.StatusCode is >= 100 and < 200 ||
        (block.StatusCode == 200 &&
            block.ReasonPhrase.Contains(
                "Connection established",
                StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeHttpStatusLine(byte[] output, int offset)
    {
        ReadOnlySpan<byte> prefix = "HTTP/"u8;
        return offset >= 0 &&
            output.Length - offset >= prefix.Length &&
            output.AsSpan(offset, prefix.Length).SequenceEqual(prefix);
    }

    private static bool TryFindHeaderTerminator(
        byte[] output,
        int offset,
        out int headerEnd,
        out int terminatorLength)
    {
        for (var index = offset; index < output.Length - 1; index++)
        {
            if (index + 3 < output.Length &&
                output[index] == '\r' &&
                output[index + 1] == '\n' &&
                output[index + 2] == '\r' &&
                output[index + 3] == '\n')
            {
                headerEnd = index;
                terminatorLength = 4;
                return true;
            }

            if (output[index] == '\n' && output[index + 1] == '\n')
            {
                headerEnd = index;
                terminatorLength = 2;
                return true;
            }
        }

        headerEnd = 0;
        terminatorLength = 0;
        return false;
    }

    private sealed record ParsedHeaderBlock(
        Version Version,
        int StatusCode,
        string ReasonPhrase,
        int BodyOffset,
        IReadOnlyList<string> Locations,
        IReadOnlyList<string> SetCookies);
}
