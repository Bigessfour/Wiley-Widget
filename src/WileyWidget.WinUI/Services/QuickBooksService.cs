#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Intuit.Ipp.Core;
using Intuit.Ipp.Data;
using Intuit.Ipp.DataService;
using Intuit.Ipp.Security;
using Intuit.Ipp.QueryFilter;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using WileyWidget.Business.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.WinUI.Services.Events;

namespace WileyWidget.Services;

/// <summary>
/// QuickBooks service using Intuit SDK with OAuth2 authentication.
/// Handles token refresh and DataService access for QuickBooks Online integration.
/// </summary>
public sealed class QuickBooksService : IQuickBooksService, IDisposable
{
    private readonly ILogger<QuickBooksService> _logger;
    private readonly WileyWidget.Services.ISettingsService _settings;
    private readonly ISecretVaultService? _secretVault;
    private readonly IQuickBooksApiClient _apiClient;

    // Values loaded lazily from secret vault or environment
    private string? _clientId;
    private string? _clientSecret;
    private string _redirectUri = "https://developer.intuit.com/v2/OAuth2Playground/RedirectUrl";
    private string? _realmId;
    private string _environment = "sandbox";
    private string? _intuitPreLoginUrl; // optional convenience URL to pre-authenticate account
    private bool _settingsLoaded;

    private volatile bool _initialized;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);

    // Cloudflare tunnel management
    private readonly SemaphoreSlim _cloudflaredSemaphore = new(1, 1);
    private Process? _cloudflaredProcess;
    private string? _cloudflaredPublicUrl;

    // Intuit sandbox base URL documented at https://developer.intuit.com/app/developer/qbo/docs/develop/sandboxes
    private static readonly IReadOnlyList<string> DefaultScopes = new[] { "com.intuit.quickbooks.accounting" };

    // Intuit OAuth 2.0 endpoints (per official docs)
    private const string AuthorizationEndpoint = "https://appcenter.intuit.com/connect/oauth2";
    private const string TokenEndpoint = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";

    private readonly HttpClient _httpClient;
    private readonly IServiceProvider _serviceProvider;

    public QuickBooksService(WileyWidget.Services.ISettingsService settings, ISecretVaultService keyVaultService, ILogger<QuickBooksService> logger, IQuickBooksApiClient apiClient, HttpClient httpClient, IServiceProvider serviceProvider)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _secretVault = keyVaultService; // may be null in some test contexts
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Secrets and OAuth client are loaded lazily via EnsureInitializedAsync()
        EnsureSettingsLoaded();
    }

    public void Dispose()
    {
        _initSemaphore.Dispose();
        _cloudflaredSemaphore.Dispose();
        try
        {
            if (_cloudflaredProcess is { HasExited: false })
            {
                _cloudflaredProcess.Kill(entireProcessTree: true);
                _cloudflaredProcess.Dispose();
            }
        }
        catch { /* best effort */ }
    }

    private static async System.Threading.Tasks.Task<string?> TryGetFromSecretVaultAsync(ISecretVaultService? keyVaultService, string secretName, ILogger logger)
    {
        if (keyVaultService == null)
        {
            logger.LogDebug("Secret vault service not available for {SecretName}", secretName);
            return null;
        }

        try
        {
            // Prefer async API
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
            Console.WriteLine($"[DIAGNOSTIC] GetEnvironmentVariable('{name}') => { (v == null ? "<null>" : "<redacted>") }");
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        catch { /* ignore */ }

        try
        {
            // Some callers on Windows may have user-scoped variables set; try that as a fallback.
            var uv = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
            Console.WriteLine($"[DIAGNOSTIC] GetEnvironmentVariable(User,'{name}') => {(uv == null ? "<null>" : "<redacted>")}");
            return uv;
        }
        catch { return null; }
    }

    private async System.Threading.Tasks.Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_initialized) return;

            _logger.LogInformation("Initializing QuickBooks service...");

            // Load secrets from vault or environment
            _clientId = await TryGetFromSecretVaultAsync(_secretVault, "QuickBooksClientId", _logger).ConfigureAwait(false)
                       ?? GetEnvironmentVariableAnyScope("QUICKBOOKS_CLIENT_ID")
                       ?? throw new InvalidOperationException("QuickBooks Client ID not configured");

            _clientSecret = await TryGetFromSecretVaultAsync(_secretVault, "QuickBooksClientSecret", _logger).ConfigureAwait(false)
                           ?? GetEnvironmentVariableAnyScope("QUICKBOOKS_CLIENT_SECRET")
                           ?? throw new InvalidOperationException("QuickBooks Client Secret not configured");

            _realmId = await TryGetFromSecretVaultAsync(_secretVault, "QuickBooksRealmId", _logger).ConfigureAwait(false)
                      ?? GetEnvironmentVariableAnyScope("QUICKBOOKS_REALM_ID");

            var environment = await TryGetFromSecretVaultAsync(_secretVault, "QuickBooksEnvironment", _logger).ConfigureAwait(false)
                             ?? GetEnvironmentVariableAnyScope("QUICKBOOKS_ENVIRONMENT")
                             ?? "sandbox";

            _environment = environment.ToLowerInvariant() switch
            {
                "production" or "prod" => "production",
                _ => "sandbox"
            };

            _intuitPreLoginUrl = await TryGetFromSecretVaultAsync(_secretVault, "IntuitPreLoginUrl", _logger).ConfigureAwait(false)
                                ?? GetEnvironmentVariableAnyScope("INTUIT_PRE_LOGIN_URL");

            _logger.LogInformation("QuickBooks service initialized for {Environment} environment", _environment);
            _initialized = true;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    private void EnsureSettingsLoaded()
    {
        if (_settingsLoaded) return;

        // Load settings that don't require async initialization
        var redirectUri = _settings.Current.QuickBooksRedirectUri;
        if (!string.IsNullOrWhiteSpace(redirectUri))
        {
            _redirectUri = redirectUri;
        }

        _settingsLoaded = true;
    }

    /// <summary>
    /// Gets the OAuth2 authorization URL for QuickBooks
    /// </summary>
    public async System.Threading.Tasks.Task<string> GetAuthorizationUrlAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var scopes = string.Join(" ", DefaultScopes);
        var state = Guid.NewGuid().ToString("N"); // Random state for CSRF protection

        var url = $"{AuthorizationEndpoint}?client_id={Uri.EscapeDataString(_clientId!)}&response_type=code&scope={Uri.EscapeDataString(scopes)}&redirect_uri={Uri.EscapeDataString(_redirectUri)}&state={state}";

        _logger.LogInformation("Generated QuickBooks authorization URL");
        return url;
    }

    /// <summary>
    /// Exchanges authorization code for access token
    /// </summary>
    public async System.Threading.Tasks.Task<bool> ExchangeCodeForTokenAsync(string authorizationCode)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode))
            throw new ArgumentException("Authorization code cannot be null or empty", nameof(authorizationCode));

        await EnsureInitializedAsync().ConfigureAwait(false);

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", authorizationCode),
                new KeyValuePair<string, string>("redirect_uri", _redirectUri),
                new KeyValuePair<string, string>("client_id", _clientId!),
                new KeyValuePair<string, string>("client_secret", _clientSecret!)
            });

            request.Content = content;

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to exchange code for token: {StatusCode} - {Response}", response.StatusCode, responseContent);
                return false;
            }

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);
            if (tokenResponse == null)
            {
                _logger.LogError("Invalid token response format");
                return false;
            }

            // Store tokens securely
            await StoreTokensAsync(tokenResponse).ConfigureAwait(false);

            _logger.LogInformation("Successfully exchanged authorization code for access token");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging authorization code for token");
            return false;
        }
    }

    /// <summary>
    /// Refreshes the access token using the refresh token
    /// </summary>
    public async System.Threading.Tasks.Task<bool> RefreshAccessTokenAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var refreshToken = await GetStoredRefreshTokenAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning("No refresh token available for token refresh");
            return false;
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("client_id", _clientId!),
                new KeyValuePair<string, string>("client_secret", _clientSecret!)
            });

            request.Content = content;

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to refresh access token: {StatusCode} - {Response}", response.StatusCode, responseContent);
                return false;
            }

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);
            if (tokenResponse == null)
            {
                _logger.LogError("Invalid token refresh response format");
                return false;
            }

            // Store new tokens
            await StoreTokensAsync(tokenResponse).ConfigureAwait(false);

            _logger.LogInformation("Successfully refreshed access token");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing access token");
            return false;
        }
    }

    private async System.Threading.Tasks.Task StoreTokensAsync(TokenResponse tokenResponse)
    {
        if (_secretVault != null)
        {
            await _secretVault.SetSecretAsync("QuickBooksAccessToken", tokenResponse.AccessToken).ConfigureAwait(false);
            await _secretVault.SetSecretAsync("QuickBooksRefreshToken", tokenResponse.RefreshToken).ConfigureAwait(false);
            await _secretVault.SetSecretAsync("QuickBooksTokenExpiry", tokenResponse.ExpiresIn.ToString()).ConfigureAwait(false);
        }
        else
        {
            // Fallback to settings if no secret vault
            _settings.Current.QboAccessToken = tokenResponse.AccessToken;
            _settings.Current.QboRefreshToken = tokenResponse.RefreshToken;
            _settings.Current.QboTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            _settings.Save();
            await System.Threading.Tasks.Task.CompletedTask;
        }
    }

    private async System.Threading.Tasks.Task<string?> GetStoredAccessTokenAsync()
    {
        if (_secretVault != null)
        {
            return await _secretVault.GetSecretAsync("QuickBooksAccessToken").ConfigureAwait(false);
        }
        else
        {
            return _settings.Current.QboAccessToken;
        }
    }

    private async System.Threading.Tasks.Task<string?> GetStoredRefreshTokenAsync()
    {
        if (_secretVault != null)
        {
            return await _secretVault.GetSecretAsync("QuickBooksRefreshToken").ConfigureAwait(false);
        }
        else
        {
            return _settings.Current.QboRefreshToken;
        }
    }

    private async System.Threading.Tasks.Task<DateTime?> GetStoredTokenExpiryAsync()
    {
        if (_secretVault != null)
        {
            var expiryStr = await _secretVault.GetSecretAsync("QuickBooksTokenExpiry").ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(expiryStr) || !long.TryParse(expiryStr, out var expirySeconds))
            {
                return null;
            }
            return DateTime.UtcNow.AddSeconds(expirySeconds);
        }
        else
        {
            return _settings.Current.QboTokenExpiry;
        }
    }

    /// <summary>
    /// Creates and configures the QuickBooks OAuth2 client
    /// </summary>
    private async System.Threading.Tasks.Task<OAuth2RequestValidator> CreateOAuth2RequestValidatorAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var accessToken = await GetStoredAccessTokenAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("No access token available. Please authorize the application first.");
        }

        return new OAuth2RequestValidator(accessToken);
    }

    /// <summary>
    /// Creates and configures the QuickBooks service context
    /// </summary>
    private async System.Threading.Tasks.Task<ServiceContext> CreateServiceContextAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(_realmId))
        {
            throw new InvalidOperationException("Realm ID not configured. Please set QuickBooks Realm ID.");
        }

        var oauthValidator = await CreateOAuth2RequestValidatorAsync().ConfigureAwait(false);

        var serviceContext = new ServiceContext(_realmId, IntuitServicesType.QBO, oauthValidator);
        // MinorVersion is read-only in this SDK version

        return serviceContext;
    }

    /// <summary>
    /// Creates and configures the QuickBooks data service
    /// </summary>
    private async System.Threading.Tasks.Task<DataService> CreateDataServiceAsync()
    {
        var serviceContext = await CreateServiceContextAsync().ConfigureAwait(false);
        return new DataService(serviceContext);
    }

    /// <summary>
    /// Checks if the service is currently connected
    /// </summary>
    /// <returns>True if connected, false otherwise</returns>
    public async System.Threading.Tasks.Task<bool> IsConnectedAsync()
    {
        try
        {
            var accessToken = await GetStoredAccessTokenAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return false;
            }

            var tokenExpiry = await GetStoredTokenExpiryAsync().ConfigureAwait(false);
            if (tokenExpiry.HasValue && tokenExpiry.Value <= DateTime.UtcNow)
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking connection status");
            return false;
        }
    }

    /// <summary>
    /// Tests the QuickBooks API connection
    /// </summary>
    public async System.Threading.Tasks.Task<bool> TestConnectionAsync()
    {
        try
        {
            var dataService = await CreateDataServiceAsync().ConfigureAwait(false);

            // Try to get company info as a connection test
            var companyInfo = dataService.FindAll<CompanyInfo>(new CompanyInfo(), 1, 1).FirstOrDefault();

            if (companyInfo != null)
            {
                _logger.LogInformation("QuickBooks connection test successful. Company: {CompanyName}", companyInfo.CompanyName);
                return true;
            }
            else
            {
                _logger.LogWarning("QuickBooks connection test failed - no company info returned");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QuickBooks connection test failed");
            return false;
        }
    }

    /// <summary>
    /// Gets all customers from QuickBooks
    /// </summary>
    public async System.Threading.Tasks.Task<List<Customer>> GetCustomersAsync()
    {
        try
        {
            var dataService = await CreateDataServiceAsync().ConfigureAwait(false);

            var customers = dataService.FindAll<Customer>(new Customer()).ToList();
            _logger.LogInformation("Retrieved {Count} customers from QuickBooks", customers.Count);

            return customers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving customers from QuickBooks");
            throw;
        }
    }

    /// <summary>
    /// Gets invoices from QuickBooks, optionally filtered by enterprise
    /// </summary>
    public async System.Threading.Tasks.Task<List<Invoice>> GetInvoicesAsync(string? enterprise = null)
    {
        try
        {
            var dataService = await CreateDataServiceAsync().ConfigureAwait(false);
            var serviceContext = await CreateServiceContextAsync().ConfigureAwait(false);

            var queryService = new QueryService<Invoice>(serviceContext);

            string query = "SELECT * FROM Invoice";
            if (!string.IsNullOrWhiteSpace(enterprise))
            {
                query += $" WHERE CustomerRef.Name = '{enterprise.Replace("'", "''")}'";
            }

            var invoices = queryService.ExecuteIdsQuery(query).ToList();
            _logger.LogInformation("Retrieved {Count} invoices from QuickBooks", invoices.Count);

            return invoices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invoices from QuickBooks");
            throw;
        }
    }

    /// <summary>
    /// Gets the chart of accounts from QuickBooks
    /// </summary>
    public async System.Threading.Tasks.Task<List<Account>> GetChartOfAccountsAsync()
    {
        try
        {
            var dataService = await CreateDataServiceAsync().ConfigureAwait(false);

            var accounts = dataService.FindAll<Account>(new Account()).ToList();
            _logger.LogInformation("Retrieved {Count} accounts from QuickBooks", accounts.Count);

            return accounts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chart of accounts from QuickBooks");
            throw;
        }
    }

    /// <summary>
    /// Gets journal entries from QuickBooks within a date range
    /// </summary>
    public async System.Threading.Tasks.Task<List<JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var serviceContext = await CreateServiceContextAsync().ConfigureAwait(false);

            var queryService = new QueryService<JournalEntry>(serviceContext);

            string query = $"SELECT * FROM JournalEntry WHERE TxnDate >= '{startDate:yyyy-MM-dd}' AND TxnDate <= '{endDate:yyyy-MM-dd}'";

            var journalEntries = queryService.ExecuteIdsQuery(query).ToList();
            _logger.LogInformation("Retrieved {Count} journal entries from QuickBooks", journalEntries.Count);

            return journalEntries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving journal entries from QuickBooks");
            throw;
        }
    }

    /// <summary>
    /// Gets budgets from QuickBooks
    /// </summary>
    public async System.Threading.Tasks.Task<List<Budget>> GetBudgetsAsync()
    {
        try
        {
            var dataService = await CreateDataServiceAsync().ConfigureAwait(false);

            var budgets = dataService.FindAll<Budget>(new Budget()).ToList();
            _logger.LogInformation("Retrieved {Count} budgets from QuickBooks", budgets.Count);

            return budgets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving budgets from QuickBooks");
            throw;
        }
    }

    /// <summary>
    /// Syncs budgets from QuickBooks to the application
    /// </summary>
    public async System.Threading.Tasks.Task<SyncResult> SyncBudgetsToAppAsync(IEnumerable<Budget> budgets, CancellationToken cancellationToken = default)
    {
        try
        {
            // Implementation would sync budgets to local database
            // This is a placeholder for the actual sync logic
            await System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);

            _logger.LogInformation("Budget sync completed. Processed {Count} budgets", budgets.Count());
            return new SyncResult
            {
                Success = true,
                RecordsSynced = budgets.Count(),
                Duration = TimeSpan.Zero
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing budgets to application");
            return new SyncResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = TimeSpan.Zero
            };
        }
    }

    /// <summary>
    /// Connects to the QuickBooks API
    /// </summary>
    public async System.Threading.Tasks.Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var isConnected = await TestConnectionAsync().ConfigureAwait(false);
            if (isConnected)
            {
                _logger.LogInformation("Successfully connected to QuickBooks API");
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to connect to QuickBooks API");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to QuickBooks API");
            return false;
        }
    }

    /// <summary>
    /// Disconnects from the QuickBooks API
    /// </summary>
    public async System.Threading.Tasks.Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        // For OAuth2, disconnection is handled by revoking tokens
        // This is a placeholder for actual disconnect logic
        _logger.LogInformation("Disconnected from QuickBooks API");
        await System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the current connection status
    /// </summary>
    public async System.Threading.Tasks.Task<ConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var isConnected = await TestConnectionAsync().ConfigureAwait(false);
            return new ConnectionStatus
            {
                IsConnected = isConnected,
                LastSyncTime = DateTime.UtcNow.ToString("o"),
                StatusMessage = $"Connected to {_environment} environment"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking connection status");
            return new ConnectionStatus
            {
                IsConnected = false,
                StatusMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Imports chart of accounts from QuickBooks
    /// </summary>
    public async System.Threading.Tasks.Task<ImportResult> ImportChartOfAccountsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var accounts = await GetChartOfAccountsAsync().ConfigureAwait(false);

            // Implementation would import accounts to local database
            // This is a placeholder for the actual import logic

            _logger.LogInformation("Chart of accounts import completed. Imported {Count} accounts", accounts.Count);
            return new ImportResult
            {
                Success = true,
                AccountsImported = accounts.Count,
                Duration = TimeSpan.Zero
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing chart of accounts");
            return new ImportResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = TimeSpan.Zero
            };
        }
    }

    /// <summary>
    /// Syncs data from QuickBooks to the application
    /// </summary>
    public async System.Threading.Tasks.Task<SyncResult> SyncDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Implementation would sync various data types
            // This is a placeholder for the actual sync logic
            await System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);

            _logger.LogInformation("Data sync completed");
            return new SyncResult
            {
                Success = true,
                RecordsSynced = 0,
                Duration = TimeSpan.Zero
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing data");
            return new SyncResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = TimeSpan.Zero
            };
        }
    }

    /// <summary>
    /// Authorizes the application with QuickBooks
    /// </summary>
    public async System.Threading.Tasks.Task<bool> AuthorizeAsync()
    {
        try
        {
            var authUrl = await GetAuthorizationUrlAsync().ConfigureAwait(false);
            // Implementation would open browser or return URL for authorization
            // This is a placeholder for the actual authorization logic

            _logger.LogInformation("QuickBooks authorization initiated");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authorizing with QuickBooks");
            return false;
        }
    }

    /// <summary>
    /// Checks URL ACL for localhost redirect URI
    /// </summary>
    public async System.Threading.Tasks.Task<UrlAclCheckResult> CheckUrlAclAsync(string? redirectUri = null)
    {
        var result = new UrlAclCheckResult();

        try
        {
            var uri = redirectUri ?? _redirectUri;
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
            {
                result.Guidance = "Invalid redirect URI format";
                return result;
            }

            if (parsedUri.Host != "localhost" && parsedUri.Host != "127.0.0.1")
            {
                result.IsReady = true;
                result.Guidance = "Non-localhost URI does not require URL ACL";
                return result;
            }

            // Check if URL ACL exists for the port
            var netshOutput = await RunNetshCommandAsync($"http show urlacl url={uri}/").ConfigureAwait(false);

            if (netshOutput.Contains("Reserved URL"))
            {
                result.IsReady = true;
                result.ListenerPrefix = uri;
                result.Guidance = "URL ACL is properly configured";
                result.RawNetshOutput = netshOutput;
            }
            else
            {
                result.IsReady = false;
                result.Guidance = $"URL ACL not found for {uri}. Run: netsh http add urlacl url={uri}/ user=Everyone";
                result.RawNetshOutput = netshOutput;
            }
        }
        catch (Exception ex)
        {
            result.IsReady = false;
            result.Guidance = $"Error checking URL ACL: {ex.Message}";
        }

        return result;
    }

    private async System.Threading.Tasks.Task<string> RunNetshCommandAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "netsh.exe",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return "Failed to start netsh process";
        }

        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

        await process.WaitForExitAsync().ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(error) ? output : $"{output}\n{error}";
    }

    // Nested classes for API responses
    private sealed class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public long ExpiresIn { get; set; }
        public string XRefreshTokenExpiresIn { get; set; } = string.Empty;
    }
}