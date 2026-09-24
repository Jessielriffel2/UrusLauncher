using LegendLauncher.Providers.Elarionis;

namespace LegendLauncher.Tests.Elarionis;

public sealed class ElarionisLaunchPageParserTests
{
    private static readonly Uri DocumentUri = new("https://elarionis.online/s7/play?site=s7&token=abc");

    [Fact]
    public void Parse_ExtractsEmbedSwfAndFlashVars()
    {
        const string html = """
            <html><body>
              <object>
                <param name="movie" value="/client/game.swf?zone=7" />
                <param name="FlashVars" value="user=Bob&amp;auth=xyz&amp;site=s7" />
                <embed src="/client/game.swf?zone=7" flashvars="user=Bob&auth=xyz&site=s7" />
              </object>
            </body></html>
            """;

        var result = ElarionisLaunchPageParser.Parse(html, DocumentUri);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsOriginAllowed);
        Assert.Equal(new Uri("https://elarionis.online/client/game.swf?zone=7"), result.LaunchUri);
        Assert.NotNull(result.Parameters);
        Assert.Equal("Bob", result.Parameters["user"]);
        Assert.Equal("xyz", result.Parameters["auth"]);
        Assert.Equal("s7", result.Parameters["site"]);
    }

    [Fact]
    public void Parse_PrefersLoadingMovieAndMergesQueryIntoParameters()
    {
        const string html = """
            <html><head><script>var a="/ads/banner.swf";var b="https://elarionis.online/client/Loading.swf?token=abc";</script></head>
            <body><embed src="https://elarionis.online/client/Loading.swf?token=abc" /></body></html>
            """;

        var result = ElarionisLaunchPageParser.Parse(html, DocumentUri);

        Assert.True(result.IsSuccess);
        Assert.Equal("/client/Loading.swf", result.LaunchUri?.AbsolutePath);
        Assert.NotNull(result.Parameters);
        Assert.Equal("abc", result.Parameters["token"]);
    }

    [Fact]
    public void Parse_RejectsSwfOutsideAllowlist()
    {
        const string html = """<html><body><embed src="https://attacker.example/game.swf" /></body></html>""";

        var result = ElarionisLaunchPageParser.Parse(html, DocumentUri);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsOriginAllowed);
    }

    [Fact]
    public void Parse_ReturnsFollowUpForAllowedIframeWithoutSwf()
    {
        const string html = """<html><body><iframe src="/s7/inner?site=s7"></iframe></body></html>""";

        var result = ElarionisLaunchPageParser.Parse(html, DocumentUri);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsOriginAllowed);
        Assert.Equal(new Uri("https://elarionis.online/s7/inner?site=s7"), result.FollowUpUri);
    }

    [Fact]
    public void Parse_ReturnsNotFoundWhenNoMoviePresent()
    {
        const string html = """<html><body><p>Sunucu Dolu</p></body></html>""";

        var result = ElarionisLaunchPageParser.Parse(html, DocumentUri);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsOriginAllowed);
        Assert.Null(result.LaunchUri);
        Assert.Null(result.FollowUpUri);
    }
}
