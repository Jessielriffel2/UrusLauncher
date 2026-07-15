using System.Net;
using LegendLauncher.NetworkBridge;

namespace LegendLauncher.Tests.NetworkBridge;

public sealed class BridgeSecurityPolicyTests
{
    private readonly BridgeSecurityPolicy _policy = new();

    [Fact]
    public void EnsureLoopback_AcceptsIpv4AndIpv6Loopback()
    {
        _policy.EnsureLoopback(new IPEndPoint(IPAddress.Loopback, 0));
        _policy.EnsureLoopback(new IPEndPoint(IPAddress.IPv6Loopback, 0));
    }

    [Fact]
    public void EnsureLoopback_RejectsLanBinding()
    {
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.10"), 8125);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => _policy.EnsureLoopback(endpoint));

        Assert.Contains("loopback", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://odp3.oasgames.com/api/game/serverlist")]
    [InlineData("https://lobr.creaction-network.com/serverlist/s3257")]
    [InlineData("wss://s1.lobr.creaction-network.com/websocket2")]
    public void ValidateUpstream_AllowsKnownGameDomains(string address)
    {
        BridgeValidationResult result = _policy.ValidateUpstream(new Uri(address));

        Assert.True(result.IsAllowed, result.Reason);
    }

    [Theory]
    [InlineData("http://odp3.oasgames.com/api/game/serverlist")]
    [InlineData("https://oasgames.com.attacker.example/payload")]
    [InlineData("https://127.0.0.1/admin")]
    [InlineData("https://user:secret@oasgames.com/")]
    public void ValidateUpstream_RejectsUnsafeAddresses(string address)
    {
        BridgeValidationResult result = _policy.ValidateUpstream(new Uri(address));

        Assert.False(result.IsAllowed);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }
}
