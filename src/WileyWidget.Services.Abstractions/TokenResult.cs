using System;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Result wrapper for OAuth token operations indicating success or failure.
/// </summary>
public sealed record TokenResult(
    bool IsSuccess,
    QuickBooksOAuthToken? Token = null,
    string? ErrorMessage = null,
    Exception? Exception = null)
{
    // These properties preserve the old interface for compatibility
    public string? AccessToken => Token?.AccessToken;
    public string? RefreshToken => Token?.RefreshToken;
    public int ExpiresIn => Token?.ExpiresIn ?? 0;

    /// <summary>
    /// Creates a successful token result.
    /// </summary>
    public static TokenResult Success(QuickBooksOAuthToken token) =>
        new(IsSuccess: true, Token: token);

    /// <summary>
    /// Creates a successful token result from string tokens.
    /// </summary>
    public static TokenResult Success(string accessToken, string? refreshToken = null, int expiresIn = 3600) =>
        new(IsSuccess: true, Token: new QuickBooksOAuthToken
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn
        });

    /// <summary>
    /// Creates a failed token result.
    /// </summary>
    public static TokenResult Failure(string message, Exception? ex = null) =>
        new(IsSuccess: false, ErrorMessage: message, Exception: ex);
}
