using System.Net;
using System.Text;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.Oas;

public sealed class OasCurlResponseParserTests
{
    private static readonly Uri RequestUri =
        new("https://lortr.creaction-network.com/serverlist/s115");

    [Fact]
    public async Task Parse_CreatesResponseWithBodyRequestAndCookies()
    {
        var output = Encode(
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/html\r\n" +
            "Set-Cookie: launch=one; Secure; Path=/\r\n" +
            "Set-Cookie: route=two; Secure; Path=/\r\n" +
            "\r\n" +
            "<html>ready</html>");

        using var response = OasCurlResponseParser.Parse(output, RequestUri, 4096);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpVersion.Version11, response.Version);
        Assert.Equal(RequestUri, response.RequestMessage?.RequestUri);
        Assert.Equal("<html>ready</html>", await response.Content.ReadAsStringAsync());
        Assert.Equal(
            ["launch=one; Secure; Path=/", "route=two; Secure; Path=/"],
            response.Headers.GetValues("Set-Cookie"));
    }

    [Fact]
    public void Parse_UsesLastBlockAfterProxyAndInterimResponses()
    {
        var output = Encode(
            "HTTP/1.1 200 Connection established\r\n" +
            "Set-Cookie: proxy=ignored\r\n" +
            "\r\n" +
            "HTTP/1.1 100 Continue\r\n" +
            "Set-Cookie: interim=ignored\r\n" +
            "\r\n" +
            "HTTP/1.1 302 Found\r\n" +
            "Location: https://s115lortr.creaction-network.com/client/game.jsp?token=dummy\r\n" +
            "Set-Cookie: game=kept; Secure; Path=/\r\n" +
            "\r\n");

        using var response = OasCurlResponseParser.Parse(output, RequestUri, 4096);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(
            new Uri("https://s115lortr.creaction-network.com/client/game.jsp?token=dummy"),
            response.Headers.Location);
        Assert.Equal(
            "game=kept; Secure; Path=/",
            Assert.Single(response.Headers.GetValues("Set-Cookie")));
    }

    [Fact]
    public async Task Parse_DoesNotTreatFinalBodyStartingWithHttpAsAnotherBlock()
    {
        const string body = "HTTP/1.1 418 body text\r\n\r\nstill body";
        var output = Encode("HTTP/1.1 200 OK\r\n\r\n" + body);

        using var response = OasCurlResponseParser.Parse(output, RequestUri, 4096);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(body, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Parse_AcceptsLfOnlyHeaderDelimiter()
    {
        var output = Encode("HTTP/1.1 200 OK\nSet-Cookie: route=one\n\nbody");

        using var response = OasCurlResponseParser.Parse(output, RequestUri, 4096);

        Assert.Equal("body", await response.Content.ReadAsStringAsync());
        Assert.Equal("route=one", Assert.Single(response.Headers.GetValues("Set-Cookie")));
    }

    [Fact]
    public void Parse_RejectsAmbiguousMultipleLocations()
    {
        var output = Encode(
            "HTTP/1.1 302 Found\r\n" +
            "Location: https://s115lortr.creaction-network.com/client/game.jsp?token=one\r\n" +
            "Location: https://evil.example/client/game.jsp?token=two\r\n" +
            "\r\n");

        Assert.Throws<HttpRequestException>(() =>
            OasCurlResponseParser.Parse(output, RequestUri, 4096));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-http")]
    [InlineData("HTTP/1.1 invalid\r\n\r\n")]
    [InlineData("HTTP/1.1 200 OK\r\nmissing-terminator")]
    public void Parse_RejectsMalformedOutput(string value)
    {
        var exception = Assert.Throws<HttpRequestException>(() =>
            OasCurlResponseParser.Parse(Encode(value), RequestUri, 4096));

        if (value.Length > 0)
        {
            Assert.DoesNotContain(value, exception.Message, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(RequestUri.AbsoluteUri, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsOversizedBody()
    {
        var output = Encode("HTTP/1.1 200 OK\r\n\r\n12345");

        Assert.Throws<OasResponseTooLargeException>(() =>
            OasCurlResponseParser.Parse(output, RequestUri, 4));
    }

    [Fact]
    public void Parse_RejectsOversizedHeaders()
    {
        var oversizedHeader = new string('a', OasCurlResponseParser.MaximumHeaderBytes);
        var output = Encode($"HTTP/1.1 200 OK\r\nX-Large: {oversizedHeader}\r\n\r\n");

        Assert.Throws<OasResponseTooLargeException>(() =>
            OasCurlResponseParser.Parse(output, RequestUri, 4096));
    }

    private static byte[] Encode(string value) => Encoding.Latin1.GetBytes(value);
}
