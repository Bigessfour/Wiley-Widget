using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Handles QuickBooks OAuth authentication and token management.
/// Extracted from QuickBooksService for better modularity and single responsibility.
/// </summary>
internal sealed class QuickBooksAuthService : IDisposable
{
    private readonly ILogger _logger;
    private readonly ISettingsService _settings;
    private readonly ISecretVaultService? _secretVault;
    private readonly HttpClient _httpClient;
    private readonly IServiceProvider _serviceProvider;

    // Values loaded lazily from secret vault or environment
    private string? _clientId;
    private string? _clientSecret;
    private string _redirectUri = "https://developer.intuit.com/v2/OAuth2Playground/RedirectUrl";
    private string? _realmId;
    private string _environment = "sandbox";

    private volatile bool _initialized;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);

    // Intuit OAuth 2.0 endpoints (per official docs)
    private const string AuthorizationEndpoint = "https://appcenter.intuit.com/connect/oauth2";
    private const string TokenEndpoint = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";

    public QuickBooksAuthService(ISettingsService settings, ISecretVaultService keyVaultService, ILogger logger, HttpClient httpClient, IServiceProvider serviceProvider)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _secretVault = keyVaultService; // may be null in some test contexts
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public void Dispose()
    {
        _initSemaphore.Dispose();
    }

    private static async Task<string?> TryGetFromSecretVaultAsync(ISecretVaultService? keyVaultService, string secretName, ILogger logger)
    {
        if (keyVaultService == null)
        {
            logger.LogDebug("Secret vault service not available for {SecretName}", secretName);
            return null;
        }

        try
        {
            var secretValue = await keyVaultService.GetSecretAsync(secretName).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(secretValue))
            {
                logger.LogInformation("Successfully loaded {SecretName} from secret vault", secretName);
                return secretValue;
            }

            logger.LogDebug("{SecretName} not found in secret vault (not an error, just missing)", secretName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load {SecretName} from secret vault", secretName);
            return null;
        }
    }

    /// <summary>
    /// Retrieve an environment variable from any reasonable scope.
    /// Prefer process-level variables (container-friendly). If not present, attempt
    /// to read user-level variables (Windows) but swallow any platform-specific
    /// exceptions so callers remain cross-platform friendly.
    /// </summary>
    private static string? GetEnvironmentVariableAnyScope(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        try
        {
            var v = Environment.GetEnvironmentVariable(name);
            Console.WriteLine($"[DIAGNOSTIC] GetEnvironmentVariable('{name}') => {(v == null ? "<null>" : "<redacted>")}");
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        catch { /* ignore */ }

        try
        {
            var uv = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
            Console.WriteLine($"[DIAGNOSTIC] GetEnvironmentVariable(User,'{name}') => {(uv == null ? "<null>" : "<redacted>")}");
            return uv;
        }
        catch { return null; }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_initialized) return;

            // Load QBO credentials from secret vault with fallback to environment variables
            var envClientCandidate = GetEnvironmentVariableAnyScope("QBO_CLIENT_ID");
            if (!string.IsNullOrWhiteSpace(envClientCandidate))
            {
                _logger.LogInformation("QBO_CLIENT_ID found in environment (process/user). Using env value for initialization.");
            }
            else
            {
                _logger.LogDebug("QBO_CLIENT_ID not set in environment");
            }

            _clientId = await TryGetFromSecretVaultAsync(_secretVault, "QBO-CLIENT-ID", _logger).ConfigureAwait(false)
                        ?? await TryGetFromSecretVaultAsync(_secretVault, "QuickBooks-ClientId", _logger).ConfigureAwait(false)
                        ?? envClientCandidate
                        ?? GetEnvironmentVariableAnyScope("QUICKBOOKS_CLIENT_ID")
                        ?? throw new InvalidOperationException("QBO_CLIENT_ID not found in the secret vault or environment variables.");

            var envSecretCandidate = GetEnvironmentVariableAnyScope("QBO_CLIENT_SECRET");
            if (!string.IsNullOrWhiteSpace(envSecretCandidate))
            {
                _logger.LogInformation("QBO_CLIENT_SECRET found in environment (process/user). Using env value for initialization.");
            }

            _clientSecret = await TryGetFromSecretVaultAsync(_secretVault, "QBO-CLIENT-SECRET", _logger).ConfigureAwait(false)
                            ?? await TryGetFromSecretVaultAsync(_secretVault, "QuickBooks-ClientSecret", _logger).ConfigureAwait(false)
                            ?? envSecretCandidate
                            ?? GetEnvironmentVariableAnyScope("QUICKBOOKS_CLIENT_SECRET")
                            ?? string.Empty;

            var envRealmCandidate = GetEnvironmentVariableAnyScope("QBO_REALM_ID") ?? GetEnvironmentVariableAnyScope("QUICKBOOKS_REALM_ID");
            if (!string.IsNullOrWhiteSpace(envRealmCandidate))
            {
                _logger.LogInformation("QBO_REALM_ID found in environment (process/user). Using env value for initialization.");
            }

            _realmId = await TryGetFromSecretVaultAsync(_secretVault, "QBO-REALM-ID", _logger).ConfigureAwait(false)
                       ?? await TryGetFromSecretVaultAsync(_secretVault, "QuickBooks-RealmId", _logger).ConfigureAwait(false)
                       ?? envRealmCandidate;

            // Redirect URI is configurable; fall back to default local listener
            var redirectFromVault = await TryGetFromSecretVaultAsync(_secretVault, "QBO-REDIRECT-URI", _logger).ConfigureAwait(false)
                    ?? GetEnvironmentVariableAnyScope("QBO_REDIRECT_URI");
            if (!string.IsNullOrWhiteSpace(redirectFromVault))
            {
                _redirectUri = redirectFromVault!;
            }

            _environment = await TryGetFromSecretVaultAsync(_secretVault, "QBO-ENVIRONMENT", _logger).ConfigureAwait(false)
                       ?? GetEnvironmentVariableAnyScope("QBO_ENVIRONMENT")
                       ?? _environment;

            _logger.LogInformation("QuickBooks auth service initialized - ClientId: {ClientIdPrefix}..., RealmId: {RealmId}, Environment: {Environment}",
                _clientId.Substring(0, Math.Min(8, _clientId.Length)), _realmId, _environment);

            _initialized = true;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    public bool HasValidAccessToken()
    {
        var s = _settings.Current;
        // Consider token valid if set and expires more than 60s from now (renew early to avoid edge expiry in-flight)
        if (string.IsNullOrWhiteSpace(s.QboAccessToken)) return false;
        if (s.QboTokenExpiry == default) return false;
        return s.QboTokenExpiry > DateTime.UtcNow.AddSeconds(60);
    }

    public async Task RefreshTokenIfNeededAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var s = _settings.Current;
        if (HasValidAccessToken()) return;

        if (string.IsNullOrWhiteSpace(s.QboRefreshToken))
        {
            throw new InvalidOperationException("No refresh token available. Please re-authorize the application.");
        }

        await RefreshTokenAsync();
    }

    public async Task RefreshTokenAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var s = _settings.Current;

        try
        {
            var result = await RefreshAccessTokenAsync(s.QboRefreshToken!).ConfigureAwait(false);
            s.QboAccessToken = result.AccessToken;
            s.QboRefreshToken = result.RefreshToken;
            s.QboTokenExpiry = DateTime.UtcNow.AddSeconds(result.ExpiresIn);
            _settings.Save();
            _logger.LogInformation("QBO token refreshed successfully (exp {Expiry}). Reminder: protect tokens at rest in production.", s.QboTokenExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh QBO access token");
            // Clear invalid tokens to force re-authorization
            s.QboAccessToken = null;
            s.QboRefreshToken = null;
            s.QboTokenExpiry = default;
            _settings.Save();
            throw new QuickBooksAuthException("QuickBooks token refresh failed. Please re-authorize the application.", ex);
        }
    }

    private sealed record TokenResult(string AccessToken, string RefreshToken, int ExpiresIn, int RefreshTokenExpiresIn);

    private async Task<TokenResult> RefreshAccessTokenAsync(string refreshToken)
    {
        const int maxRetries = 3;
        var lastException = (Exception?)null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var json = string.Empty;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
                var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_clientId}:{_clientSecret}"));
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basic);
                req.Headers.Accept.ParseAdd("application/json");

                var form = new List<KeyValuePair<string, string>>
                    {
                        new("grant_type", "refresh_token"),
                        new("refresh_token", refreshToken)
                    };
                req.Content = new FormUrlEncodedContent(form);

                using var resp = await _httpClient.SendAsync(req).ConfigureAwait(false);
                json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    var error = $"Intuit token refresh failed ({(int)resp.StatusCode}): {json}";
                    _logger.LogWarning("Token refresh attempt {Attempt}/{MaxRetries} failed: {Error}", attempt, maxRetries, error);

                    // Check if it's a permanent failure (400 Bad Request usually means invalid refresh token)
                    if (resp.StatusCode == HttpStatusCode.BadRequest)
                    {
                        throw new QuickBooksAuthException($"Refresh token is invalid or expired: {json}");
                    }

                    lastException = new HttpRequestException(error);
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))).ConfigureAwait(false);
                        continue;
                    }
                    throw lastException;
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("access_token", out var accessTokenProp) || accessTokenProp.GetString() is not string access)
                {
                    throw new QuickBooksAuthException("Invalid token response: missing access_token");
                }

                var refresh = root.TryGetProperty("refresh_token", out var refreshTokenProp)
                    ? refreshTokenProp.GetString() ?? refreshToken
                    : refreshToken;

                if (!root.TryGetProperty("expires_in", out var expiresProp) || !expiresProp.TryGetInt32(out var expires))
                {
                    throw new QuickBooksAuthException("Invalid token response: missing or invalid expires_in");
                }

                var refreshExpires = root.TryGetProperty("x_refresh_token_expires_in", out var x) && x.TryGetInt32(out var xVal) ? xVal : 0;

                _logger.LogInformation("Token refresh successful on attempt {Attempt}", attempt);
                return new TokenResult(access, refresh, expires, refreshExpires);
            }
            catch (JsonException ex)
            {
                lastException = new QuickBooksAuthException($"Invalid JSON response from token endpoint: {json}", ex);
                _logger.LogWarning(ex, "JSON parsing failed on attempt {Attempt}", attempt);
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "HTTP request failed on attempt {Attempt}", attempt);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogError(ex, "Unexpected error during token refresh on attempt {Attempt}", attempt);
            }

            if (attempt < maxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogInformation("Retrying token refresh in {Delay}", delay);
                await Task.Delay(delay).ConfigureAwait(false);
            }
        }

        throw new QuickBooksAuthException($"Token refresh failed after {maxRetries} attempts", lastException);
    }

    public string GetAccessToken()
    {
        var s = _settings.Current;
        if (!HasValidAccessToken())
        {
            throw new QuickBooksAuthException("Access token invalid – refresh required.");
        }
        return s.QboAccessToken!;
    }

    public string? GetRealmId() => _realmId;
    public string GetEnvironment() => _environment;
}

/// <summary>
/// Exception thrown when QuickBooks authentication fails.
/// </summary>
public class QuickBooksAuthException : Exception
{
    public QuickBooksAuthException(string message) : base(message) { }
    public QuickBooksAuthException(string message, Exception innerException) : base(message, innerException) { }
}
