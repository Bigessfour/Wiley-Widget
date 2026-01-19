using System;
using System.Text.Json.Serialization;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Represents a QuickBooks OAuth token with expiry tracking and refresh capability.
/// </summary>
public sealed class QuickBooksOAuthToken
{
    /// <summary>
    /// The OAuth access token used for API requests.
    /// </summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// The refresh token used to obtain a new access token.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Token type (usually "Bearer").
    /// </summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Number of seconds until the access token expires.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Number of seconds until the refresh token expires (if provided).
    /// </summary>
    [JsonPropertyName("x_refresh_token_expires_in")]
    public int RefreshTokenExpiresIn { get; set; }

    /// <summary>
    /// Timestamp when the token was issued (UTC).
    /// </summary>
    [JsonIgnore]
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Computed expiry time of the access token (UTC).
    /// </summary>
    [JsonIgnore]
    public DateTime AccessTokenExpiresAtUtc => IssuedAtUtc.AddSeconds(ExpiresIn);

    /// <summary>
    /// Computed expiry time of the refresh token (UTC).
    /// </summary>
    [JsonIgnore]
    public DateTime RefreshTokenExpiresAtUtc => IssuedAtUtc.AddSeconds(RefreshTokenExpiresIn);

    /// <summary>
    /// Checks if the access token has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= AccessTokenExpiresAtUtc;

    /// <summary>
    /// Checks if the access token is expired or will expire within the specified buffer.
    /// </summary>
    /// <param name="bufferSeconds">Number of seconds before expiry to consider the token expired.</param>
    public bool IsExpiredOrSoonToExpire(int bufferSeconds = 300) =>
        DateTime.UtcNow.AddSeconds(bufferSeconds) >= AccessTokenExpiresAtUtc;

    /// <summary>
    /// Checks if the refresh token has expired.
    /// </summary>
    public bool IsRefreshTokenExpired => RefreshTokenExpiresIn > 0 && DateTime.UtcNow >= RefreshTokenExpiresAtUtc;

    /// <summary>
    /// Checks if this token is valid (not expired and has required fields).
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrEmpty(AccessToken) &&
        !IsExpired &&
        !IsRefreshTokenExpired;
}
