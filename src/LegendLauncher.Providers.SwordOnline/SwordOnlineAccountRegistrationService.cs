using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Providers.SwordOnline;

/// <summary>
/// Creates a SwordOnline account through the same endpoint as the website's "Criar conta" page
/// (<c>POST /api/conta/criar</c>). The rules below mirror the site, so a bad login or password is
/// reported without a network round trip; the server remains the authority and may still refuse.
/// </summary>
/// <remarks>
/// The password is sent only over HTTPS to <see cref="SwordOnlinePlatformCatalog.RegisterUri"/> and
/// never appears in an exception, a log line or a returned message.
/// </remarks>
public sealed partial class SwordOnlineAccountRegistrationService : IAccountRegistrationService
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 64;
    public const int MaxEmailLength = 120;

    private const int MaxResponseBytes = 16 * 1024;
    private const int MaxMessageLength = 200;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient;
    private readonly Uri _registerUri;

    public SwordOnlineAccountRegistrationService(HttpClient httpClient)
        : this(httpClient, SwordOnlinePlatformCatalog.RegisterUri)
    {
    }

    public SwordOnlineAccountRegistrationService(HttpClient httpClient, Uri registerUri)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _registerUri = registerUri ?? throw new ArgumentNullException(nameof(registerUri));
    }

    /// <summary>Site rule: 4-16 chars, letters/digits/underscore, starting with a letter.</summary>
    public static bool IsValidLogin(string? login) =>
        login is not null && LoginPattern().IsMatch(login);

    /// <summary>Site rule: 8-64 chars with at least one letter and one digit.</summary>
    public static bool IsValidPassword(string? password) =>
        password is not null &&
        password.Length is >= MinPasswordLength and <= MaxPasswordLength &&
        password.Any(static c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')) &&
        password.Any(static c => c is >= '0' and <= '9');

    public async Task<AccountRegistrationResult> RegisterAsync(
        AccountRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(
                request.Platform.Id,
                SwordOnlinePlatformCatalog.SwordOnline.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            return AccountRegistrationResult.Failure(
                SwordOnlineAuthenticationErrorCodes.UnsupportedPlatform);
        }

        string login = request.Login.Trim();
        if (!IsValidLogin(login))
        {
            return AccountRegistrationResult.Failure(SwordOnlineRegistrationErrorCodes.InvalidLogin);
        }

        if (!IsValidPassword(request.Password))
        {
            return AccountRegistrationResult.Failure(SwordOnlineRegistrationErrorCodes.InvalidPassword);
        }

        string email = request.Email ?? string.Empty;
        if (email.Length > MaxEmailLength || (email.Length > 0 && !LooksLikeEmail(email)))
        {
            return AccountRegistrationResult.Failure(SwordOnlineRegistrationErrorCodes.InvalidEmail);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, _registerUri)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { login, senha = request.Password, email }),
                    Encoding.UTF8,
                    new MediaTypeHeaderValue("application/json")),
            };

            using HttpResponseMessage response = await _httpClient
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            byte[]? body = await ReadBoundedAsync(response, timeout.Token).ConfigureAwait(false);
            if (body is null)
            {
                return AccountRegistrationResult.Failure(SwordOnlineAuthenticationErrorCodes.ResponseTooLarge);
            }

            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
            {
                return ParseSuccess(body, login);
            }

            string? serverMessage = ReadServerMessage(body);
            return response.StatusCode switch
            {
                HttpStatusCode.Conflict => AccountRegistrationResult.Failure(
                    SwordOnlineRegistrationErrorCodes.LoginTaken,
                    serverMessage),
                HttpStatusCode.TooManyRequests => AccountRegistrationResult.Failure(
                    SwordOnlineRegistrationErrorCodes.TooManyRequests,
                    serverMessage),
                HttpStatusCode.BadRequest => AccountRegistrationResult.Failure(
                    SwordOnlineRegistrationErrorCodes.Rejected,
                    serverMessage),
                _ => AccountRegistrationResult.Failure(
                    SwordOnlineAuthenticationErrorCodes.HttpError,
                    serverMessage),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AccountRegistrationResult.Failure(SwordOnlineAuthenticationErrorCodes.RequestTimeout);
        }
        catch (HttpRequestException)
        {
            return AccountRegistrationResult.Failure(SwordOnlineAuthenticationErrorCodes.NetworkError);
        }
    }

    private static bool LooksLikeEmail(string email)
    {
        int at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0 &&
            at == email.LastIndexOf('@') &&
            at < email.Length - 1 &&
            email.IndexOf('.', at) > at + 1 &&
            !email.Any(char.IsWhiteSpace);
    }

    private static AccountRegistrationResult ParseSuccess(byte[] body, string fallbackLogin)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("ok", out JsonElement ok) ||
                ok.ValueKind != JsonValueKind.True)
            {
                return AccountRegistrationResult.Failure(
                    SwordOnlineAuthenticationErrorCodes.InvalidAuthenticationResponse);
            }

            // Prefer the canonical spelling the site answers with; fall back to what was typed.
            string login = root.TryGetProperty("login", out JsonElement loginElement) &&
                loginElement.ValueKind == JsonValueKind.String &&
                IsValidLogin(loginElement.GetString())
                    ? loginElement.GetString()!
                    : fallbackLogin;
            return AccountRegistrationResult.Success(login);
        }
        catch (JsonException)
        {
            return AccountRegistrationResult.Failure(
                SwordOnlineAuthenticationErrorCodes.InvalidAuthenticationResponse);
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
            // Not a JSON error body: the caller falls back to the localized generic message.
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

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]{3,15}\z", RegexOptions.CultureInvariant)]
    private static partial Regex LoginPattern();
}
