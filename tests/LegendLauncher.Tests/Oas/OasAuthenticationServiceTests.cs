using System.Collections.Concurrent;
using System.Net;
using System.Text;
using LegendLauncher.Core.Models;
using LegendLauncher.Providers.Oas;

namespace LegendLauncher.Tests.Oas;

public sealed class OasAuthenticationServiceTests
{
    private const string ValidPassportJson =
        """{"status":"ok","val":{"id":"42","loginKey":"valid-login-key"}}""";

    [Fact]
    public async Task AuthenticateAsync_EscapesCredentialsCarriesCookieAndBuildsLoadingMovie()
    {
        const string userName = "user+tag@example.test";
        const string password = "p@ ss&=?é";
        var handler = new AuthenticationHandler(async (request, call, _) =>
        {
            if (call == 1)
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal("passport.oasgames.com", request.RequestUri?.Host);
                Assert.Equal("/index.php", request.RequestUri?.AbsolutePath);
                Assert.Contains("m=login", request.RequestUri?.Query, StringComparison.Ordinal);
                Assert.Contains("email=user%2Btag%40example.test", request.RequestUri?.Query, StringComparison.Ordinal);
                Assert.Contains("pwd=p%40%20ss%26%3D%3F%C3%A9", request.RequestUri?.Query, StringComparison.Ordinal);
                Assert.Equal("LegendLauncherNext/0.1", request.Headers.UserAgent.ToString());
                Assert.False(request.Headers.Contains("Cookie"));

                return JsonResponse(
                    """{"status":"ok","val":{"id":"4242","loginKey":"session-one"}}""");
            }

            Assert.Equal("lobr.creaction-network.com", request.RequestUri?.Host);
            Assert.Contains("region=br", request.RequestUri?.Query, StringComparison.Ordinal);
            Assert.Contains("pay_later=1", request.RequestUri?.Query, StringComparison.Ordinal);
            Assert.DoesNotContain("pay_later=0", request.RequestUri?.Query, StringComparison.Ordinal);
            Assert.Equal(
                "oas_user=session-one",
                Assert.Single(request.Headers.GetValues("Cookie")));

            return HtmlResponse(
                """
                <html><body>
                  <FRAME data-kind="game"
                    SRC="https://s123lobr.creaction-network.com/client/game.jsp?token=abc%2B123&amp;zone=7">
                </body></html>
                """);
        });
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest(userName, password));

        Assert.True(result.IsSuccess);
        Assert.Equal(4242, result.ProviderUserId);
        Assert.Equal(2, handler.CallCount);
        Assert.NotNull(result.Session);
        Assert.Equal("/client/Loading.swf", result.Session.LaunchUri.AbsolutePath);
        Assert.Equal("?token=abc%2B123&zone=7", result.Session.LaunchUri.Query);
        Assert.Empty(result.Session.Parameters);
        Assert.DoesNotContain(password, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(password, result.Session.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsync_AcceptsTextualProviderIdAndResolvesRelativeFrame()
    {
        var handler = TwoStepHandler(
            """{"status":"ok","val":{"id":"77","loginKey":"text-id-key"}}""",
            """<iframe src='/client/game.jsp?zone=9'></iframe>""");
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(77, result.ProviderUserId);
        Assert.Equal(
            new Uri("https://lobr.creaction-network.com/client/Loading.swf?zone=9"),
            result.Session?.LaunchUri);
    }

    [Fact]
    public async Task AuthenticateAsync_AcceptsNumericProviderId()
    {
        var handler = new AuthenticationHandler((request, call, _) =>
        {
            if (call == 1)
            {
                Assert.Equal("passport.creaction-network.com", request.RequestUri?.Host);
                return Task.FromResult(JsonResponse(
                    """{"status":"ok","val":{"id":88,"loginKey":"numeric-id-key"}}"""));
            }

            return Task.FromResult(HtmlResponse(
                """<frame src="https://s115lortr.creaction-network.com/client/game.jsp"></frame>"""));
        });
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest(
            launchUri: "https://lortr.creaction-network.com/serverlist/s115",
            platform: OasPlatformCatalog.RebornTurkish));

        Assert.True(result.IsSuccess);
        Assert.Equal(88, result.ProviderUserId);
    }

    [Fact]
    public async Task AuthenticateAsync_FollowsLoginTokenBeforeCreatingFlashSession()
    {
        var handler = new AuthenticationHandler((request, call, _) =>
        {
            if (call == 1)
            {
                return Task.FromResult(JsonResponse(
                    """{"status":"ok","val":{"id":15,"loginKey":"follow-up"}}"""));
            }

            Assert.Equal(
                "oas_user=follow-up",
                Assert.Single(request.Headers.GetValues("Cookie")));
            return Task.FromResult(call switch
            {
                2 => HtmlResponse(
                    """<iframe src='https://s5lobr.creaction-network.com/login?token=opaque%20token&amp;x=1'></iframe>"""),
                3 => HtmlResponse(
                    """<frame src='/client/game.jsp?token=flash%2Btoken&amp;zone=5'></frame>"""),
                _ => throw new InvalidOperationException("Unexpected request."),
            });
        });
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(3, handler.CallCount);
        Assert.Equal("/client/Loading.swf", result.Session?.LaunchUri.AbsolutePath);
        Assert.Equal("?token=flash%2Btoken&zone=5", result.Session?.LaunchUri.Query);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsLoginFollowUpLoop()
    {
        const string loopHtml =
            "<iframe src='https://s5lobr.creaction-network.com/login?token=loop'></iframe>";
        var handler = new AuthenticationHandler((_, call, _) => Task.FromResult(
            call == 1
                ? JsonResponse(ValidPassportJson)
                : HtmlResponse(loopHtml)));
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(OasAuthenticationErrorCodes.InvalidLaunchResponse, result.ErrorCode);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task AuthenticateAsync_ResolvesAllowlistedRedirectToGamePage()
    {
        var handler = new AuthenticationHandler((_, call, _) => Task.FromResult(call switch
        {
            1 => JsonResponse(ValidPassportJson),
            2 => HtmlResponse(
                """<iframe src='https://s5lobr.creaction-network.com/login?token=next'></iframe>"""),
            3 => RedirectResponse(
                "https://s5lobr.creaction-network.com/client/game.jsp?token=redirected"),
            _ => throw new InvalidOperationException("Unexpected request."),
        }));
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal("/client/Loading.swf", result.Session?.LaunchUri.AbsolutePath);
        Assert.Contains("token=redirected", result.Session?.LaunchUri.Query, StringComparison.Ordinal);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsRedirectOutsideAllowlist()
    {
        var handler = new AuthenticationHandler((_, call, _) => Task.FromResult(call switch
        {
            1 => JsonResponse(ValidPassportJson),
            2 => RedirectResponse("https://evil.example/client/game.jsp?token=stolen"),
            _ => throw new InvalidOperationException("Unexpected request."),
        }));
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(OasAuthenticationErrorCodes.OriginNotAllowed, result.ErrorCode);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsProviderFailureAndRedactsSubmittedSecret()
    {
        const string userName = "person@example.test";
        const string password = "Dummy secret&value";
        var encodedPassword = Uri.EscapeDataString(password);
        var handler = new AuthenticationHandler((_, _, _) => Task.FromResult(JsonResponse(
            $"{{\"status\":\"error\",\"err_code\":\"AUTH_17\",\"val\":\"Rejected {userName.ToUpperInvariant()} / {encodedPassword}\\n\"}}")));
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest(userName, password));

        Assert.False(result.IsSuccess);
        Assert.Equal("AUTH_17", result.ErrorCode);
        Assert.Equal("A plataforma recusou as credenciais informadas.", result.ErrorMessage);
        Assert.DoesNotContain(userName, result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(password, result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(encodedPassword, result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task AuthenticateAsync_ReplacesUnsafeProviderErrorCode()
    {
        var handler = new AuthenticationHandler((_, _, _) => Task.FromResult(JsonResponse(
            """{"status":"error","err_code":"bad\ncode secret","val":"Rejected"}""")));
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.Equal(OasAuthenticationErrorCodes.AuthenticationRejected, result.ErrorCode);
    }

    [Theory]
    [InlineData("{\"status\":\"ok\"}")]
    [InlineData("{\"status\":\"ok\",\"val\":null}")]
    [InlineData("{\"status\":\"ok\",\"val\":[]}")]
    [InlineData("{\"status\":\"ok\",\"val\":{}}")]
    [InlineData("{\"status\":\"ok\",\"val\":{\"id\":1}}")]
    [InlineData("{\"status\":\"ok\",\"val\":{\"loginKey\":\"missing-id\"}}")]
    [InlineData("{\"status\":\"ok\",\"val\":{\"id\":0,\"loginKey\":\"invalid-id\"}}")]
    [InlineData("{\"status\":\"ok\",\"val\":{\"id\":1,\"loginKey\":\"\"}}")]
    public async Task AuthenticateAsync_RejectsUnconfirmedSuccessfulPassportPayload(string json)
    {
        var handler = new AuthenticationHandler((_, _, _) => Task.FromResult(JsonResponse(json)));
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(OasAuthenticationErrorCodes.InvalidAuthenticationResponse, result.ErrorCode);
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"status\":17,\"val\":{}}")]
    public async Task AuthenticateAsync_RejectsMalformedPassportPayload(string json)
    {
        var handler = new AuthenticationHandler((_, _, _) => Task.FromResult(JsonResponse(json)));
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(OasAuthenticationErrorCodes.InvalidAuthenticationResponse, result.ErrorCode);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsHttpErrorForRejectedPassportRequest()
    {
        var handler = new AuthenticationHandler((_, _, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(OasAuthenticationErrorCodes.HttpError, result.ErrorCode);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsHttpErrorForRejectedServerRequest()
    {
        var handler = new AuthenticationHandler((_, call, _) => Task.FromResult(
            call == 1
                ? JsonResponse(ValidPassportJson)
                : new HttpResponseMessage(HttpStatusCode.Forbidden)));
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(OasAuthenticationErrorCodes.HttpError, result.ErrorCode);
        Assert.Equal(2, handler.CallCount);
    }

    [Theory]
    [InlineData("http://lobr.creaction-network.com/serverlist/s123")]
    [InlineData("https://evil.example/serverlist/s123")]
    [InlineData("https://lobr.creaction-network.com/other/s123")]
    [InlineData("https://user@lobr.creaction-network.com/serverlist/s123")]
    public async Task AuthenticateAsync_RejectsUnsafeServerBeforeSendingCredentials(string launchUri)
    {
        var factoryCalls = 0;
        var service = new OasAuthenticationService(() =>
        {
            Interlocked.Increment(ref factoryCalls);
            throw new InvalidOperationException("Transport must not be created.");
        });

        var result = await service.AuthenticateAsync(CreateRequest(launchUri: launchUri));

        Assert.False(result.IsSuccess);
        Assert.Equal(OasAuthenticationErrorCodes.InvalidServer, result.ErrorCode);
        Assert.Equal(0, factoryCalls);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsUnknownPlatformBeforeSendingCredentials()
    {
        var factoryCalls = 0;
        var service = new OasAuthenticationService(() =>
        {
            Interlocked.Increment(ref factoryCalls);
            throw new InvalidOperationException("Transport must not be created.");
        });
        var validRequest = CreateRequest();
        var unknownPlatform = validRequest.Platform with
        {
            Id = "unknown-oas",
            GameCode = "unknown",
        };
        var request = new AuthenticationRequest(
            unknownPlatform,
            validRequest.Server,
            validRequest.LoginHint,
            validRequest.Secret);

        var result = await service.AuthenticateAsync(request);

        Assert.Equal(OasAuthenticationErrorCodes.UnsupportedPlatform, result.ErrorCode);
        Assert.Equal(0, factoryCalls);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsEmptyCredentialBeforeCreatingTransport()
    {
        var factoryCalls = 0;
        var service = new OasAuthenticationService(() =>
        {
            Interlocked.Increment(ref factoryCalls);
            throw new InvalidOperationException("Transport must not be created.");
        });

        var result = await service.AuthenticateAsync(CreateRequest(password: string.Empty));

        Assert.Equal(OasAuthenticationErrorCodes.InvalidCredentials, result.ErrorCode);
        Assert.Equal(0, factoryCalls);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsMalformedUtf16BeforeCreatingTransport()
    {
        var factoryCalls = 0;
        var service = new OasAuthenticationService(() =>
        {
            Interlocked.Increment(ref factoryCalls);
            throw new InvalidOperationException("Transport must not be created.");
        });
        var malformedPassword = new string('\uD800', 1);

        var result = await service.AuthenticateAsync(CreateRequest(password: malformedPassword));

        Assert.Equal(OasAuthenticationErrorCodes.InvalidCredentials, result.ErrorCode);
        Assert.Equal(0, factoryCalls);
    }

    [Theory]
    [InlineData("<iframe src='http://s5lobr.creaction-network.com/client/game.jsp?token=x'></iframe>")]
    [InlineData("<frame src='https://evil.example/client/game.jsp?token=x'></frame>")]
    [InlineData("<iframe src='https://creaction-network.com@evil.example/client/game.jsp'></iframe>")]
    public async Task AuthenticateAsync_RejectsFrameOutsideHttpsAllowlist(string html)
    {
        var handler = TwoStepHandler(
            ValidPassportJson,
            html);
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(OasAuthenticationErrorCodes.OriginNotAllowed, result.ErrorCode);
    }

    [Fact]
    public async Task AuthenticateAsync_IgnoresFrameLikeMarkupInsideCommentsAndScripts()
    {
        const string html = """
            <!-- <iframe src="https://evil.example/client/game.jsp"></iframe> -->
            <script>const fake = '<frame src="https://evil.example/client/game.jsp">';</script>
            <iframe src="https://s5lobr.creaction-network.com/client/game.jsp?token=real"></iframe>
            """;
        var handler = TwoStepHandler(
            ValidPassportJson,
            html);
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Contains("token=real", result.Session?.LaunchUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsHtmlWithoutRecognizedFrame()
    {
        var handler = TwoStepHandler(
            ValidPassportJson,
            "<html><iframe src='/news'></iframe></html>");
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(OasAuthenticationErrorCodes.InvalidLaunchResponse, result.ErrorCode);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsOversizedResponseBeforeParsing()
    {
        var handler = new AuthenticationHandler((_, _, _) => Task.FromResult(JsonResponse(
            ValidPassportJson)));
        var service = new OasAuthenticationService(
            () => handler,
            maxJsonResponseBytes: 16);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(OasAuthenticationErrorCodes.ResponseTooLarge, result.ErrorCode);
    }

    [Fact]
    public async Task AuthenticateAsync_AppliesResponseLimitToLaunchFollowUps()
    {
        var handler = TwoStepHandler(
            ValidPassportJson,
            new string('x', 64));
        var service = new OasAuthenticationService(
            () => handler,
            maxHtmlResponseBytes: 32);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(OasAuthenticationErrorCodes.ResponseTooLarge, result.ErrorCode);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsTimeoutWithoutLeakingTransportMessage()
    {
        var handler = new AuthenticationHandler(async (_, _, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        });
        var service = new OasAuthenticationService(
            () => handler,
            TimeSpan.FromMilliseconds(30));

        var result = await service.AuthenticateAsync(CreateRequest(password: "timeout-dummy"));

        Assert.False(result.IsSuccess);
        Assert.Equal(OasAuthenticationErrorCodes.RequestTimeout, result.ErrorCode);
        Assert.DoesNotContain("timeout-dummy", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsync_PropagatesCallerCancellation()
    {
        var handler = new AuthenticationHandler(async (_, _, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        });
        var service = CreateService(handler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.AuthenticateAsync(CreateRequest(), cancellation.Token));
    }

    [Fact]
    public async Task AuthenticateAsync_DoesNotExposeSecretFromNetworkException()
    {
        const string password = "network-dummy-secret";
        var handler = new AuthenticationHandler((_, _, _) =>
            throw new HttpRequestException($"Failed URI contains {password}"));
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest(password: password));

        Assert.Equal(OasAuthenticationErrorCodes.NetworkError, result.ErrorCode);
        Assert.DoesNotContain(password, result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(password, result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsEffectiveRedirectOrigin()
    {
        var handler = new AuthenticationHandler((_, _, _) =>
        {
            var response = JsonResponse(ValidPassportJson);
            response.RequestMessage = new HttpRequestMessage(
                HttpMethod.Get,
                "https://evil.example/captured");
            return Task.FromResult(response);
        });
        var service = CreateService(handler);

        var result = await service.AuthenticateAsync(CreateRequest());

        Assert.Equal(OasAuthenticationErrorCodes.OriginNotAllowed, result.ErrorCode);
    }

    [Fact]
    public void InternalParserResults_DoNotExposeRemoteSecretUrlOrTokenInToString()
    {
        const string remoteSecret = "dummy-login-key-secret";
        const string token = "dummy-session-token";
        var providerAssembly = typeof(OasAuthenticationService).Assembly;
        var passportParser = providerAssembly.GetType(
            "LegendLauncher.Providers.Oas.OasPassportResponseParser",
            throwOnError: true)!;
        var passportResult = passportParser
            .GetMethod(
                "Parse",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static)!
            .Invoke(
                null,
                [$"{{\"status\":\"ok\",\"val\":{{\"id\":17,\"loginKey\":\"{remoteSecret}\"}}}}"]);

        var launchParser = providerAssembly.GetType(
            "LegendLauncher.Providers.Oas.OasLaunchPageParser",
            throwOnError: true)!;
        var launchResult = launchParser
            .GetMethod(
                "Parse",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static)!
            .Invoke(
                null,
                [
                    $"<iframe src='https://s5lobr.creaction-network.com/client/game.jsp?token={token}'></iframe>",
                    new Uri("https://lobr.creaction-network.com/serverlist/s5"),
                ]);

        Assert.DoesNotContain(remoteSecret, passportResult?.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(token, launchResult?.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(
            "s5lobr.creaction-network.com",
            launchResult?.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsync_IsolatesCookiesAcrossConcurrentAccounts()
    {
        var handlers = new ConcurrentBag<IsolatedSessionHandler>();
        var bothPassportRequests = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var passportCount = 0;
        var nextSessionId = 0;
        var service = new OasAuthenticationService(() =>
        {
            var handler = new IsolatedSessionHandler(
                Interlocked.Increment(ref nextSessionId),
                () =>
                {
                    if (Interlocked.Increment(ref passportCount) == 2)
                    {
                        bothPassportRequests.TrySetResult();
                    }

                    return bothPassportRequests.Task;
                });
            handlers.Add(handler);
            return handler;
        });

        var results = await Task.WhenAll(
            service.AuthenticateAsync(CreateRequest("first@example.test", "dummy-one")),
            service.AuthenticateAsync(CreateRequest("second@example.test", "dummy-two")));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(2, handlers.Count);
        Assert.All(handlers, handler =>
            Assert.Equal($"oas_user=session-{handler.SessionId}", handler.LaunchCookie));
    }

    private static OasAuthenticationService CreateService(AuthenticationHandler handler) =>
        new(() => handler);

    private static AuthenticationHandler TwoStepHandler(string passportJson, string launchHtml) =>
        new((_, call, _) => Task.FromResult(
            call == 1 ? JsonResponse(passportJson) : HtmlResponse(launchHtml)));

    private static AuthenticationRequest CreateRequest(
        string userName = "account@example.test",
        string password = "dummy-password",
        string launchUri = "https://lobr.creaction-network.com/serverlist/s123?region=br&pay_later=0",
        PlatformDefinition? platform = null)
    {
        var server = new GameServer(
            "123",
            123,
            "S123",
            "Test",
            "OAS123: Test",
            new Uri(launchUri),
            false,
            true,
            null,
            null);
        return new AuthenticationRequest(
            platform ?? OasPlatformCatalog.Brazil,
            server,
            userName,
            new CredentialSecret(userName, password));
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage HtmlResponse(string html) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(html, Encoding.UTF8, "text/html"),
    };

    private static HttpResponseMessage RedirectResponse(string location) => new(HttpStatusCode.Found)
    {
        Headers = { Location = new Uri(location) },
    };

    private sealed class AuthenticationHandler(
        Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        : HttpMessageHandler
    {
        private int _callCount;

        public int CallCount => _callCount;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await sendAsync(
                request,
                Interlocked.Increment(ref _callCount),
                cancellationToken);
            response.RequestMessage ??= request;
            return response;
        }
    }

    private sealed class IsolatedSessionHandler(
        int sessionId,
        Func<Task> waitForBothPassportRequests)
        : HttpMessageHandler
    {
        private int _callCount;

        public int SessionId { get; } = sessionId;

        public string? LaunchCookie { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                await waitForBothPassportRequests().WaitAsync(cancellationToken);
                response = JsonResponse(
                    $"{{\"status\":\"ok\",\"val\":{{\"id\":\"{SessionId}\",\"loginKey\":\"session-{SessionId}\"}}}}");
            }
            else
            {
                LaunchCookie = Assert.Single(request.Headers.GetValues("Cookie"));
                response = HtmlResponse(
                    """<iframe src="https://s5lobr.creaction-network.com/client/game.jsp"></iframe>""");
            }

            response.RequestMessage = request;
            return response;
        }
    }
}
