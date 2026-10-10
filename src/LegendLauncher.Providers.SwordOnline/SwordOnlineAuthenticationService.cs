using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.SwordOnline;

/// <summary>
/// Authenticates against the SwordOnline portal. The player creates the account on the
/// website; here the launcher posts login + password to <c>/api/launcher</c> and receives
/// a game ticket that the Flash client forwards (RSA-encrypted) to <c>/login</c>.
/// </summary>
/// <remarks>
/// The password is sent only over HTTPS to <see cref="SwordOnlinePlatformCatalog.LauncherUri"/>
/// (or, for the developer-only local portal, over loopback HTTP) and never appears in an exception, a log line or a returned message.
/// </remarks>
public sealed partial class SwordOnlineAuthenticationService : IGameAuthenticationService
{
    private const int MaxResponseBytes = 16 * 1024;
    private const int MaxMessageLength = 200;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient;
    private readonly SwordOnlinePortal _portal;

    public SwordOnlineAuthenticationService(HttpClient httpClient)
        : this(httpClient, SwordOnlinePlatformCatalog.Portal)
    {
    }

    public SwordOnlineAuthenticationService(HttpClient httpClient, SwordOnlinePortal portal)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _portal = portal ?? throw new ArgumentNullException(nameof(portal));
    }

    public async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.Platform.Id, _portal.PlatformId, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticationResult.Failure(
                SwordOnlineAuthenticationErrorCodes.UnsupportedPlatform,
                $"'{request.Platform.Id}' is not the SwordOnline platform.");
        }

        if (!string.Equals(request.Server.Id, _portal.ServerId, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticationResult.Failure(
                SwordOnlineAuthenticationErrorCodes.InvalidServer,
                $"'{request.Server.Id}' is not the SwordOnline server.");
        }

        string login = request.Secret.UserName.Trim();
        string password = request.Secret.Password;
        if (login.Length == 0 || password.Length == 0)
        {
            return AuthenticationResult.Failure(SwordOnlineAuthenticationErrorCodes.InvalidCredentials);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, _portal.LauncherUri)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { login, senha = password }),
                    Encoding.UTF8,
                    new MediaTypeHeaderValue("application/json")),
            };

            using HttpResponseMessage response = await _httpClient
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            byte[]? body = await ReadBoundedAsync(response, timeout.Token).ConfigureAwait(false);
            if (body is null)
            {
                return AuthenticationResult.Failure(SwordOnlineAuthenticationErrorCodes.ResponseTooLarge);
            }

            return response.StatusCode switch
            {
                HttpStatusCode.OK => ParseSuccess(body, login, _portal),
                HttpStatusCode.Unauthorized => AuthenticationResult.Failure(
                    SwordOnlineAuthenticationErrorCodes.InvalidCredentials,
                    ReadServerMessage(body)),
                HttpStatusCode.BadRequest => AuthenticationResult.Failure(
                    SwordOnlineAuthenticationErrorCodes.AuthenticationRejected,
                    ReadServerMessage(body)),
                _ => AuthenticationResult.Failure(
                    SwordOnlineAuthenticationErrorCodes.HttpError,
                    ReadServerMessage(body)),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthenticationResult.Failure(SwordOnlineAuthenticationErrorCodes.RequestTimeout);
        }
        catch (HttpRequestException)
        {
            return AuthenticationResult.Failure(SwordOnlineAuthenticationErrorCodes.NetworkError);
        }
    }

    private static AuthenticationResult ParseSuccess(byte[] body, string fallbackLogin, SwordOnlinePortal portal)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("ok", out JsonElement ok) ||
                ok.ValueKind != JsonValueKind.True ||
                !root.TryGetProperty("ficha", out JsonElement ticketElement) ||
                ticketElement.ValueKind != JsonValueKind.String)
            {
                return AuthenticationResult.Failure(SwordOnlineAuthenticationErrorCodes.InvalidAuthenticationResponse);
            }

            string? ticket = ticketElement.GetString();
            if (ticket is null || !TicketPattern().IsMatch(ticket))
            {
                return AuthenticationResult.Failure(SwordOnlineAuthenticationErrorCodes.InvalidAuthenticationResponse);
            }

            // The portal answers with the canonical spelling of the login; the Flash client
            // must send exactly that name inside the RSA payload.
            string login = root.TryGetProperty("login", out JsonElement loginElement) &&
                loginElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(loginElement.GetString())
                    ? loginElement.GetString()!
                    : fallbackLogin;

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["user"] = login,
                ["key"] = ticket,
                ["site"] = "local",
                ["config"] = portal.ConfigUri.ToString(),
                ["playfb"] = "0",
                ["http"] = portal.HttpFlashVar,
            };

            return AuthenticationResult.Success(
                new LaunchSession(portal.MovieUri, parameters));
        }
        catch (JsonException)
        {
            return AuthenticationResult.Failure(SwordOnlineAuthenticationErrorCodes.InvalidAuthenticationResponse);
        }
    }

    private static string? ReadServerMessage(byte[] body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("erro", out JsonElement error) &&
                error.ValueKind == JsonValueKind.String)
            {
                string? text = error.GetString()?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    return text.Length <= MaxMessageLength ? text : text[..MaxMessageLength];
                }
            }
        }
        catch (JsonException)
        {
            // Not a JSON error body: fall through to the localized generic message.
        }

        return null;
    }

    private static async Task<byte[]?> ReadBoundedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxResponseBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex TicketPattern();
}
