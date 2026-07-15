using System.Net;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.Oas;

public sealed class OasCurlLaunchTransportTests
{
    [Fact]
    public void CreateProcessStartInfo_UsesTrustedCurlAndContainsNoRequestSecrets()
    {
        const string secretToken = "dummy-token-that-must-not-be-an-argument";
        const string secretCookie = "dummy-cookie-that-must-not-be-an-argument";
        var requestUri = new Uri(
            $"https://lortr.creaction-network.com/serverlist/s115?token={secretToken}");
        var cookies = CreateCookies(requestUri, secretCookie);
        var transport = new OasCurlLaunchTransport(TimeSpan.FromSeconds(15), 4096);

        var requestConfig = OasCurlLaunchTransport.BuildRequestConfig(requestUri, cookies);
        var startInfo = transport.CreateProcessStartInfo();
        var arguments = string.Join('\n', startInfo.ArgumentList);

        Assert.Equal(
            Path.Combine(Environment.SystemDirectory, "curl.exe"),
            startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.True(startInfo.RedirectStandardInput);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.Equal("--disable", startInfo.ArgumentList[0]);
        Assert.Contains("-4", startInfo.ArgumentList);
        Assert.Contains("--http1.1", startInfo.ArgumentList);
        Assert.Contains("--proto", startInfo.ArgumentList);
        Assert.Contains("=https", startInfo.ArgumentList);
        Assert.Contains("--max-redirs", startInfo.ArgumentList);
        Assert.Contains("--max-time", startInfo.ArgumentList);
        Assert.Contains("--max-filesize", startInfo.ArgumentList);
        Assert.Contains("--config", startInfo.ArgumentList);
        Assert.DoesNotContain("--location", startInfo.ArgumentList);
        Assert.DoesNotContain(requestUri.AbsoluteUri, arguments, StringComparison.Ordinal);
        Assert.DoesNotContain(secretToken, arguments, StringComparison.Ordinal);
        Assert.DoesNotContain(secretCookie, arguments, StringComparison.Ordinal);
        Assert.Contains(requestUri.AbsoluteUri, requestConfig, StringComparison.Ordinal);
        Assert.Contains($"Cookie: oas_user={secretCookie}", requestConfig, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRequestConfig_UsesGetAndEscapesQuotedValues()
    {
        var requestUri = new Uri(
            "https://lortr.creaction-network.com/serverlist/s115?value=%22quoted%22");

        var requestConfig = OasCurlLaunchTransport.BuildRequestConfig(
            requestUri,
            new CookieContainer());

        Assert.Contains("request = \"GET\"", requestConfig, StringComparison.Ordinal);
        Assert.Contains("header = \"Accept: text/html\"", requestConfig, StringComparison.Ordinal);
        Assert.Contains("user-agent = \"LegendLauncherNext/0.1\"", requestConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("Cookie:", requestConfig, StringComparison.Ordinal);
        Assert.EndsWith(Environment.NewLine, requestConfig, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("line\rbreak")]
    [InlineData("line\nbreak")]
    [InlineData("tab\tbreak")]
    [InlineData("null\0break")]
    [InlineData("delete\u007Fbreak")]
    public void EscapeConfigValue_RejectsControlCharacters(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            OasCurlLaunchTransport.EscapeConfigValue(value));
    }

    [Fact]
    public void EscapeConfigValue_EscapesQuotesAndBackslashes()
    {
        var escaped = OasCurlLaunchTransport.EscapeConfigValue("a\"b\\c");

        Assert.Equal("a\\\"b\\\\c", escaped);
    }

    [Theory]
    [InlineData("http://lortr.creaction-network.com/serverlist/s115")]
    [InlineData("https://user@lortr.creaction-network.com/serverlist/s115")]
    [InlineData("https://lortr.creaction-network.com:444/serverlist/s115")]
    [InlineData("https://creaction-network.com.evil.example/serverlist/s115")]
    [InlineData("https://example.test/serverlist/s115")]
    public async Task SendGetAsync_RejectsUnsafeAddressBeforeStartingCurl(string address)
    {
        var transport = new OasCurlLaunchTransport(TimeSpan.FromSeconds(15), 4096);

        await Assert.ThrowsAsync<ArgumentException>(() => transport.SendGetAsync(
            new Uri(address),
            new CookieContainer(),
            CancellationToken.None));
    }

    [Fact]
    public async Task SendGetAsync_StopsBeforeStartingCurlWhenAlreadyCanceled()
    {
        var transport = new OasCurlLaunchTransport(TimeSpan.FromSeconds(15), 4096);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.SendGetAsync(
            new Uri("https://lortr.creaction-network.com/serverlist/s115"),
            new CookieContainer(),
            cancellation.Token));
    }

    [Fact]
    public void Constructor_ClampsInfiniteTimeoutToSafetyLimit()
    {
        var transport = new OasCurlLaunchTransport(Timeout.InfiniteTimeSpan, 4096);

        var startInfo = transport.CreateProcessStartInfo();
        var maxTimeIndex = startInfo.ArgumentList.IndexOf("--max-time");

        Assert.True(maxTimeIndex >= 0);
        Assert.Equal("300", startInfo.ArgumentList[maxTimeIndex + 1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(16 * 1024 * 1024 + 1)]
    public void Constructor_RejectsInvalidResponseLimit(int maximumResponseBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OasCurlLaunchTransport(TimeSpan.FromSeconds(15), maximumResponseBytes));
    }

    private static CookieContainer CreateCookies(Uri requestUri, string value)
    {
        var cookies = new CookieContainer();
        cookies.Add(requestUri, new Cookie("oas_user", value));
        return cookies;
    }
}
