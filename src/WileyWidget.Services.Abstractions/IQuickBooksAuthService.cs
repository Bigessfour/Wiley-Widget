using System;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Service for managing QuickBooks OAuth 2.0 token lifecycle.
/// Handles token acquisition, refresh, revocation, and validation.
/// </summary>
public interface IQuickBooksAuthService
{
    /// <summary>
    /// Gets a valid access token, automatically refreshing if necessary and possible.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A valid access token, or null if no token is available.</returns>
    Task<QuickBooksOAuthToken?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the access token using a refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token to use for obtaining a new access token.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A result indicating success or failure of the refresh operation.</returns>
    Task<TokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges an authorization code for an access token.
    /// Called after OAuth user authorization flow.
    /// </summary>
    /// <param name="authorizationCode">The authorization code from the OAuth flow.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A result containing the token or error message.</returns>
    Task<TokenResult> ExchangeCodeForTokenAsync(string authorizationCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates the OAuth authorization URL that the user should visit to grant access.
    /// </summary>
    /// <returns>The full authorization URL with query parameters.</returns>
    string GenerateAuthorizationUrl();

    /// <summary>
    /// Revokes the current token and clears local cached token.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task RevokeTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current cached token without attempting refresh.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The cached token if available and valid, or null.</returns>
    Task<QuickBooksOAuthToken?> GetCurrentTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a valid access token is currently available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if a valid token exists, false otherwise.</returns>
    Task<bool> HasValidTokenAsync(CancellationToken cancellationToken = default);
}
