using System;
using System.Collections.Generic;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Configuration options for QuickBooks OAuth 2.0 authentication.
/// Loaded from appsettings.json under Services.QuickBooks.OAuth
/// </summary>
public sealed class QuickBooksOAuthOptions
{
    /// <summary>
    /// Intuit app Client ID (registered at developer.intuit.com).
    /// Load from user secrets or environment variable QUICKBOOKS_CLIENT_ID.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Intuit app Client Secret (registered at developer.intuit.com).
    /// Load from user secrets or environment variable QUICKBOOKS_CLIENT_SECRET.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// OAuth redirect URI for the callback handler.
    /// Typically http://localhost:5000/callback for development.
    /// Must match the registered URI at developer.intuit.com.
    /// </summary>
    public string? RedirectUri { get; set; }

    /// <summary>
    /// OAuth environment: "sandbox" or "production".
    /// Defaults to "sandbox" for development.
    /// </summary>
    public string Environment { get; set; } = "sandbox";

    /// <summary>
    /// OAuth 2.0 authorization endpoint.
    /// Per Intuit docs: https://appcenter.intuit.com/connect/oauth2
    /// </summary>
    public string AuthorizationEndpoint { get; set; } = "https://appcenter.intuit.com/connect/oauth2";

    /// <summary>
    /// OAuth 2.0 token endpoint for obtaining and refreshing tokens.
    /// Per Intuit docs: https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer
    /// </summary>
    public string TokenEndpoint { get; set; } = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";

    /// <summary>
    /// OAuth 2.0 revocation endpoint for token revocation.
    /// Per Intuit docs: https://developer.intuit.com/v2/oauth
    /// </summary>
    public string RevokeEndpoint { get; set; } = "https://developer.intuit.com/v2/oauth";

    /// <summary>
    /// Scopes requested during OAuth flow.
    /// Default: ["com.intuit.quickbooks.accounting"]
    /// </summary>
    public IReadOnlyList<string> Scopes { get; set; } = new[] { "com.intuit.quickbooks.accounting" };

    /// <summary>
    /// Local file path to persist cached OAuth tokens.
    /// Used when EnableTokenPersistence is true.
    /// </summary>
    public string? TokenCachePath { get; set; }

    /// <summary>
    /// Enable local persistence of OAuth tokens to avoid re-authentication on app restart.
    /// Tokens are stored encrypted in TokenCachePath.
    /// </summary>
    public bool EnableTokenPersistence { get; set; } = true;

    /// <summary>
    /// Timeout (seconds) for OAuth token requests.
    /// </summary>
    public int TokenRequestTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Token refresh buffer (seconds) to refresh before actual expiry.
    /// Prevents mid-flight token expiration. Default: 300 (5 minutes).
    /// </summary>
    public int TokenExpiryBufferSeconds { get; set; } = 300;

    /// <summary>
    /// Validates that required OAuth configuration is present and non-empty.
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(ClientId) &&
                          !string.IsNullOrWhiteSpace(ClientSecret) &&
                          !string.IsNullOrWhiteSpace(RedirectUri);
}
