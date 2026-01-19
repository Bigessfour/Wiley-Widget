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
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Handles QuickBooks OAuth authentication and token management with production-grade resilience.
/// Implements Polly v8 patterns for timeout, circuit breaker, and retry with jitter.
/// Intuit API Spec: https://developer.intuit.com/app/developer/qbo/docs/auth/oauth2
/// </summary>
public sealed class QuickBooksAuthService : IQuickBooksAuthService, IDisposable
{
    private readonly ILogger _logger;
    private readonly ISettingsService _settings;
    private readonly ISecretVaultService? _secretVault;
    private readonly HttpClient _httpClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly QuickBooksTokenStore? _tokenStore;
    private readonly QuickBooksOAuthOptions? _oauthOptions;

    // Resilience pipelines
    private readonly ResiliencePipeline<Abstractions.TokenResult> _tokenRefreshPipeline;
    private readonly ActivitySource _activitySource = new("WileyWidget.Services.QuickBooksAuthService");

    // Values loaded lazily from secret vault or environment
    private string? _clientId;
    private string? _clientSecret;
    private string? _redirectUri;
    private string? _realmId;
    private string _environment = "sandbox";

    // Token expiry buffer: 5 minutes to prevent mid-flight expiry
    private const int TokenExpiryBufferSeconds = 300;
    private const int TokenRefreshTimeoutSeconds = 15;
    private const int MaxTokenRefreshRetries = 5;
    private const double CircuitBreakerFailureRatio = 0.7;

    private volatile bool _initialized;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);

    // Intuit OAuth 2.0 endpoints (per official docs)
    private const string AuthorizationEndpoint = "https://appcenter.intuit.com/connect/oauth2";
    private const string TokenEndpoint = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";

    public QuickBooksAuthService(
        ISettingsService settings,
        ISecretVaultService keyVaultService,
        ILogger logger,
        HttpClient httpClient,
        IServiceProvider serviceProvider,
        QuickBooksTokenStore? tokenStore = null,
        Microsoft.Extensions.Options.IOptions<QuickBooksOAuthOptions>? oauthOptions = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _secretVault = keyVaultService; // may be null in some test contexts
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _tokenStore = tokenStore;
        _oauthOptions = oauthOptions?.Value;

        // Initialize Polly v8 resilience pipeline for token refresh
        _tokenRefreshPipeline = BuildTokenRefreshPipeline();
    }

    public void Dispose()
    {
        _initSemaphore.Dispose();
        _activitySource.Dispose();
    }

    /// <summary>
    /// Builds the resilience pipeline for token refresh operations.
    /// Stack order (outer to inner): Timeout → CircuitBreaker → Retry
    /// </summary>
    private ResiliencePipeline<Abstractions.TokenResult> BuildTokenRefreshPipeline()
    {
        return new ResiliencePipelineBuilder<Abstractions.TokenResult>()
            // Outermost: Timeout prevents indefinite hangs
            .AddTimeout(TimeSpan.FromSeconds(TokenRefreshTimeoutSeconds))

            // Middle: Circuit breaker prevents hammering Intuit on persistent failures
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<Abstractions.TokenResult>
            {
                FailureRatio = CircuitBreakerFailureRatio,  // Open on 70% failures
                BreakDuration = TimeSpan.FromMinutes(5),     // Wait 5 min before retry
                MinimumThroughput = 2,                       // After 2 requests
                SamplingDuration = TimeSpan.FromSeconds(30), // 30-second sample window
                ShouldHandle = new PredicateBuilder<Abstractions.TokenResult>()
                    .HandleResult(r => r == null)
                    .Handle<HttpRequestException>()
                    .Handle<JsonException>()
                    .Handle<InvalidOperationException>(),
                OnOpened = args =>
                {
                    _logger.LogError(
                        "QuickBooks token refresh circuit breaker OPEN - Intuit service unavailable. " +
                        "Will retry in {BreakDuration}",
                        args.BreakDuration);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("QuickBooks token refresh circuit breaker CLOSED");
                    return ValueTask.CompletedTask;
                }
            })

            // Innermost: Retry handles transient failures with exponential backoff and jitter
            .AddRetry(new RetryStrategyOptions<TokenResult>
            {
                MaxRetryAttempts = MaxTokenRefreshRetries,
                Delay = TimeSpan.FromMilliseconds(500),     // Start with 500ms
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,                           // Prevent thundering herd
                ShouldHandle = new PredicateBuilder<TokenResult>()
                    .Handle<HttpRequestException>(ex =>
                        ex.InnerException is TimeoutException ||
                        ex.InnerException is IOException ||
                        ex.StatusCode == HttpStatusCode.ServiceUnavailable ||
                        ex.StatusCode == HttpStatusCode.GatewayTimeout)
                    .Handle<TimeoutException>()
                    .Handle<OperationCanceledException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Token refresh retry {Attempt}/{MaxAttempts} after {Delay}ms delay. " +
                        "Reason: {Exception}",
                        args.AttemptNumber + 1,
                        MaxTokenRefreshRetries,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    private static async Task<string?> TryGetFromSecretVaultAsync(ISecretVaultService? keyVaultService,
        string secretName,
        ILogger logger, CancellationToken cancellationToken = default)
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
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        catch { /* ignore */ }

        try
        {
            var uv = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
            return uv;
        }
        catch { return null; }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
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
                _logger.LogInformation("QBO_CLIENT_ID found in environment (process/user). Using env value.");
            }

            _clientId = await TryGetFromSecretVaultAsync(_secretVault, "QBO-CLIENT-ID", _logger).ConfigureAwait(false)
                        ?? await TryGetFromSecretVaultAsync(_secretVault, "QuickBooks-ClientId", _logger).ConfigureAwait(false)
                        ?? envClientCandidate
                        ?? GetEnvironmentVariableAnyScope("QUICKBOOKS_CLIENT_ID")
                        ?? throw new InvalidOperationException("QBO_CLIENT_ID not found in the secret vault or environment variables.");

            var envSecretCandidate = GetEnvironmentVariableAnyScope("QBO_CLIENT_SECRET");
            if (!string.IsNullOrWhiteSpace(envSecretCandidate))
            {
                _logger.LogInformation("QBO_CLIENT_SECRET found in environment (process/user). Using env value.");
            }

            _clientSecret = await TryGetFromSecretVaultAsync(_secretVault, "QBO-CLIENT-SECRET", _logger).ConfigureAwait(false)
                            ?? await TryGetFromSecretVaultAsync(_secretVault, "QuickBooks-ClientSecret", _logger).ConfigureAwait(false)
                            ?? envSecretCandidate
                            ?? GetEnvironmentVariableAnyScope("QUICKBOOKS_CLIENT_SECRET")
                            ?? string.Empty;

            var envRealmCandidate = GetEnvironmentVariableAnyScope("QBO_REALM_ID") ?? GetEnvironmentVariableAnyScope("QUICKBOOKS_REALM_ID");
            if (!string.IsNullOrWhiteSpace(envRealmCandidate))
            {
                _logger.LogInformation("QBO_REALM_ID found in environment (process/user). Using env value.");
            }

            _realmId = await TryGetFromSecretVaultAsync(_secretVault, "QBO-REALM-ID", _logger).ConfigureAwait(false)
                       ?? await TryGetFromSecretVaultAsync(_secretVault, "QuickBooks-RealmId", _logger).ConfigureAwait(false)
                       ?? envRealmCandidate;

            var redirectFromVault = await TryGetFromSecretVaultAsync(_secretVault, "QBO-REDIRECT-URI", _logger).ConfigureAwait(false)
                        ?? GetEnvironmentVariableAnyScope("QBO_REDIRECT_URI");
            if (!string.IsNullOrWhiteSpace(redirectFromVault))
            {
                _redirectUri = redirectFromVault!;
            }

            _environment = await TryGetFromSecretVaultAsync(_secretVault, "QBO-ENVIRONMENT", _logger).ConfigureAwait(false)
                       ?? GetEnvironmentVariableAnyScope("QBO_ENVIRONMENT")
                       ?? _environment;

            _logger.LogInformation(
                "QuickBooks auth service initialized - ClientId: {ClientIdPrefix}..., RealmId: {RealmId}, Environment: {Environment}",
                _clientId.Substring(0, Math.Min(8, _clientId.Length)), _realmId ?? "<not set>", _environment);

            _initialized = true;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    /// <summary>
    /// Checks if the current access token is still valid.
    /// Uses a 5-minute safety buffer to prevent mid-flight expiry.
    /// </summary>
    public bool HasValidAccessToken()
    {
        var s = _settings.Current;
        if (string.IsNullOrWhiteSpace(s.QboAccessToken)) return false;
        if (s.QboTokenExpiry == default) return false;

        var bufferTime = DateTime.UtcNow.AddSeconds(TokenExpiryBufferSeconds);
        return s.QboTokenExpiry > bufferTime;
    }

    /// <inheritdoc/>
    public async Task<QuickBooksOAuthToken?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Use token store if available
        if (_tokenStore != null)
        {
            var token = await _tokenStore.GetTokenAsync().ConfigureAwait(false);
            if (token?.IsValid == true && !token.IsExpiredOrSoonToExpire(_oauthOptions?.TokenExpiryBufferSeconds ?? TokenExpiryBufferSeconds))
            {
                _logger.LogDebug("Returning cached access token");
                return token;
            }

            // Attempt refresh if token is expired
            if (token?.RefreshToken != null && !token.IsRefreshTokenExpired)
            {
                _logger.LogInformation("Token expired, attempting refresh");
                var result = await RefreshTokenAsync(token.RefreshToken, cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess && result.Token != null)
                {
                    await _tokenStore.SaveTokenAsync(result.Token).ConfigureAwait(false);
                    return result.Token;
                }
            }
        }

        // Fallback to settings-based token
        if (HasValidAccessToken())
        {
            return new QuickBooksOAuthToken
            {
                AccessToken = _settings.Current.QboAccessToken,
                RefreshToken = _settings.Current.QboRefreshToken,
                TokenType = "Bearer",
                IssuedAtUtc = DateTime.UtcNow
            };
        }

        _logger.LogWarning("No valid access token available");
        return null;
    }

    /// <inheritdoc/>
    public async Task<Abstractions.TokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Abstractions.TokenResult.Failure("Refresh token is null or empty");
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

            using var activity = _activitySource.StartActivity("RefreshToken");
            activity?.SetTag("realm_id", _realmId);

            var result = await PerformTokenRefreshAsync(refreshToken).ConfigureAwait(false);

            if (result?.IsSuccess != true)
            {
                return Abstractions.TokenResult.Failure(result?.ErrorMessage ?? "Token refresh failed");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed: {Message}", ex.Message);
            return Abstractions.TokenResult.Failure("Token refresh failed", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Abstractions.TokenResult> ExchangeCodeForTokenAsync(string authorizationCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode))
        {
            return Abstractions.TokenResult.Failure("Authorization code is null or empty");
        }

        if (_oauthOptions == null || !_oauthOptions.IsValid)
        {
            return Abstractions.TokenResult.Failure("QuickBooks OAuth configuration is not valid");
        }

        try
        {
            var requestContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", authorizationCode),
                new KeyValuePair<string, string>("client_id", _oauthOptions.ClientId!),
                new KeyValuePair<string, string>("client_secret", _oauthOptions.ClientSecret!)
            });

            var request = new HttpRequestMessage(HttpMethod.Post, _oauthOptions.TokenEndpoint)
            {
                Content = requestContent
            };

            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var tokenResponse = JsonSerializer.Deserialize<QuickBooksTokenResponse>(json);

            if (tokenResponse == null)
            {
                return Abstractions.TokenResult.Failure("Failed to parse token response");
            }

            var token = tokenResponse.ToOAuthToken();
            if (!token.IsValid)
            {
                return Abstractions.TokenResult.Failure("Token response validation failed");
            }

            _logger.LogInformation("Successfully exchanged authorization code for tokens");
            return Abstractions.TokenResult.Success(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authorization code exchange failed: {Message}", ex.Message);
            return Abstractions.TokenResult.Failure("Authorization code exchange failed", ex);
        }
    }

    /// <inheritdoc/>
    public string GenerateAuthorizationUrl()
    {
        if (_oauthOptions == null || !_oauthOptions.IsValid)
        {
            throw new InvalidOperationException("QuickBooks OAuth configuration is not valid");
        }

        var state = Guid.NewGuid().ToString("N");
        var scopes = string.Join(" ", _oauthOptions.Scopes);

        var queryParams = new System.Collections.Generic.List<string>
        {
            $"client_id={Uri.EscapeDataString(_oauthOptions.ClientId!)}",
            $"response_type=code",
            $"scope={Uri.EscapeDataString(scopes)}",
            $"redirect_uri={Uri.EscapeDataString(_oauthOptions.RedirectUri!)}",
            $"state={Uri.EscapeDataString(state)}"
        };

        var authUrl = $"{_oauthOptions.AuthorizationEndpoint}?{string.Join("&", queryParams)}";
        _logger.LogDebug("Generated OAuth authorization URL with state: {State}", state);

        return authUrl;
    }

    /// <inheritdoc/>
    public async Task RevokeTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (token == null)
            {
                _logger.LogInformation("No token to revoke");
                return;
            }

            if (_oauthOptions == null)
            {
                _logger.LogWarning("OAuth options not configured, cannot revoke token");
                return;
            }

            // Revoke via Intuit endpoint
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", token.RefreshToken ?? token.AccessToken!),
                new KeyValuePair<string, string>("client_id", _oauthOptions.ClientId!),
                new KeyValuePair<string, string>("client_secret", _oauthOptions.ClientSecret!)
            });

            var response = await _httpClient.PostAsync(new Uri(_oauthOptions.RevokeEndpoint), content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // Clear local token
            if (_tokenStore != null)
            {
                await _tokenStore.ClearTokenAsync().ConfigureAwait(false);
            }

            _settings.Current.QboAccessToken = null;
            _settings.Current.QboRefreshToken = null;
            _settings.Current.QboTokenExpiry = default;
            _settings.Save();

            _logger.LogInformation("OAuth token revoked successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke OAuth token: {Message}", ex.Message);
            // Continue despite error - clear local state anyway
            if (_tokenStore != null)
            {
                await _tokenStore.ClearTokenAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<QuickBooksOAuthToken?> GetCurrentTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_tokenStore != null)
        {
            var token = await _tokenStore.GetTokenAsync().ConfigureAwait(false);
            if (token != null)
            {
                return token;
            }
        }

        if (HasValidAccessToken())
        {
            return new QuickBooksOAuthToken
            {
                AccessToken = _settings.Current.QboAccessToken,
                RefreshToken = _settings.Current.QboRefreshToken,
                TokenType = "Bearer",
                IssuedAtUtc = DateTime.UtcNow
            };
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<bool> HasValidTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        return token?.IsValid == true && !token.IsExpired;
    }

    public async Task RefreshTokenIfNeededAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var s = _settings.Current;
        if (HasValidAccessToken()) return;

        if (string.IsNullOrWhiteSpace(s.QboRefreshToken))
        {
            throw new InvalidOperationException(
                "No refresh token available. Please re-authorize the application.");
        }

        await RefreshTokenAsync(s.QboRefreshToken, cancellationToken);
    }

    /// <summary>
    /// Performs the actual HTTP token refresh against Intuit API.
    /// Called within the resilience pipeline context.
    /// </summary>
    private async Task<TokenResult> PerformTokenRefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
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
        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            var error = $"Intuit token refresh failed ({(int)resp.StatusCode}): {json}";
            _logger.LogWarning("Token refresh HTTP error: {StatusCode} {Response}", resp.StatusCode, json);

            // Check if it's a permanent failure
            if (resp.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new InvalidOperationException(
                    $"Refresh token is invalid or expired. Please re-authorize.");
            }

            throw new HttpRequestException(error);
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("access_token", out var accessTokenProp) ||
            accessTokenProp.GetString() is not string access ||
            string.IsNullOrEmpty(access))
        {
            throw new InvalidOperationException("Invalid token response: missing or empty access_token");
        }

        if (!root.TryGetProperty("expires_in", out var expiresProp) ||
            !expiresProp.TryGetInt32(out var expires) ||
            expires <= 0)
        {
            throw new InvalidOperationException("Invalid token response: missing or invalid expires_in");
        }

        // IMPORTANT: Always use the new refresh token if Intuit provides one
        // Intuit may rotate the refresh token on each refresh
        var refresh = refreshToken;  // default to current
        if (root.TryGetProperty("refresh_token", out var refreshTokenProp))
        {
            var newRefresh = refreshTokenProp.GetString();
            if (!string.IsNullOrEmpty(newRefresh))
            {
                refresh = newRefresh;
                _logger.LogInformation("Refresh token rotated by Intuit");
            }
        }

        var refreshExpires = root.TryGetProperty("x_refresh_token_expires_in", out var x) &&
                            x.TryGetInt32(out var xVal) ? xVal : 0;

        return Abstractions.TokenResult.Success(access, refresh, expires);
    }

    public string GetAccessToken()
    {
        var s = _settings.Current;
        if (!HasValidAccessToken())
        {
            throw new QuickBooksAuthException(
                "Access token invalid or expired – refresh required.");
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
    public QuickBooksAuthException(string message, Exception innerException)
        : base(message, innerException) { }
}
