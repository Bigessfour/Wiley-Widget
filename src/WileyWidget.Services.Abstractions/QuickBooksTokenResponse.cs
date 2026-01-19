using System.Text.Json.Serialization;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Represents the successful response from Intuit OAuth token endpoint.
/// </summary>
public sealed class QuickBooksTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; } = 3600; // 1 hour default

    [JsonPropertyName("x_refresh_token_expires_in")]
    public int RefreshTokenExpiresIn { get; set; } = 8726400; // 101 days

    [JsonPropertyName("realmId")]
    public string? RealmId { get; set; }

    /// <summary>
    /// Converts the response to a QuickBooksOAuthToken domain model.
    /// </summary>
    public QuickBooksOAuthToken ToOAuthToken() =>
        new()
        {
            AccessToken = AccessToken,
            RefreshToken = RefreshToken,
            TokenType = TokenType,
            ExpiresIn = ExpiresIn,
            RefreshTokenExpiresIn = RefreshTokenExpiresIn
        };
}

/// <summary>
/// Represents an error response from Intuit OAuth endpoint.
/// </summary>
public sealed class TokenErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }

    [JsonPropertyName("error_uri")]
    public string? ErrorUri { get; set; }

    /// <summary>
    /// Gets a user-friendly error message.
    /// </summary>
    public string GetErrorMessage() =>
        !string.IsNullOrEmpty(ErrorDescription)
            ? $"{Error}: {ErrorDescription}"
            : Error;
}

/// <summary>
/// Represents an address from QuickBooks.
/// </summary>
public sealed class Address
{
    [JsonPropertyName("line1")]
    public string? Line1 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }
}

/// <summary>
/// Represents contact information from QuickBooks.
/// </summary>
public sealed class ContactInfo
{
    [JsonPropertyName("free_text")]
    public string? FreeText { get; set; }
}
