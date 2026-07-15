using System.Globalization;
using System.Text.Json;

namespace LegendLauncher.Providers.Oas;

internal static class OasPassportResponseParser
{
    private const int MaximumErrorCodeLength = 64;
    private const int MaximumLoginKeyLength = 4096;

    public static PassportParseResult Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions { MaxDepth = 16 });

            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !TryGetProperty(document.RootElement, "status", out var statusElement) ||
                statusElement.ValueKind != JsonValueKind.String)
            {
                return PassportParseResult.Invalid;
            }

            var status = statusElement.GetString();
            if (string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetProperty(document.RootElement, "val", out var value))
                {
                    return PassportParseResult.Failure(
                        OasAuthenticationErrorCodes.InvalidAuthenticationResponse,
                        "A plataforma não forneceu uma sessão de autenticação válida.");
                }

                if (value.ValueKind != JsonValueKind.Object ||
                    !TryReadLoginKey(value, out var loginKey) ||
                    !TryReadProviderUserId(value, out var providerUserId))
                {
                    return PassportParseResult.Failure(
                        OasAuthenticationErrorCodes.InvalidAuthenticationResponse,
                        "A plataforma não forneceu uma sessão de autenticação válida.");
                }

                return PassportParseResult.Success(
                    providerUserId,
                    loginKey);
            }

            var errorCode = ReadSafeErrorCode(document.RootElement);
            return PassportParseResult.Failure(
                errorCode,
                "A plataforma recusou as credenciais informadas.");
        }
        catch (JsonException)
        {
            return PassportParseResult.Invalid;
        }
    }

    private static bool TryReadProviderUserId(JsonElement value, out long providerUserId)
    {
        providerUserId = 0;
        return TryGetProperty(value, "id", out var accountId) &&
            TryReadPositiveInt64(accountId, out providerUserId);
    }

    private static bool TryReadLoginKey(JsonElement value, out string loginKey)
    {
        loginKey = string.Empty;
        if (!TryGetProperty(value, "loginKey", out var loginKeyElement) ||
            loginKeyElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var candidate = loginKeyElement.GetString();
        if (string.IsNullOrWhiteSpace(candidate) ||
            candidate.Length > MaximumLoginKeyLength ||
            candidate.Any(character => char.IsControl(character) || character is ';' or ','))
        {
            return false;
        }

        loginKey = candidate;
        return true;
    }

    private static bool TryReadPositiveInt64(JsonElement value, out long parsed)
    {
        parsed = value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt64(out var numericValue)
                ? numericValue
                : 0,
            JsonValueKind.String => long.TryParse(
                value.GetString(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var textValue)
                    ? textValue
                    : 0,
            _ => 0,
        };

        return parsed > 0;
    }

    private static string ReadSafeErrorCode(JsonElement root)
    {
        if (!TryGetProperty(root, "err_code", out var errorCodeElement))
        {
            return OasAuthenticationErrorCodes.AuthenticationRejected;
        }

        var candidate = errorCodeElement.ValueKind switch
        {
            JsonValueKind.String => errorCodeElement.GetString(),
            JsonValueKind.Number => errorCodeElement.GetRawText(),
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(candidate) ||
            candidate.Length > MaximumErrorCodeLength ||
            candidate.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-' and not '.'))
        {
            return OasAuthenticationErrorCodes.AuthenticationRejected;
        }

        return candidate;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

internal sealed record PassportParseResult(
    bool IsValid,
    bool IsSuccess,
    long? ProviderUserId,
    string? LoginKey,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static PassportParseResult Invalid { get; } =
        new(false, false, null, null, null, null);

    public static PassportParseResult Success(long? providerUserId, string loginKey) =>
        new(true, true, providerUserId, loginKey, null, null);

    public static PassportParseResult Failure(string errorCode, string? errorMessage) =>
        new(true, false, null, null, errorCode, errorMessage);

    public override string ToString() =>
        $"PassportParseResult {{ IsValid = {IsValid}, IsSuccess = {IsSuccess}, HasProviderUserId = {ProviderUserId is not null}, HasLoginKey = {LoginKey is not null}, HasError = {ErrorCode is not null} }}";
}
