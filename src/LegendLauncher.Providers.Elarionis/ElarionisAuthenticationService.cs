using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.Elarionis;

/// <summary>
/// Authenticates an Elarionis account and resolves the Flash movie address for the
/// internal GameHost. Every call uses an independent transport so accounts cannot
/// share sessions. The Elarionis token is treated like a password: it travels only
/// in the request body or query, and it never reaches logs or diagnostics.
/// </summary>
public sealed class ElarionisAuthenticationService : IGameAuthenticationService
{
    private static readonly Uri LoginEndpoint =
        new("https://elarionis.online/api/login", UriKind.Absolute);

    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(15);

    public const int DefaultMaxJsonResponseBytes = 64 * 1024;
    public const int DefaultMaxHtmlResponseBytes = 1024 * 1024;

    private const int MaximumConfigurableResponseBytes = 16 * 1024 * 1024;
    private const int MaximumUserNameLength = 64;
    private const int MaximumPasswordLength = 1024;
    private const int MinimumTokenLength = 8;
    private const int MaximumTokenLength = 512;
    private const int MaximumServerMessageLength = 200;
    private const string UserAgent = "LegendLauncherNext/0.1";

    private readonly Func<HttpMessageHandler> _handlerFactory;
    private readonly TimeSpan _requestTimeout;
    private readonly int _maxJsonResponseBytes;
    private readonly int _maxHtmlResponseBytes;

    /// <summary>
    /// Creates the production adapter. A fresh hardened handler is used for each login attempt.
    /// </summary>
    public ElarionisAuthenticationService()
        : this(CreateDefaultHandler, null, DefaultMaxJsonResponseBytes, DefaultMaxHtmlResponseBytes)
    {
    }

    /// <summary>
    /// Creates an adapter with an injectable handler factory for tests and composition.
    /// The factory must return a new, unused handler for every invocation.
    /// </summary>
    public ElarionisAuthenticationService(
        Func<HttpMessageHandler> handlerFactory,
        TimeSpan? requestTimeout = null,
        int maxJsonResponseBytes = DefaultMaxJsonResponseBytes,
        int maxHtmlResponseBytes = DefaultMaxHtmlResponseBytes)
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
    }

    public async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationFailure = ValidateRequest(request);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        var diagnosticContext = new ElarionisAuthenticationDiagnosticContext();
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

            diagnosticContext.Begin(
                AuthenticationFailurePhase.Passport,
                AuthenticationTransportKind.ManagedHttp);
            // The browser flow sends cookies on every call (withCredentials), so the
            // login jar travels with can_enter and play like a normal navigation.
            var cookies = new CookieContainer(50, 20, 4096);
            LoginOutcome login = await LoginAsync(
                    httpClient,
                    cookies,
                    request.Secret,
                    diagnosticContext,
                    timeoutSource.Token)
                .ConfigureAwait(false);
            if (!login.IsSuccess)
            {
                return login.Failure!;
            }

            string token = login.Token!;
            diagnosticContext.Begin(
                AuthenticationFailurePhase.Launch,
                AuthenticationTransportKind.ManagedHttp);
            var gate = await CheckCanEnterAsync(
                    httpClient,
                    cookies,
                    request.Server.LaunchUri!,
                    token,
                    diagnosticContext,
                    timeoutSource.Token)
                .ConfigureAwait(false);
            if (gate is not null)
            {
                return gate;
            }

            var session = await ResolvePlaySessionAsync(
                    httpClient,
                    cookies,
                    request.Server.LaunchUri!,
                    request.Secret.UserName,
                    token,
                    diagnosticContext,
                    timeoutSource.Token)
                .ConfigureAwait(false);
            if (!session.IsSuccess)
            {
                return session.Failure!;
            }

            return AuthenticationResult.Success(session.Session!);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return AuthenticationResult.Failure(
                ElarionisAuthenticationErrorCodes.RequestTimeout,
                "A autenticação excedeu o tempo limite.",
                diagnosticContext.Current);
        }
        catch (ElarionisResponseTooLargeException)
        {
            return AuthenticationResult.Failure(
                ElarionisAuthenticationErrorCodes.ResponseTooLarge,
                "A plataforma devolveu uma resposta maior que o limite permitido.",
                diagnosticContext.Current);
        }
        catch (HttpRequestException)
        {
            return AuthenticationResult.Failure(
                ElarionisAuthenticationErrorCodes.NetworkError,
                "Não foi possível comunicar com a plataforma.",
                diagnosticContext.Current);
        }
        catch (IOException)
        {
            return AuthenticationResult.Failure(
                ElarionisAuthenticationErrorCodes.NetworkError,
                "A resposta da plataforma foi interrompida.",
                diagnosticContext.Current);
        }
    }

    private async Task<LoginOutcome> LoginAsync(
        HttpClient httpClient,
        CookieContainer cookies,
        CredentialSecret credential,
        ElarionisAuthenticationDiagnosticContext diagnosticContext,
        CancellationToken cancellationToken)
    {
        string payload = BuildLoginPayload(credential.UserName, credential.Password);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, LoginEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Content = content;

        using var response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        var responseDiagnostic = diagnosticContext.RecordStatus((int)response.StatusCode);
        CaptureResponseCookies(response, LoginEndpoint, cookies);

        if (!IsEffectiveUriAllowed(response, ElarionisOriginPolicy.IsAllowedApiUri))
        {
            return LoginOutcome.Failed(OriginFailure(responseDiagnostic));
        }

        if (!response.IsSuccessStatusCode)
        {
            return LoginOutcome.Failed(HttpFailure(responseDiagnostic));
        }

        string json = await ReadUtf8Async(response.Content, _maxJsonResponseBytes, cancellationToken)
            .ConfigureAwait(false);
        if (!TryParseLogin(json, out string? token, out string? serverMessage))
        {
            return LoginOutcome.Failed(AuthenticationResult.Failure(
                ElarionisAuthenticationErrorCodes.InvalidAuthenticationResponse,
                "A plataforma devolveu uma resposta de autenticação inválida.",
                responseDiagnostic));
        }

        if (token is null)
        {
            return LoginOutcome.Failed(AuthenticationResult.Failure(
                ElarionisAuthenticationErrorCodes.AuthenticationRejected,
                NormalizeServerMessage(serverMessage) ?? "O login ou a senha não foram confirmados.",
                responseDiagnostic));
        }

        return LoginOutcome.Succeeded(token);
    }

    private async Task<AuthenticationResult?> CheckCanEnterAsync(
        HttpClient httpClient,
        CookieContainer cookies,
        Uri playDocumentUri,
        string token,
        ElarionisAuthenticationDiagnosticContext diagnosticContext,
        CancellationToken cancellationToken)
    {
        Uri canEnterUri = BuildCanEnterUri(playDocumentUri, token);
        using var request = new HttpRequestMessage(HttpMethod.Get, canEnterUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd(UserAgent);
        AttachCookies(request, cookies, canEnterUri);

        using var response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        var responseDiagnostic = diagnosticContext.RecordStatus((int)response.StatusCode);
        CaptureResponseCookies(response, canEnterUri, cookies);

        if (!IsEffectiveUriAllowed(response, ElarionisOriginPolicy.IsAllowedApiUri))
        {
            return OriginFailure(responseDiagnostic);
        }

        if (!response.IsSuccessStatusCode)
        {
            return HttpFailure(responseDiagnostic);
        }

        string json = await ReadUtf8Async(response.Content, _maxJsonResponseBytes, cancellationToken)
            .ConfigureAwait(false);
        if (!TryParseGate(json, out bool relogin, out bool full, out string? limit))
        {
            // An unreadable gate response must not block the play document itself,
            // which enforces the same rules server-side.
            return null;
        }

        if (relogin)
        {
            return AuthenticationResult.Failure(
                ElarionisAuthenticationErrorCodes.AuthenticationRejected,
                "A sessão expirou antes de entrar no servidor. Entre novamente.",
                responseDiagnostic);
        }

        if (full)
        {
            string message = string.IsNullOrWhiteSpace(limit)
                ? "Esse servidor atingiu o limite de personagens. Escolha outro servidor."
                : $"Esse servidor atingiu o limite de personagens ({limit}). Escolha outro servidor.";
            return AuthenticationResult.Failure(
                ElarionisAuthenticationErrorCodes.InvalidServer,
                message,
                responseDiagnostic);
        }

        return null;
    }

    private async Task<PlayResolutionStep> ResolvePlaySessionAsync(
        HttpClient httpClient,
        CookieContainer cookies,
        Uri playDocumentUri,
        string userName,
        string token,
        ElarionisAuthenticationDiagnosticContext diagnosticContext,
        CancellationToken cancellationToken)
    {
        Uri authenticatedPlayUri = WithToken(playDocumentUri, token);
        var currentUri = authenticatedPlayUri;

        for (var hop = 0; hop <= 1; hop++)
        {
            using var response = await SendPlayGetAsync(httpClient, cookies, currentUri, cancellationToken)
                .ConfigureAwait(false);
            var responseDiagnostic = diagnosticContext.RecordStatus((int)response.StatusCode);
            CaptureResponseCookies(response, currentUri, cookies);

            if (!IsEffectiveUriAllowed(response, ElarionisOriginPolicy.IsAllowedGameUri))
            {
                return PlayResolutionStep.Failed(OriginFailure(responseDiagnostic));
            }

            if (!response.IsSuccessStatusCode)
            {
                return PlayResolutionStep.Failed(HttpFailure(responseDiagnostic));
            }

            Uri effectiveUri = response.RequestMessage?.RequestUri ?? currentUri;
            string html = await ReadUtf8Async(response.Content, _maxHtmlResponseBytes, cancellationToken)
                .ConfigureAwait(false);
            var parsed = ElarionisLaunchPageParser.Parse(html, effectiveUri);
            if (!parsed.IsOriginAllowed)
            {
                return PlayResolutionStep.Failed(OriginFailure(responseDiagnostic));
            }

            if (parsed.IsSuccess)
            {
                Uri launchUri = EnsureToken(parsed.LaunchUri!, token);
                var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
                if (parsed.Parameters is not null)
                {
                    foreach (var pair in parsed.Parameters)
                    {
                        parameters[pair.Key] = pair.Value;
                    }
                }

                parameters.TryAdd("token", token);
                string? site = ElarionisOriginPolicy.GetQueryParameter(authenticatedPlayUri, "site")
                    ?? ElarionisOriginPolicy.GetQueryParameter(effectiveUri, "site");
                if (!string.IsNullOrWhiteSpace(site))
                {
                    parameters.TryAdd("site", site!);
                }

                parameters.TryAdd("user", userName);
                return PlayResolutionStep.Succeeded(new LaunchSession(launchUri, parameters));
            }

            if (parsed.FollowUpUri is null || hop == 1)
            {
                return PlayResolutionStep.Failed(AuthenticationResult.Failure(
                    ElarionisAuthenticationErrorCodes.InvalidLaunchResponse,
                    "A plataforma não forneceu uma sessão de jogo reconhecível.",
                    responseDiagnostic));
            }

            currentUri = WithToken(parsed.FollowUpUri, token);
        }

        return PlayResolutionStep.Failed(AuthenticationResult.Failure(
            ElarionisAuthenticationErrorCodes.InvalidLaunchResponse,
            "A plataforma não forneceu uma sessão de jogo reconhecível.",
            diagnosticContext.Current));
    }

    private async Task<HttpResponseMessage> SendPlayGetAsync(
        HttpClient httpClient,
        CookieContainer cookies,
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        request.Headers.UserAgent.ParseAdd(UserAgent);
        AttachCookies(request, cookies, requestUri);
        return await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void AttachCookies(
        HttpRequestMessage request,
        CookieContainer cookies,
        Uri requestUri)
    {
        string cookieHeader = cookies.GetCookieHeader(requestUri);
        if (!string.IsNullOrEmpty(cookieHeader))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }
    }

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

    private static AuthenticationResult? ValidateRequest(AuthenticationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Secret.UserName) ||
            string.IsNullOrEmpty(request.Secret.Password) ||
            request.Secret.UserName.Length > MaximumUserNameLength ||
            request.Secret.Password.Length > MaximumPasswordLength ||
            !IsWellFormedUtf16(request.Secret.UserName) ||
            !IsWellFormedUtf16(request.Secret.Password))
        {
            return AuthenticationResult.Failure(
                ElarionisAuthenticationErrorCodes.InvalidCredentials,
                "Informe uma conta e uma senha válidas.");
        }

        if (!ElarionisOriginPolicy.TryGetPlatformHost(request.Platform, out _))
        {
            return AuthenticationResult.Failure(
                ElarionisAuthenticationErrorCodes.UnsupportedPlatform,
                "A plataforma selecionada não é compatível com este provedor.");
        }

        if (request.Server.LaunchUri is null ||
            !ElarionisOriginPolicy.IsPlayDocumentUri(request.Server.LaunchUri))
        {
            return AuthenticationResult.Failure(
                ElarionisAuthenticationErrorCodes.InvalidServer,
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

    private static string BuildLoginPayload(string userName, string password)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("username", userName);
            writer.WriteString("password", password);
            writer.WriteString("site", "local");
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static bool TryParseLogin(string json, out string? token, out string? serverMessage)
    {
        token = null;
        serverMessage = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryGetProperty(root, "ok", out var ok) ||
                ok.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return false;
            }

            if (TryGetProperty(root, "error", out var error) &&
                error.ValueKind == JsonValueKind.String)
            {
                serverMessage = error.GetString();
            }

            if (ok.ValueKind != JsonValueKind.True)
            {
                return true;
            }

            if (TryGetProperty(root, "token", out var tokenElement) &&
                tokenElement.ValueKind == JsonValueKind.String &&
                IsPlausibleToken(tokenElement.GetString()))
            {
                token = tokenElement.GetString();
                return true;
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseGate(string json, out bool relogin, out bool full, out string? limit)
    {
        relogin = false;
        full = false;
        limit = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (TryGetProperty(root, "relogin", out var reloginElement))
            {
                relogin = IsTruthy(reloginElement);
            }

            if (TryGetProperty(root, "full", out var fullElement))
            {
                full = IsTruthy(fullElement);
            }

            if (TryGetProperty(root, "limit", out var limitElement))
            {
                limit = limitElement.ValueKind switch
                {
                    JsonValueKind.Number => limitElement.GetRawText(),
                    JsonValueKind.String => limitElement.GetString(),
                    _ => null,
                };
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsTruthy(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.Number => element.TryGetInt64(out long number) && number != 0,
        JsonValueKind.String => string.Equals(
            element.GetString()?.Trim(),
            "true",
            StringComparison.OrdinalIgnoreCase) ||
            element.GetString()?.Trim() == "1",
        _ => false,
    };

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool IsPlausibleToken(string? token) =>
        !string.IsNullOrWhiteSpace(token) &&
        token!.Length is >= MinimumTokenLength and <= MaximumTokenLength &&
        token.All(static c => !char.IsWhiteSpace(c) && !char.IsControl(c));

    private static string? NormalizeServerMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        string normalized = string.Join(
            ' ',
            message!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= MaximumServerMessageLength
            ? normalized
            : normalized[..MaximumServerMessageLength];
    }

    private static Uri BuildCanEnterUri(Uri playDocumentUri, string token)
    {
        string path = playDocumentUri.AbsolutePath;
        var lastSlash = path.LastIndexOf('/');
        string directory = lastSlash >= 0 ? path[..(lastSlash + 1)] : "/";
        var builder = new UriBuilder(
            Uri.UriSchemeHttps,
            ElarionisOriginPolicy.AllowedHost,
            443,
            $"{directory}api/can_enter")
        {
            Query = $"token={Uri.EscapeDataString(token)}",
        };
        return builder.Uri;
    }

    private static Uri WithToken(Uri uri, string token)
    {
        string query = uri.Query.TrimStart('?');
        var parts = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(static part => !IsQueryParameter(part, "token"))
            .Append($"token={Uri.EscapeDataString(token)}");
        var builder = new UriBuilder(uri) { Query = string.Join("&", parts) };
        return builder.Uri;
    }

    private static Uri EnsureToken(Uri uri, string token) =>
        string.IsNullOrWhiteSpace(ElarionisOriginPolicy.GetQueryParameter(uri, "token"))
            ? WithToken(uri, token)
            : uri;

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

    private static bool IsEffectiveUriAllowed(HttpResponseMessage response, Func<Uri, bool> isAllowed)
    {
        var effectiveUri = response.RequestMessage?.RequestUri;
        return effectiveUri is not null && isAllowed(effectiveUri);
    }

    private static async Task<string> ReadUtf8Async(
        HttpContent content,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long contentLength &&
            contentLength > maximumBytes)
        {
            throw new ElarionisResponseTooLargeException();
        }

        await using var contentStream = await content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var bufferStream = new MemoryStream(
            content.Headers.ContentLength is > 0 and <= int.MaxValue
                ? (int)Math.Min(content.Headers.ContentLength.Value, maximumBytes)
                : 0);

        var buffer = new byte[8192];
        while (true)
        {
            int bytesRead = await contentStream
                .ReadAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            if (bufferStream.Length + bytesRead > maximumBytes)
            {
                throw new ElarionisResponseTooLargeException();
            }

            await bufferStream
                .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                .ConfigureAwait(false);
        }

        return Encoding.UTF8
            .GetString(bufferStream.GetBuffer(), 0, checked((int)bufferStream.Length))
            .TrimStart('﻿');
    }

    private HttpMessageHandler CreateHandler()
    {
        var handler = _handlerFactory() ??
            throw new InvalidOperationException("The Elarionis handler factory returned null.");

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

    private static AuthenticationResult HttpFailure(AuthenticationFailureDiagnostic diagnostic) =>
        AuthenticationResult.Failure(
            ElarionisAuthenticationErrorCodes.HttpError,
            "A plataforma recusou a requisição de autenticação.",
            diagnostic);

    private static AuthenticationResult OriginFailure(AuthenticationFailureDiagnostic diagnostic) =>
        AuthenticationResult.Failure(
            ElarionisAuthenticationErrorCodes.OriginNotAllowed,
            "A plataforma devolveu um endereço fora das origens permitidas.",
            diagnostic);

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

    private sealed record LoginOutcome(bool IsSuccess, string? Token, AuthenticationResult? Failure)
    {
        public static LoginOutcome Succeeded(string token) => new(true, token, null);

        public static LoginOutcome Failed(AuthenticationResult failure) => new(false, null, failure);
    }

    private sealed record PlayResolutionStep(bool IsSuccess, LaunchSession? Session, AuthenticationResult? Failure)
    {
        public static PlayResolutionStep Succeeded(LaunchSession session) => new(true, session, null);

        public static PlayResolutionStep Failed(AuthenticationResult failure) => new(false, null, failure);
    }

    private sealed class ElarionisAuthenticationDiagnosticContext
    {
        public AuthenticationFailureDiagnostic? Current { get; private set; }

        public void Begin(AuthenticationFailurePhase phase, AuthenticationTransportKind transport) =>
            Current = new AuthenticationFailureDiagnostic(phase, transport);

        public AuthenticationFailureDiagnostic RecordStatus(int statusCode)
        {
            if (Current is null)
            {
                throw new InvalidOperationException("The diagnostic phase was not initialized.");
            }

            Current = new AuthenticationFailureDiagnostic(Current.Phase, Current.Transport, statusCode);
            return Current;
        }
    }
}

internal sealed class ElarionisResponseTooLargeException : Exception;
