using System.Text.Json.Serialization;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services.Configuration;

/// <summary>
/// Response from Intuit OAuth 2.0 token endpoint.
/// Per spec: https://developer.intuit.com/app/developer/qbo/docs/auth/oauth2
/// </summary>
public sealed class QuickBooksTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("x_refresh_token_expires_in")]
    public int RefreshTokenExpiresIn { get; set; }

    /// <summary>
    /// Converts this response to a QuickBooksOAuthToken for internal use.
    /// </summary>
    public QuickBooksOAuthToken ToOAuthToken()
    {
        return new QuickBooksOAuthToken
        {
            AccessToken = AccessToken,
            RefreshToken = RefreshToken,
            TokenType = TokenType,
            ExpiresIn = ExpiresIn,
            RefreshTokenExpiresIn = RefreshTokenExpiresIn,
            IssuedAtUtc = System.DateTime.UtcNow
        };
    }
}

/// <summary>
/// Error response from Intuit OAuth 2.0 endpoint.
/// Per spec: https://developer.intuit.com/app/developer/qbo/docs/auth/oauth2
/// </summary>
public sealed class QuickBooksTokenErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }

    [JsonPropertyName("error_uri")]
    public string? ErrorUri { get; set; }

    public string GetErrorMessage() =>
        $"{Error}: {ErrorDescription}" + (string.IsNullOrWhiteSpace(ErrorUri) ? "" : $" ({ErrorUri})");
}

/// <summary>
/// Company/Realm information for QuickBooks Online.
/// Parsed from OAuth callback parameters.
/// </summary>
public sealed class QuickBooksCompanyInfo
{
    /// <summary>
    /// Realm ID (Company ID) in QuickBooks Online.
    /// Required for all subsequent API requests.
    /// </summary>
    public string? RealmId { get; set; }

    /// <summary>
    /// Company name (human-readable).
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// Account ID (User ID) that authorized the connection.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Validates that required company info is present.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(RealmId) &&
        !string.IsNullOrWhiteSpace(CompanyName);
}
