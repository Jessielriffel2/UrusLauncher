using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.Oas;

public sealed class OasCurlPassportTransportTests
{
    [Fact]
    public void ProcessArgumentsNeverContainPassportUriOrCredentials()
    {
        const string userName = "person+test@example.test";
        const string password = "dummy sensitive password&value";
        var requestUri = new Uri(
            $"https://passport.creaction-network.com/index.php?m=login&email={Uri.EscapeDataString(userName)}&pwd={Uri.EscapeDataString(password)}");
        var transport = new OasCurlPassportTransport(TimeSpan.FromSeconds(15), 4096);

        var startInfo = transport.CreateProcessStartInfo();
        var arguments = string.Join('\n', startInfo.ArgumentList);
        var requestConfig = OasCurlPassportTransport.BuildRequestConfig(requestUri);

        Assert.Equal(
            Path.Combine(Environment.SystemDirectory, "curl.exe"),
            startInfo.FileName);
        Assert.True(startInfo.CreateNoWindow);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardInput);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.DoesNotContain("--location", startInfo.ArgumentList);
        Assert.DoesNotContain(requestUri.AbsoluteUri, arguments, StringComparison.Ordinal);
        Assert.DoesNotContain(userName, arguments, StringComparison.Ordinal);
        Assert.DoesNotContain(password, arguments, StringComparison.Ordinal);
        Assert.Contains(requestUri.AbsoluteUri, requestConfig, StringComparison.Ordinal);
        Assert.Contains("header = \"Accept: application/json\"", requestConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("Cookie:", requestConfig, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://passport.creaction-network.com/index.php?m=login")]
    [InlineData("https://user@passport.creaction-network.com/index.php?m=login")]
    [InlineData("https://passport.creaction-network.com:444/index.php?m=login")]
    [InlineData("https://passport.creaction-network.com/other.php?m=login")]
    [InlineData("https://passport.oasgames.com/other.php?m=login")]
    [InlineData("https://passport.oasgames.com:444/index.php?m=login")]
    [InlineData("https://passport.creaction-network.com.evil.example/index.php?m=login")]
    [InlineData("https://passport.creaction-network.com/index.php?m=login#fragment")]
    [InlineData("https://passport.creaction-network.com/index.php?m=login&email=x")]
    [InlineData("https://passport.creaction-network.com/index.php?m=login&email=x&pwd=y&extra=z")]
    [InlineData("https://passport.creaction-network.com/index.php?email=x&pwd=y&m=login")]
    public async Task SendGetAsyncRejectsEveryAddressOutsideExactEndpoints(string address)
    {
        var transport = new OasCurlPassportTransport(TimeSpan.FromSeconds(15), 4096);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            transport.SendGetAsync(new Uri(address), CancellationToken.None));
    }

    [Theory]
    [InlineData("https://passport.creaction-network.com/index.php?m=login&email=user%40example.test&pwd=dummy")]
    [InlineData("https://passport.oasgames.com/index.php?m=login&email=user%40example.test&pwd=dummy")]
    public void BuildRequestConfigAcceptsBothExactOasPassportEndpoints(string address)
    {
        const string userName = "user@example.test";
        const string password = "dummy sensitive password&value";
        var requestUri = new Uri(
            $"https://{new Uri(address).Host}/index.php?m=login&email={Uri.EscapeDataString(userName)}&pwd={Uri.EscapeDataString(password)}");
        var transport = new OasCurlPassportTransport(TimeSpan.FromSeconds(15), 4096);

        var startInfo = transport.CreateProcessStartInfo();
        var arguments = string.Join('\n', startInfo.ArgumentList);
        var requestConfig = OasCurlPassportTransport.BuildRequestConfig(requestUri);

        Assert.Equal(
            Path.Combine(Environment.SystemDirectory, "curl.exe"),
            startInfo.FileName);
        Assert.DoesNotContain(requestUri.AbsoluteUri, arguments, StringComparison.Ordinal);
        Assert.DoesNotContain(userName, arguments, StringComparison.Ordinal);
        Assert.DoesNotContain(password, arguments, StringComparison.Ordinal);
        Assert.Contains(requestUri.AbsoluteUri, requestConfig, StringComparison.Ordinal);
        Assert.Contains("header = \"Accept: application/json\"", requestConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("Cookie:", requestConfig, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendGetAsyncStopsBeforeProcessStartWhenCanceled()
    {
        var transport = new OasCurlPassportTransport(TimeSpan.FromSeconds(15), 4096);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            transport.SendGetAsync(
                new Uri("https://passport.creaction-network.com/index.php?m=login&email=dummy&pwd=dummy"),
                cancellation.Token));
    }
}
