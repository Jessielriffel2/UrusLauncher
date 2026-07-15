using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.Oas;

/// <summary>
/// Authenticates an OAS account and resolves the allowlisted Flash launch URI.
/// Every call uses an independent transport and cookie jar so accounts cannot share sessions.
/// </summary>
public sealed class OasAuthenticationService : IGameAuthenticationService
{
    private static readonly Uri CreactionPassportEndpoint =
        new("https://passport.creaction-network.com/index.php", UriKind.Absolute);

    private static readonly Uri OasGamesPassportEndpoint =
        new("https://passport.oasgames.com/index.php", UriKind.Absolute);

    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(15);

    public const int DefaultMaxJsonResponseBytes = 64 * 1024;
    public const int DefaultMaxHtmlResponseBytes = 1024 * 1024;

    private const int MaximumConfigurableResponseBytes = 16 * 1024 * 1024;
    private const int MaximumUserNameLength = 320;
    private const int MaximumPasswordLength = 1024;
    private const int MaximumLaunchFollowUpHops = 2;
    private const string UserAgent = "LegendLauncherNext/0.1";
    private const string CreactionPassportGameCode = "lortr";
    private const string AuthenticationCookieName = "oas_user";
    private const string AuthenticationCookieDomain = ".creaction-network.com";

    private readonly Func<HttpMessageHandler> _handlerFactory;
    private readonly OasCurlLaunchTransport? _compatibleLaunchTransport;
    private readonly TimeSpan _requestTimeout;
    private readonly int _maxJsonResponseBytes;
    private readonly int _maxHtmlResponseBytes;

    /// <summary>
    /// Creates the production adapter. A fresh hardened handler is used for each login attempt.
    /// </summary>
    public OasAuthenticationService()
        : this(
            CreateDefaultHandler,
            requestTimeout: null,
            DefaultMaxJsonResponseBytes,
            DefaultMaxHtmlResponseBytes,
            useCompatibleLaunchTransport: true)
    {
    }

    /// <summary>
    /// Creates an adapter with an injectable handler factory for tests and composition.
    /// The factory must return a new, unused handler for every invocation.
    /// </summary>
    public OasAuthenticationService(
        Func<HttpMessageHandler> handlerFactory,
        TimeSpan? requestTimeout = null,
        int maxJsonResponseBytes = DefaultMaxJsonResponseBytes,
        int maxHtmlResponseBytes = DefaultMaxHtmlResponseBytes)
        : this(
            handlerFactory,
            requestTimeout,
            maxJsonResponseBytes,
            maxHtmlResponseBytes,
            useCompatibleLaunchTransport: false)
    {
    }

    private OasAuthenticationService(
        Func<HttpMessageHandler> handlerFactory,
        TimeSpan? requestTimeout,
        int maxJsonResponseBytes,
        int maxHtmlResponseBytes,
        bool useCompatibleLaunchTransport)
    {
        ArgumentNullException.ThrowIfNull(handlerFactory);
        var effectiveTimeout = requestTimeout ?? DefaultRequestTimeout;
        ValidateTimeout(effectiveTimeout);
        ValidateMaximumBytes(maxJsonResponseBytes, nameof(maxJsonResponseBytes));
        ValidateMaximumBytes(maxHtmlResponseBytes, nameof(maxHtmlResponseBytes));

        _handlerFactory = handlerFactory;
        _requestTimeout = effectiveTimeout;
        _maxJsonResponseBytes = maxJsonResponseBytes;
        _maxHtmlResponseBytes = maxHtmlResponseBytes;
        _compatibleLaunchTransport = useCompatibleLaunchTransport
            ? new OasCurlLaunchTransport(effectiveTimeout, maxHtmlResponseBytes)
            : null;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationFailure = ValidateRequest(request, out var platformHost);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_requestTimeout != Timeout.InfiniteTimeSpan)
        {
            timeoutSource.CancelAfter(_requestTimeout);
        }

        try
        {
            using var handler = CreateHandler();
            using var httpClient = new HttpClient(handler, disposeHandler: false)
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };
            var cookies = new CookieContainer(50, 20, 4096);

            var passportResult = await AuthenticatePassportAsync(
                    httpClient,
                    cookies,
                    request.Platform,
                    request.Secret,
                    timeoutSource.Token)
                .ConfigureAwait(false);
            if (!passportResult.IsSuccess)
            {
                return passportResult.Failure!;
            }

            var launchDocumentUri = BuildLaunchDocumentUri(request.Server.LaunchUri!);
            var launchResult = await ResolveLaunchSessionAsync(
                    httpClient,
                    cookies,
                    launchDocumentUri,
                    platformHost,
                    timeoutSource.Token)
                .ConfigureAwait(false);
            if (!launchResult.IsSuccess)
            {
                return launchResult.Failure!;
            }

            return AuthenticationResult.Success(
                launchResult.Session!,
                passportResult.ProviderUserId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return AuthenticationResult.Failure(
                OasAuthenticationErrorCodes.RequestTimeout,
                "A autenticação excedeu o tempo limite.");
        }
        catch (OasResponseTooLargeException)
        {
            return AuthenticationResult.Failure(
                OasAuthenticationErrorCodes.ResponseTooLarge,
                "A plataforma devolveu uma resposta maior que o limite permitido.");
        }
        catch (HttpRequestException)
        {
            return AuthenticationResult.Failure(
                OasAuthenticationErrorCodes.NetworkError,
                "Não foi possível comunicar com a plataforma.");
        }
        catch (IOException)
        {
            return AuthenticationResult.Failure(
                OasAuthenticationErrorCodes.NetworkError,
                "A resposta da plataforma foi interrompida.");
        }
    }

    private async Task<PassportAuthenticationStep> AuthenticatePassportAsync(
        HttpClient httpClient,
        CookieContainer cookies,
        PlatformDefinition platform,
        CredentialSecret credential,
        CancellationToken cancellationToken)
    {
        var passportEndpoint = GetPassportEndpoint(platform);
        var loginUri = BuildPassportUri(passportEndpoint, credential);
        using var response = await SendGetAsync(
                httpClient,
                cookies,
                loginUri,
                "application/json",
                cancellationToken)
            .ConfigureAwait(false);

        if (!IsEffectiveUriAllowed(
                response,
                uri => OasOriginPolicy.IsPassportUri(uri, passportEndpoint)))
        {
            return PassportAuthenticationStep.Failed(OriginFailure());
        }

        if (!response.IsSuccessStatusCode)
        {
            return PassportAuthenticationStep.Failed(HttpFailure());
        }

        CaptureResponseCookies(response, loginUri, cookies);
        var json = await BoundedHttpContentReader
            .ReadUtf8Async(response.Content, _maxJsonResponseBytes, cancellationToken)
            .ConfigureAwait(false);
        var parsed = OasPassportResponseParser.Parse(json);
        if (!parsed.IsValid)
        {
            return PassportAuthenticationStep.Failed(AuthenticationResult.Failure(
                OasAuthenticationErrorCodes.InvalidAuthenticationResponse,
                "A plataforma devolveu uma resposta de autenticação inválida."));
        }

        if (!parsed.IsSuccess)
        {
            return PassportAuthenticationStep.Failed(AuthenticationResult.Failure(
                parsed.ErrorCode!,
                parsed.ErrorMessage));
        }

        if (!TrySetAuthenticationCookie(cookies, parsed.LoginKey!))
        {
            return PassportAuthenticationStep.Failed(AuthenticationResult.Failure(
                OasAuthenticationErrorCodes.InvalidAuthenticationResponse,
                "A plataforma não forneceu uma sessão de autenticação válida."));
        }

        return PassportAuthenticationStep.Succeeded(parsed.ProviderUserId);
    }

    private async Task<LaunchResolutionStep> ResolveLaunchSessionAsync(
        HttpClient httpClient,
        CookieContainer cookies,
        Uri launchDocumentUri,
        string platformHost,
        CancellationToken cancellationToken)
    {
        var currentUri = launchDocumentUri;
        var visitedUris = new HashSet<string>(StringComparer.Ordinal);

        for (var hop = 0; hop <= MaximumLaunchFollowUpHops; hop++)
        {
            if (!visitedUris.Add(GetVisitKey(currentUri)))
            {
                return InvalidLaunchStep();
            }

            using var response = await SendLaunchGetAsync(
                    httpClient,
                    cookies,
                    currentUri,
                    cancellationToken)
                .ConfigureAwait(false);
            var effectiveUri = response.RequestMessage?.RequestUri;
            var isAllowedEffectiveUri = effectiveUri is not null &&
                (hop == 0
                    ? OasOriginPolicy.IsAllowedPlatformUri(effectiveUri, platformHost)
                    : OasOriginPolicy.IsAllowedGameUri(effectiveUri));
            if (!isAllowedEffectiveUri)
            {
                return LaunchResolutionStep.Failed(OriginFailure());
            }

            CaptureResponseCookies(response, effectiveUri!, cookies);

            if (TryResolveRedirect(response, effectiveUri!, out var redirectUri))
            {
                if (!OasOriginPolicy.IsAllowedGameUri(redirectUri))
                {
                    return LaunchResolutionStep.Failed(OriginFailure());
                }

                if (OasLaunchPageParser.TryCreateGameLaunch(
                        redirectUri,
                        out var redirectedLaunchUri))
                {
                    return LaunchResolutionStep.Succeeded(
                        new LaunchSession(redirectedLaunchUri));
                }

                if (hop == MaximumLaunchFollowUpHops)
                {
                    return InvalidLaunchStep();
                }

                currentUri = redirectUri;
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                return LaunchResolutionStep.Failed(HttpFailure());
            }

            if (OasLaunchPageParser.TryCreateGameLaunch(
                    effectiveUri!,
                    out var directLaunchUri))
            {
                return LaunchResolutionStep.Succeeded(
                    new LaunchSession(directLaunchUri));
            }

            var html = await BoundedHttpContentReader
                .ReadUtf8Async(response.Content, _maxHtmlResponseBytes, cancellationToken)
                .ConfigureAwait(false);
            var parsed = OasLaunchPageParser.Parse(html, effectiveUri!);
            if (!parsed.IsOriginAllowed)
            {
                return LaunchResolutionStep.Failed(OriginFailure());
            }

            if (parsed.IsSuccess)
            {
                return LaunchResolutionStep.Succeeded(
                    new LaunchSession(parsed.LaunchUri!));
            }

            if (parsed.FollowUpUri is null || hop == MaximumLaunchFollowUpHops)
            {
                return InvalidLaunchStep();
            }

            currentUri = parsed.FollowUpUri;
        }

        return InvalidLaunchStep();
    }

    private static string GetVisitKey(Uri uri)
    {
        var normalizedOrigin = uri
            .GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)
            .ToLowerInvariant();
        var pathAndQuery = uri.GetComponents(
            UriComponents.PathAndQuery,
            UriFormat.UriEscaped);
        return $"{normalizedOrigin}/{pathAndQuery}";
    }

    private static bool TryResolveRedirect(
        HttpResponseMessage response,
        Uri responseUri,
        out Uri redirectUri)
    {
        redirectUri = null!;
        var statusCode = (int)response.StatusCode;
        if (statusCode is not (301 or 302 or 303 or 307 or 308))
        {
            return false;
        }

        var location = response.Headers.Location;
        if (location is null || !Uri.TryCreate(responseUri, location, out var resolvedUri))
        {
            return false;
        }

        redirectUri = resolvedUri;
        return true;
    }

    private static async Task<HttpResponseMessage> SendGetAsync(
        HttpClient httpClient,
        CookieContainer cookies,
        Uri requestUri,
        string acceptMediaType,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptMediaType));
        request.Headers.UserAgent.ParseAdd(UserAgent);

        var cookieHeader = cookies.GetCookieHeader(requestUri);
        if (!string.IsNullOrEmpty(cookieHeader))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        return await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<HttpResponseMessage> SendLaunchGetAsync(
        HttpClient httpClient,
        CookieContainer cookies,
        Uri requestUri,
        CancellationToken cancellationToken) =>
        _compatibleLaunchTransport is not null
            ? _compatibleLaunchTransport.SendGetAsync(
                requestUri,
                cookies,
                cancellationToken)
            : SendGetAsync(
                httpClient,
                cookies,
                requestUri,
                "text/html",
                cancellationToken);

    private static void CaptureResponseCookies(
        HttpResponseMessage response,
        Uri responseUri,
        CookieContainer cookies)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            return;
        }

        foreach (var setCookieHeader in setCookieHeaders)
        {
            try
            {
                cookies.SetCookies(responseUri, setCookieHeader);
            }
            catch (CookieException)
            {
                // Invalid platform cookies are ignored instead of weakening origin isolation.
            }
        }
    }

    private static bool IsEffectiveUriAllowed(
        HttpResponseMessage response,
        Func<Uri, bool> isAllowed)
    {
        var effectiveUri = response.RequestMessage?.RequestUri;
        return effectiveUri is not null && isAllowed(effectiveUri);
    }

    private static Uri GetPassportEndpoint(PlatformDefinition platform) =>
        string.Equals(
            platform.GameCode,
            CreactionPassportGameCode,
            StringComparison.OrdinalIgnoreCase)
            ? CreactionPassportEndpoint
            : OasGamesPassportEndpoint;

    private static Uri BuildPassportUri(Uri passportEndpoint, CredentialSecret credential)
    {
        var builder = new UriBuilder(passportEndpoint)
        {
            Query = string.Join(
                "&",
                "m=login",
                $"email={Uri.EscapeDataString(credential.UserName)}",
                $"pwd={Uri.EscapeDataString(credential.Password)}"),
        };
        return builder.Uri;
    }

    private static bool TrySetAuthenticationCookie(
        CookieContainer cookies,
        string loginKey)
    {
        try
        {
            cookies.Add(new Cookie(
                AuthenticationCookieName,
                loginKey,
                "/",
                AuthenticationCookieDomain)
            {
                HttpOnly = true,
                Secure = true,
            });
            return true;
        }
        catch (CookieException)
        {
            return false;
        }
    }

    private static Uri BuildLaunchDocumentUri(Uri serverUri)
    {
        var queryParts = serverUri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !IsQueryParameter(part, "pay_later"))
            .Append("pay_later=1");
        var builder = new UriBuilder(serverUri)
        {
            Query = string.Join("&", queryParts),
            Fragment = string.Empty,
        };
        return builder.Uri;
    }

    private static bool IsQueryParameter(string queryPart, string expectedName)
    {
        var separator = queryPart.IndexOf('=');
        var encodedName = separator >= 0 ? queryPart[..separator] : queryPart;
        try
        {
            return string.Equals(
                Uri.UnescapeDataString(encodedName.Replace('+', ' ')),
                expectedName,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static AuthenticationResult? ValidateRequest(
        AuthenticationRequest request,
        out string platformHost)
    {
        platformHost = string.Empty;
        if (string.IsNullOrWhiteSpace(request.Secret.UserName) ||
            string.IsNullOrEmpty(request.Secret.Password) ||
            request.Secret.UserName.Length > MaximumUserNameLength ||
            request.Secret.Password.Length > MaximumPasswordLength ||
            !IsWellFormedUtf16(request.Secret.UserName) ||
            !IsWellFormedUtf16(request.Secret.Password))
        {
            return AuthenticationResult.Failure(
                OasAuthenticationErrorCodes.InvalidCredentials,
                "Informe uma conta e uma senha válidas.");
        }

        if (!OasOriginPolicy.TryGetPlatformHost(request.Platform, out platformHost))
        {
            return AuthenticationResult.Failure(
                OasAuthenticationErrorCodes.UnsupportedPlatform,
                "A plataforma selecionada não é compatível com este provedor.");
        }

        if (request.Server.LaunchUri is null ||
            !OasOriginPolicy.IsAllowedPlatformUri(request.Server.LaunchUri, platformHost) ||
            !IsServerListPath(request.Server.LaunchUri.AbsolutePath))
        {
            return AuthenticationResult.Failure(
                OasAuthenticationErrorCodes.InvalidServer,
                "O endereço do servidor selecionado não é permitido.");
        }

        return null;
    }

    private static bool IsWellFormedUtf16(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    return false;
                }

                index++;
            }
            else if (char.IsLowSurrogate(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsServerListPath(string absolutePath)
    {
        const string prefix = "/serverlist/s";
        return absolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            absolutePath.Length > prefix.Length &&
            absolutePath[prefix.Length..].All(char.IsAsciiDigit);
    }

    private HttpMessageHandler CreateHandler()
    {
        var handler = _handlerFactory() ??
            throw new InvalidOperationException("The OAS handler factory returned null.");

        switch (handler)
        {
            case SocketsHttpHandler socketsHandler:
                socketsHandler.UseCookies = false;
                socketsHandler.AllowAutoRedirect = false;
                break;
            case HttpClientHandler clientHandler:
                clientHandler.UseCookies = false;
                clientHandler.AllowAutoRedirect = false;
                break;
        }

        return handler;
    }

    private static HttpMessageHandler CreateDefaultHandler() => new SocketsHttpHandler
    {
        UseCookies = false,
        AllowAutoRedirect = false,
        AutomaticDecompression =
            DecompressionMethods.GZip |
            DecompressionMethods.Deflate |
            DecompressionMethods.Brotli,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        MaxResponseHeadersLength = 32,
        MaxConnectionsPerServer = 4,
    };

    private static AuthenticationResult HttpFailure() => AuthenticationResult.Failure(
        OasAuthenticationErrorCodes.HttpError,
        "A plataforma recusou a requisição de autenticação.");

    private static AuthenticationResult OriginFailure() => AuthenticationResult.Failure(
        OasAuthenticationErrorCodes.OriginNotAllowed,
        "A plataforma devolveu um endereço fora das origens permitidas.");

    private static LaunchResolutionStep InvalidLaunchStep() =>
        LaunchResolutionStep.Failed(AuthenticationResult.Failure(
            OasAuthenticationErrorCodes.InvalidLaunchResponse,
            "A plataforma não forneceu uma sessão de jogo reconhecível."));

    private static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout != Timeout.InfiniteTimeSpan && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "The request timeout must be positive or infinite.");
        }
    }

    private static void ValidateMaximumBytes(int maximumBytes, string parameterName)
    {
        if (maximumBytes <= 0 || maximumBytes > MaximumConfigurableResponseBytes)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The response limit must be between 1 and {MaximumConfigurableResponseBytes} bytes."));
        }
    }

    private sealed record PassportAuthenticationStep(
        bool IsSuccess,
        long? ProviderUserId,
        AuthenticationResult? Failure)
    {
        public static PassportAuthenticationStep Succeeded(long? providerUserId) =>
            new(true, providerUserId, null);

        public static PassportAuthenticationStep Failed(AuthenticationResult failure) =>
            new(false, null, failure);
    }

    private sealed record LaunchResolutionStep(
        bool IsSuccess,
        LaunchSession? Session,
        AuthenticationResult? Failure)
    {
        public static LaunchResolutionStep Succeeded(LaunchSession session) =>
            new(true, session, null);

        public static LaunchResolutionStep Failed(AuthenticationResult failure) =>
            new(false, null, failure);
    }
}
