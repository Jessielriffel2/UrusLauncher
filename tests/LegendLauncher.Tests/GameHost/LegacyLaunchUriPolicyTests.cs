using LegendLauncher.GameHost.Legacy;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Tests.GameHost;

public sealed class LegacyLaunchUriPolicyTests
{
    [Fact]
    public void FlashConfiguration_DefaultsToRestrictedScriptingAndFullscreen()
    {
        var configuration = FlashRuntimeConfiguration.From(
            new GameRuntimeOptions(Path.GetTempPath()));

        Assert.Equal("sameDomain", configuration.AllowScriptAccess);
        Assert.Equal("false", configuration.AllowFullScreen);
        Assert.Equal("false", configuration.AllowFullScreenInteractive);
        Assert.Equal("ShowAll", configuration.Scale);
        Assert.Equal("high", configuration.Quality);
        Assert.Equal("opaque", configuration.WindowMode);
    }

    [Fact]
    public void FlashConfiguration_AppliesTextQualityThroughQuality2()
    {
        var configuration = FlashRuntimeConfiguration.From(
            new GameRuntimeOptions(Path.GetTempPath()));
        var flash = new FakeFlashProperties();

        configuration.ApplyTo(flash);

        Assert.Equal("sameDomain", flash.AllowScriptAccess);
        Assert.Equal("false", flash.AllowFullScreen);
        Assert.Equal("false", flash.AllowFullScreenInteractive);
        Assert.Equal("ShowAll", flash.Scale);
        Assert.Equal("high", flash.Quality2);
        Assert.Equal("opaque", flash.WMode);
    }

    [Theory]
    [InlineData("https://odp3.oasgames.com/client/Loading.swf")]
    [InlineData("https://lobr.creaction-network.com/client/Loading.swf?token=test")]
    [InlineData("https://s1.lobr.creaction-network.com:443/client/Loading.swf")]
    public void IsAllowed_AcceptsHttpsOasAndCreactionHosts(string address)
    {
        Assert.True(LegacyLaunchUriPolicy.IsAllowed(new Uri(address)));
    }

    [Theory]
    [InlineData("http://lobr.creaction-network.com/client/Loading.swf")]
    [InlineData("wss://lobr.creaction-network.com/websocket2")]
    [InlineData("https://oasgames.com.attacker.example/payload.swf")]
    [InlineData("https://user:password@oasgames.com/payload.swf")]
    [InlineData("https://127.0.0.1/payload.swf")]
    [InlineData("https://oasgames.com:444/payload.swf")]
    public void IsAllowed_RejectsAddressesOutsideFlashLaunchPolicy(string address)
    {
        Assert.False(LegacyLaunchUriPolicy.IsAllowed(new Uri(address)));
    }

    public sealed class FakeFlashProperties
    {
        public string? AllowScriptAccess { get; set; }

        public string? AllowFullScreen { get; set; }

        public string? AllowFullScreenInteractive { get; set; }

        public string? Scale { get; set; }

        public string? Quality2 { get; set; }

        public string? WMode { get; set; }
    }
}
