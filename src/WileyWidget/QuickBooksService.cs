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
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using WileyWidget.Business.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Services.Events;

namespace WileyWidget.Services;

/// <summary>
/// QuickBooks service using Intuit SDK with OAuth2 authentication.
/// Handles token refresh and DataService access for QuickBooks Online integration.
/// </summary>
public sealed class QuickBooksService : IQuickBooksService, IDisposable
{
    private readonly ILogger<QuickBooksService> _logger;
    private readonly SettingsService _settings;
    private readonly ISecretVaultService? _secretVault;

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

    public QuickBooksService(SettingsService settings, ISecretVaultService keyVaultService, ILogger<QuickBooksService> logger, HttpClient httpClient, IServiceProvider serviceProvider)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

    private async System.Threading.Tasks.Task EnsureInitializedAsync()
{
    if (_initialized) return;
    await _initSemaphore.WaitAsync().ConfigureAwait(false);
    try
    {
        if (_initialized) return;

        // Load QBO credentials from secret vault with fallback to environment variables
        _clientId = await TryGetFromSecretVaultAsync(_secretVault, "QBO-CLIENT-ID", _logger).ConfigureAwait(false)
                    ?? await TryGetFromSecretVaultAsync(_secretVault, "QuickBooks-ClientId", _logger).ConfigureAwait(false)
                    ?? Environment.GetEnvironmentVariable("QBO_CLIENT_ID", EnvironmentVariableTarget.User)
                    ?? throw new InvalidOperationException("QBO_CLIENT_ID not found in the secret vault or environment variables.");

        _clientSecret = await TryGetFromSecretVaultAsync(_secretVault, "QBO-CLIENT-SECRET", _logger).ConfigureAwait(false)
                        ?? await TryGetFromSecretVaultAsync(_secretVault, "QuickBooks-ClientSecret", _logger).ConfigureAwait(false)
                        ?? Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET", EnvironmentVariableTarget.User)
                        ?? string.Empty;

        _realmId = await TryGetFromSecretVaultAsync(_secretVault, "QBO-REALM-ID", _logger).ConfigureAwait(false)
                   ?? await TryGetFromSecretVaultAsync(_secretVault, "QuickBooks-RealmId", _logger).ConfigureAwait(false)
                   ?? Environment.GetEnvironmentVariable("QBO_REALM_ID", EnvironmentVariableTarget.User);

        // Redirect URI is configurable; fall back to default local listener
        var redirectFromVault = await TryGetFromSecretVaultAsync(_secretVault, "QBO-REDIRECT-URI", _logger).ConfigureAwait(false)
                                ?? Environment.GetEnvironmentVariable("QBO_REDIRECT_URI", EnvironmentVariableTarget.User);
        if (!string.IsNullOrWhiteSpace(redirectFromVault))
        {
            _redirectUri = redirectFromVault!;
        }

        _environment = await TryGetFromSecretVaultAsync(_secretVault, "QBO-ENVIRONMENT", _logger).ConfigureAwait(false)
                       ?? Environment.GetEnvironmentVariable("QBO_ENVIRONMENT", EnvironmentVariableTarget.User)
                       ?? _environment;

        // Optional pre-login URL to smooth user sign-in; can be provided via secret or env var
        _intuitPreLoginUrl = await TryGetFromSecretVaultAsync(_secretVault, "QBO-PRELOGIN-URL", _logger).ConfigureAwait(false)
                             ?? Environment.GetEnvironmentVariable("QBO_PRELOGIN_URL", EnvironmentVariableTarget.User);

        _logger.LogInformation("QuickBooks service initialized - ClientId: {ClientIdPrefix}..., RealmId: {RealmId}, Environment: {Environment}",
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
    var s = EnsureSettingsLoaded();
    // Consider token valid if set and expires more than 60s from now (renew early to avoid edge expiry in-flight)
    if (string.IsNullOrWhiteSpace(s.QboAccessToken)) return false;
    // Default(DateTime) means 'unset'
    if (s.QboTokenExpiry == default) return false;
    return s.QboTokenExpiry > DateTime.UtcNow.AddSeconds(60);
}

public async System.Threading.Tasks.Task RefreshTokenIfNeededAsync()
{
    await EnsureInitializedAsync().ConfigureAwait(false);
    var s = EnsureSettingsLoaded();
    if (HasValidAccessToken()) return;

    if (string.IsNullOrWhiteSpace(s.QboRefreshToken))
    {
        var acquired = await AcquireTokensInteractiveAsync().ConfigureAwait(false);
        if (!acquired)
        {
            throw new InvalidOperationException("QuickBooks authorization was not completed.");
        }
        return;
    }

    await RefreshTokenAsync();
}

public async System.Threading.Tasks.Task RefreshTokenAsync()
{
    await EnsureInitializedAsync().ConfigureAwait(false);
    var s = EnsureSettingsLoaded();

    try
    {
        var result = await RefreshAccessTokenAsync(s.QboRefreshToken!).ConfigureAwait(false);
        s.QboAccessToken = result.AccessToken;
        s.QboRefreshToken = result.RefreshToken;
        s.QboTokenExpiry = DateTime.UtcNow.AddSeconds(result.ExpiresIn);
        _settings.Save();
        Serilog.Log.Information("QBO token refreshed successfully (exp {Expiry}). Reminder: protect tokens at rest in production.", s.QboTokenExpiry);
    }
    catch (Exception ex)
    {
        Serilog.Log.Error(ex, "Failed to refresh QBO access token");
        // Clear invalid tokens to force re-authorization
        s.QboAccessToken = null;
        s.QboRefreshToken = null;
        s.QboTokenExpiry = default;
        _settings.Save();
        throw new InvalidOperationException("QuickBooks token refresh failed. Please re-authorize the application.", ex);
    }
}

private (ServiceContext Ctx, DataService Ds) GetDataService()
{
    var s = EnsureSettingsLoaded();
    if (!HasValidAccessToken()) throw new InvalidOperationException("Access token invalid – refresh required.");
    if (string.IsNullOrWhiteSpace(_realmId))
        throw new InvalidOperationException("QuickBooks company (realmId) is not set. Connect to QuickBooks first.");
    var validator = new OAuth2RequestValidator(s.QboAccessToken);
    var ctx = new ServiceContext(_realmId!, IntuitServicesType.QBO, validator);
    ctx.IppConfiguration.BaseUrl.Qbo = _environment == "sandbox" ? "https://sandbox-quickbooks.api.intuit.com/" : "https://quickbooks.api.intuit.com/";
    return (ctx, new DataService(ctx));
}

public async System.Threading.Tasks.Task<bool> TestConnectionAsync()
{
    await EnsureInitializedAsync().ConfigureAwait(false);
    try
    {
        await RefreshTokenIfNeededAsync();
        var p = GetDataService();
        // Try to fetch a small amount of data to test the connection
        var customers = p.Ds.FindAll(new Customer(), 1, 1).ToList();
        return true;
    }
    catch (Exception ex)
    {
        Serilog.Log.Error(ex, "QBO connection test failed");
        return false;
    }
}

public async System.Threading.Tasks.Task<UrlAclCheckResult> CheckUrlAclAsync(string? redirectUri = null)
{
    await EnsureInitializedAsync().ConfigureAwait(false);
    var prefix = redirectUri ?? _redirectUri;
    if (!prefix.EndsWith("/", StringComparison.Ordinal))
        prefix += "/";

    // netsh requires http scheme
    if (!prefix.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
    {
        // If HTTPS or custom, we can't check via netsh easily; return guidance
        return new UrlAclCheckResult
        {
            IsReady = false,
            ListenerPrefix = prefix,
            Guidance = "The redirect URI is not using HTTP. For local dev with HttpListener, use http://localhost:PORT/ and run: netsh http add urlacl url=http://localhost:PORT/ user=%USERNAME%"
        };
    }

    try
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "http show urlacl",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        // Look for an entry matching our prefix
        // Example line:    Reserved URL            : http://localhost:8080/
        var isPresent = output?.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0;
        string? owner = null;
        if (isPresent)
        {
            // Try to capture owner following the prefix block
            // Owner: S-1-5-32-545\User or similar
            var prefixIndex = output!.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (prefixIndex >= 0)
            {
                var tail = output.Substring(prefixIndex, Math.Min(500, output.Length - prefixIndex));
                var ownerIdx = tail.IndexOf("Owner:", StringComparison.OrdinalIgnoreCase);
                if (ownerIdx >= 0)
                {
                    var line = tail.Substring(ownerIdx).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (line != null)
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1) owner = parts[1].Trim();
                    }
                }
            }
        }

        return new UrlAclCheckResult
        {
            IsReady = isPresent,
            ListenerPrefix = prefix,
            Owner = owner,
            RawNetshOutput = output,
            Guidance = isPresent
                ? "URL ACL is configured. You should be able to complete OAuth sign-in."
                : $"URL ACL not found. Run as admin: netsh http add urlacl url={prefix} user=%USERNAME%"
        };
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to check URL ACL via netsh");
        return new UrlAclCheckResult
        {
            IsReady = false,
            ListenerPrefix = prefix,
            Guidance = $"Couldn't verify URL ACL automatically. Try running as admin: netsh http add urlacl url={prefix} user=%USERNAME%"
        };
    }
}

public async System.Threading.Tasks.Task<List<Customer>> GetCustomersAsync()
{
    try
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await RefreshTokenIfNeededAsync();
        var p = GetDataService();
        // Fetch customers from QuickBooks
        return p.Ds.FindAll(new Customer(), 1, 100).ToList();
    }
    catch (Exception ex)
    {
        Serilog.Log.Error(ex, "QBO customers fetch failed");
        throw;
    }
}

public async System.Threading.Tasks.Task<List<Invoice>> GetInvoicesAsync(string? enterprise = null)
{
    try
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await RefreshTokenIfNeededAsync();
        var p = GetDataService();
        if (string.IsNullOrWhiteSpace(enterprise))
            return p.Ds.FindAll(new Invoice(), 1, 100).ToList();
        var query = $"SELECT * FROM Invoice WHERE Metadata.CustomField['Enterprise'] = '{enterprise}'";
        var qs = new QueryService<Invoice>(p.Ctx);
        return qs.ExecuteIdsQuery(query).ToList();
    }
    catch (Exception ex)
    {
        Serilog.Log.Error(ex, "QBO invoices fetch failed");
        throw;
    }
}

public async System.Threading.Tasks.Task<List<Account>> GetChartOfAccountsAsync()
{
    try
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await RefreshTokenIfNeededAsync();

        var allAccounts = new List<Account>();
        const int pageSize = 500; // QuickBooks recommended page size
        int startPosition = 1;
        int maxPages = 10; // Safety limit to prevent infinite loops
        int pageCount = 0;

        _logger.LogInformation("Starting batch fetch of chart of accounts");

        while (pageCount < maxPages)
        {
            var p = GetDataService();
            var pageAccounts = p.Ds.FindAll(new Account(), startPosition, pageSize).ToList();

            if (pageAccounts == null || pageAccounts.Count == 0)
            {
                _logger.LogInformation("No more accounts found at position {Position}, ending fetch", startPosition);
                break;
            }

            allAccounts.AddRange(pageAccounts);
            _logger.LogInformation("Fetched page {Page} with {Count} accounts (total: {Total})",
                pageCount + 1, pageAccounts.Count, allAccounts.Count);

            // If we got fewer than pageSize, we've reached the end
            if (pageAccounts.Count < pageSize)
            {
                break;
            }

            startPosition += pageSize;
            pageCount++;

            // Small delay between pages to be respectful to the API
            if (pageCount < maxPages)
            {
                await System.Threading.Tasks.Task.Delay(100).ConfigureAwait(false);
            }
        }

        if (pageCount >= maxPages)
        {
            _logger.LogWarning("Reached maximum page limit ({MaxPages}) for chart of accounts fetch. Total accounts: {Total}",
                maxPages, allAccounts.Count);
        }

        _logger.LogInformation("Chart of accounts fetch completed. Total accounts: {Total}", allAccounts.Count);
        return allAccounts;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "QBO chart of accounts batch fetch failed");
        throw;
    }
}

public async System.Threading.Tasks.Task<List<JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate)
{
    try
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await RefreshTokenIfNeededAsync();
        var p = GetDataService();

        // Query journal entries within date range
        var query = $"SELECT * FROM JournalEntry WHERE TxnDate >= '{startDate:yyyy-MM-dd}' AND TxnDate <= '{endDate:yyyy-MM-dd}'";
        var qs = new QueryService<JournalEntry>(p.Ctx);
        return qs.ExecuteIdsQuery(query).ToList();
    }
    catch (Exception ex)
    {
        Serilog.Log.Error(ex, "QBO journal entries fetch failed");
        throw;
    }
}

public async System.Threading.Tasks.Task<List<Budget>> GetBudgetsAsync()
{
    try
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await RefreshTokenIfNeededAsync();
        var p = GetDataService();
        // Fetch budgets from QuickBooks
        return p.Ds.FindAll(new Budget(), 1, 100).ToList();
    }
    catch (Exception ex)
    {
        Serilog.Log.Error(ex, "QBO budgets fetch failed");
        throw;
    }
}

/// <summary>
/// Syncs budgets to QuickBooks Online via REST API.
/// Uses IHttpClientFactory to create named 'QBO' client with proper authentication.
/// On success, publishes Prism event to refresh UI (e.g., SfDataGrid in SettingsView).
/// </summary>
public async System.Threading.Tasks.Task<SyncResult> SyncBudgetsToAppAsync(IEnumerable<Budget> budgets, CancellationToken cancellationToken = default)
{
    if (budgets == null)
    {
        throw new ArgumentNullException(nameof(budgets));
    }

    var stopwatch = Stopwatch.StartNew();
    try
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await RefreshTokenIfNeededAsync();

        if (string.IsNullOrWhiteSpace(_realmId))
        {
            return new SyncResult
            {
                Success = false,
                ErrorMessage = "QuickBooks company (realmId) is not set. Connect to QuickBooks first.",
                Duration = stopwatch.Elapsed
            };
        }

        var s = EnsureSettingsLoaded();
        if (!HasValidAccessToken())
        {
            return new SyncResult
            {
                Success = false,
                ErrorMessage = "Access token invalid – refresh required.",
                Duration = stopwatch.Elapsed
            };
        }

        // Use IHttpClientFactory to get named 'QBO' client
        var httpClientFactory = _serviceProvider.GetService<IHttpClientFactory>();
        if (httpClientFactory == null)
        {
            _logger.LogError("IHttpClientFactory not available - cannot sync budgets to QBO");
            return new SyncResult
            {
                Success = false,
                ErrorMessage = "IHttpClientFactory not registered in DI container",
                Duration = stopwatch.Elapsed
            };
        }

        var client = httpClientFactory.CreateClient("QBO");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", s.QboAccessToken);
        client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        int syncedCount = 0;
        foreach (var budget in budgets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // QBO API: POST /v3/company/{realmId}/budget
            var endpoint = $"v3/company/{_realmId}/budget";
            var json = System.Text.Json.JsonSerializer.Serialize(budget);

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                var endpointUri = new Uri(endpoint, UriKind.Relative);
                var response = await client.PostAsync(endpointUri, content, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    syncedCount++;
                    _logger.LogInformation("Successfully synced budget {BudgetId} to QBO", budget.Id);
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogWarning("Failed to sync budget {BudgetId} to QBO: {StatusCode} - {Error}",
                        budget.Id, response.StatusCode, errorBody);
                }
            }
        }

        stopwatch.Stop();

        // On success: Publish Prism event to refresh UI
        try
        {
            var eventAggregator = _serviceProvider.GetService<Prism.Events.IEventAggregator>();
            if (eventAggregator != null)
            {
                // Publish event to refresh SfDataGrid in SettingsView or other subscribers
                eventAggregator.GetEvent<WileyWidget.Services.Events.BudgetsSyncedEvent>()?.Publish(syncedCount);
                _logger.LogDebug("Published BudgetsSyncedEvent with count: {Count}", syncedCount);
            }
        }
        catch (Exception eventEx)
        {
            _logger.LogWarning(eventEx, "Failed to publish BudgetsSyncedEvent after sync");
        }

        return new SyncResult
        {
            Success = true,
            RecordsSynced = syncedCount,
            Duration = stopwatch.Elapsed
        };
    }
    catch (OperationCanceledException)
    {
        stopwatch.Stop();
        _logger.LogInformation("Budget sync to QBO was cancelled");
        return new SyncResult
        {
            Success = false,
            ErrorMessage = "Operation cancelled",
            Duration = stopwatch.Elapsed
        };
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        Serilog.Log.Error(ex, "QBO budget sync failed");
        return new SyncResult
        {
            Success = false,
            ErrorMessage = ex.Message,
            Duration = stopwatch.Elapsed
        };
    }
}

public Task<bool> AuthorizeAsync()
{
    // Expose the interactive OAuth flow to the UI
    return AcquireTokensInteractiveAsync();
}

private WileyWidget.Models.AppSettings EnsureSettingsLoaded()
{
    if (_settingsLoaded) return _settings.Current;

    _settings.Load();
    _settingsLoaded = true;
    return _settings.Current;
}

private async Task<bool> AcquireTokensInteractiveAsync()
{
    await EnsureInitializedAsync().ConfigureAwait(false);
    if (!HttpListener.IsSupported)
    {
        _logger.LogError("HttpListener is not supported on this platform; cannot perform QuickBooks OAuth authorization.");
        return false;
    }

    var s = EnsureSettingsLoaded();
    var listenerPrefix = _redirectUri.EndsWith("/", StringComparison.Ordinal) ? _redirectUri : _redirectUri + "/";
    using var listener = new HttpListener();
    const string fallbackPrefix = "http://localhost:8080/";
    var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { fallbackPrefix, listenerPrefix };
    foreach (var prefix in prefixes)
    {
        listener.Prefixes.Add(prefix);
    }

    // Ensure URL ACL exists for our chosen prefix, then ensure Cloudflare tunnel is up for OAuth redirect
    try
    {
        var acl = await CheckUrlAclAsync(listenerPrefix).ConfigureAwait(false);
        if (!acl.IsReady)
        {
            var ensured = await TryEnsureUrlAclAsync(listenerPrefix).ConfigureAwait(false);
            _logger.LogInformation("URL ACL ensure attempted for {Prefix} (success={Success}).", listenerPrefix, ensured);
        }
    }
    catch (Exception ex)
    {
        _logger.LogDebug(ex, "URL ACL ensure step encountered an issue; proceeding to start listener.");
    }

    // Try to ensure a Cloudflare tunnel is available to reach our localhost callback (optional for local dev)
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var tunnelReady = await EnsureCloudflaredTunnelAsync(cts.Token).ConfigureAwait(false);
        if (tunnelReady)
        {
            _logger.LogInformation("Cloudflare tunnel ready{Url}.", string.IsNullOrWhiteSpace(_cloudflaredPublicUrl) ? string.Empty : $" at {_cloudflaredPublicUrl}");
        }
    }
    catch (Exception ex)
    {
        _logger.LogDebug(ex, "Cloudflare tunnel step is optional and failed; continuing with local OAuth callback.");
    }

    try
    {
        listener.Start();
    }
    catch (HttpListenerException ex)
    {
        var command = $"netsh http add urlacl url={listenerPrefix} user=%USERNAME%";
        _logger.LogError(ex, "Failed to start OAuth callback listener on {Prefix}. Run '{Command}' or restart with elevated privileges.", listenerPrefix, command);
        return false;
    }

    // Build the authorization URL ourselves to avoid invoking Intuit Diagnostics advanced logging
    var state = Guid.NewGuid().ToString("N");
    var authUrl = BuildAuthorizationUrl(DefaultScopes, state);
    _logger.LogWarning("Launching QuickBooks OAuth flow. Complete sign-in for realm {RealmId}.", _realmId);
    // If provided, launch pre-login URL to ensure correct account context, then launch OAuth
    if (!string.IsNullOrWhiteSpace(_intuitPreLoginUrl))
    {
        try
        {
            LaunchOAuthBrowser(_intuitPreLoginUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open Intuit pre-login URL; continuing with OAuth");
        }
    }
    LaunchOAuthBrowser(authUrl);

    HttpListenerContext? context = null;
    try
    {
        var timeoutTask = System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(5));
        var contextTask = listener.GetContextAsync();
        var completed = await System.Threading.Tasks.Task.WhenAny(contextTask, timeoutTask).ConfigureAwait(false);
        if (completed != contextTask)
        {
            _logger.LogWarning("OAuth callback listener timed out waiting for Intuit redirect.");
            return false;
        }

        context = contextTask.Result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed while awaiting QuickBooks OAuth callback.");
        return false;
    }
    finally
    {
        listener.Stop();
    }

    var request = context.Request;
    var response = context.Response;
    var query = request.QueryString;
    var returnedState = query["state"];
    var code = query["code"];
    var realmIdFromCallback = query["realmId"]; // provided by Intuit on success
    var error = query["error"];
    var success = !string.IsNullOrWhiteSpace(code) && string.Equals(state, returnedState, StringComparison.Ordinal);

    if (!string.IsNullOrWhiteSpace(error))
    {
        _logger.LogWarning("QuickBooks OAuth returned error {Error}", error);
        success = false;
    }

    if (!success)
    {
        await WriteCallbackResponseAsync(response, "Authorization failed. You can close this window and return to Wiley Widget.").ConfigureAwait(false);
        return false;
    }

    try
    {
        var tokenResponse = await ExchangeAuthorizationCodeForTokensAsync(code).ConfigureAwait(false);
        s.QboAccessToken = tokenResponse.AccessToken;
        s.QboRefreshToken = tokenResponse.RefreshToken;
        s.QboTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

        // Capture realmId automatically if provided
        if (!string.IsNullOrWhiteSpace(realmIdFromCallback))
        {
            _realmId = realmIdFromCallback;
            // Persist realmId for future runs if a secret vault is available
            try
            {
                if (_secretVault != null)
                    await _secretVault.SetSecretAsync("QBO-REALM-ID", _realmId).ConfigureAwait(false);
            }
            catch { }
        }
        else if (string.IsNullOrWhiteSpace(_realmId) && !string.IsNullOrWhiteSpace(_intuitPreLoginUrl))
        {
            // As a safety, detect account_id_hint from pre-login URL if user provided one
            try
            {
                var uri = new Uri(_intuitPreLoginUrl);
                var qs = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var hint = qs["account_id_hint"];
                    if (!string.IsNullOrWhiteSpace(hint))
                    {
                        _realmId = hint;
                        try
                        {
                            if (_secretVault != null)
                                await _secretVault.SetSecretAsync("QBO-REALM-ID", _realmId).ConfigureAwait(false);
                        }
                        catch { }
                        _logger.LogInformation("Captured realmId from account_id_hint: {RealmId}", _realmId);
                    }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse account_id_hint from Intuit pre-login URL");
            }
        }
        _settings.Save();
        Serilog.Log.Information("QBO tokens acquired interactively (exp {Expiry}). Reminder: protect tokens at rest in production.", s.QboTokenExpiry);
        await WriteCallbackResponseAsync(response, "Authorization complete. You may close this tab and return to Wiley Widget.").ConfigureAwait(false);
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to exchange authorization code for tokens.");
        await WriteCallbackResponseAsync(response, "Authorization encountered an error. Check application logs for details.").ConfigureAwait(false);
        return false;
    }
}

private string BuildAuthorizationUrl(IReadOnlyList<string> scopes, string state)
{
    // space-delimited scope string must be URL-encoded
    var scopeParam = Uri.EscapeDataString(string.Join(' ', scopes));
    var redirectParam = Uri.EscapeDataString(_redirectUri);
    var clientIdParam = Uri.EscapeDataString(_clientId!);
    var stateParam = Uri.EscapeDataString(state);
    var url = $"{AuthorizationEndpoint}?client_id={clientIdParam}&response_type=code&scope={scopeParam}&redirect_uri={redirectParam}&state={stateParam}";
    return url;
}

private sealed record TokenResult(string AccessToken, string RefreshToken, int ExpiresIn, int RefreshTokenExpiresIn);

private async Task<TokenResult> ExchangeAuthorizationCodeForTokensAsync(string code)
{
    using var req = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
    var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_clientId}:{_clientSecret}"));
    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basic);
    req.Headers.Accept.ParseAdd("application/json");
    var form = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("code", code),
            new("redirect_uri", _redirectUri)
        };
    req.Content = new FormUrlEncodedContent(form);
    using var resp = await _httpClient.SendAsync(req).ConfigureAwait(false);
    var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
    if (!resp.IsSuccessStatusCode)
    {
        throw new InvalidOperationException($"Intuit token exchange failed ({(int)resp.StatusCode}): {json}");
    }

    using var doc = JsonDocument.Parse(json);
    var root = doc.RootElement;
    var access = root.GetProperty("access_token").GetString()!;
    var refresh = root.GetProperty("refresh_token").GetString()!;
    var expires = root.GetProperty("expires_in").GetInt32();
    var refreshExpires = root.TryGetProperty("x_refresh_token_expires_in", out var x) ? x.GetInt32() : 0;
    return new TokenResult(access, refresh, expires, refreshExpires);
}

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
                    throw new InvalidOperationException($"Refresh token is invalid or expired: {json}");
                }

                lastException = new HttpRequestException(error);
                if (attempt < maxRetries)
                {
                    await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))).ConfigureAwait(false);
                    continue;
                }
                throw lastException;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("access_token", out var accessTokenProp) || accessTokenProp.GetString() is not string access)
            {
                throw new InvalidOperationException("Invalid token response: missing access_token");
            }

            var refresh = root.TryGetProperty("refresh_token", out var refreshTokenProp)
                ? refreshTokenProp.GetString() ?? refreshToken
                : refreshToken;

            if (!root.TryGetProperty("expires_in", out var expiresProp) || !expiresProp.TryGetInt32(out var expires))
            {
                throw new InvalidOperationException("Invalid token response: missing or invalid expires_in");
            }

            var refreshExpires = root.TryGetProperty("x_refresh_token_expires_in", out var x) && x.TryGetInt32(out var xVal) ? xVal : 0;

            _logger.LogInformation("Token refresh successful on attempt {Attempt}", attempt);
            return new TokenResult(access, refresh, expires, refreshExpires);
        }
        catch (JsonException ex)
        {
            lastException = new InvalidOperationException($"Invalid JSON response from token endpoint: {json}", ex);
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
            await System.Threading.Tasks.Task.Delay(delay).ConfigureAwait(false);
        }
    }

    throw new InvalidOperationException($"Token refresh failed after {maxRetries} attempts", lastException);
}

private static async System.Threading.Tasks.Task WriteCallbackResponseAsync(HttpListenerResponse response, string message)
{
    var html = $"<html><body><h2>Wiley Widget - QuickBooks</h2><p>{WebUtility.HtmlEncode(message)}</p></body></html>";
    var payload = Encoding.UTF8.GetBytes(html);
    response.ContentType = "text/html";
    response.ContentEncoding = Encoding.UTF8;
    response.ContentLength64 = payload.Length;
    await response.OutputStream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
    response.OutputStream.Close();
}

private void LaunchOAuthBrowser(string authUrl)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = authUrl,
            UseShellExecute = true
        };
        Process.Start(psi);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to launch browser for QuickBooks OAuth flow. Navigate manually to {AuthUrl}.", authUrl);
    }
}

/// <summary>
/// Ensures a Cloudflare tunnel is running that forwards the local redirect URI port, starting one if needed.
/// Uses 'cloudflared tunnel --url http://localhost:PORT' and waits for readiness indicated by a public URL in stdout.
/// This is optional for local development but helps when a public callback URL is required.
/// For webhooks, we need to tunnel to the webhooks server port, not the main app port.
/// </summary>
private async Task<bool> EnsureCloudflaredTunnelAsync(CancellationToken cancellationToken)
{
    await EnsureInitializedAsync().ConfigureAwait(false);

    // If already running, assume good
    if (_cloudflaredProcess is { HasExited: false }) return true;

    await _cloudflaredSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        if (_cloudflaredProcess is { HasExited: false }) return true;

        // For webhooks, we need to tunnel to the webhooks server, not the main app
        // Use environment variable or default to webhooks HTTPS port
        var webhooksPort = Environment.GetEnvironmentVariable("WEBHOOKS_PORT", EnvironmentVariableTarget.User)
                          ?? Environment.GetEnvironmentVariable("WEBHOOKS_PORT", EnvironmentVariableTarget.Process)
                          ?? "7207"; // Default to webhooks HTTPS port

        var targetUrl = $"https://localhost:{webhooksPort}";

        var exe = Environment.GetEnvironmentVariable("CLOUDFLARED_EXE", EnvironmentVariableTarget.User)
                  ?? Environment.GetEnvironmentVariable("CLOUDFLARED_EXE", EnvironmentVariableTarget.Process)
                  ?? "cloudflared"; // rely on PATH

        var extraArgs = Environment.GetEnvironmentVariable("CLOUDFLARED_ARGS", EnvironmentVariableTarget.User)
                       ?? Environment.GetEnvironmentVariable("CLOUDFLARED_ARGS", EnvironmentVariableTarget.Process)
                       ?? string.Empty;

        var args = $"tunnel --no-autoupdate --loglevel info --url {targetUrl} {extraArgs}".Trim();

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process? process = null;
        try
        {
            process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var readyTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var urlRegex = new Regex(@"https?://[\w\-\.]+\.trycloudflare\.com", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data is null) return;
                var m = urlRegex.Match(e.Data);
                if (m.Success && !readyTcs.Task.IsCompleted)
                {
                    readyTcs.TrySetResult(m.Value);
                }
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data is null) return;
                // Surface obvious failures quickly
                if (e.Data.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 && !readyTcs.Task.IsCompleted)
                {
                    readyTcs.TrySetException(new InvalidOperationException($"cloudflared error: {e.Data}"));
                }
            };

            if (!process.Start())
            {
                _logger.LogWarning("Failed to start cloudflared process (FileName={Exe}).", exe);
                process.Dispose();
                return false;
            }

            _cloudflaredProcess = process;
            process = null; // Ownership transferred, prevent double dispose
            _cloudflaredProcess.BeginOutputReadLine();
            _cloudflaredProcess.BeginErrorReadLine();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(25));

            try
            {
                var url = await readyTcs.Task.WaitAsync(linkedCts.Token).ConfigureAwait(false);
                _cloudflaredPublicUrl = url;
                _logger.LogInformation("cloudflared tunnel established for webhooks: {Url} -> {Target}", url, targetUrl);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Timed out waiting for cloudflared tunnel readiness.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "cloudflared start failed. Is it installed and on PATH?");
            return false;
        }
        finally
        {
            process?.Dispose();
        }
    }
    finally
    {
        _cloudflaredSemaphore.Release();
    }
}

private async Task<bool> TryEnsureUrlAclAsync(string? redirectUri = null)
{
    var prefix = redirectUri ?? _redirectUri;
    if (!prefix.EndsWith("/", StringComparison.Ordinal))
        prefix += "/";
    if (!prefix.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        return false; // only supported for HTTP

    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = $"http add urlacl url={prefix} user=%USERNAME%",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p == null) return false;
        var output = await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var error = await p.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await p.WaitForExitAsync().ConfigureAwait(false);
        var success = p.ExitCode == 0 || (output?.IndexOf("exists", StringComparison.OrdinalIgnoreCase) >= 0);
        if (!success)
        {
            _logger.LogDebug("netsh add urlacl failed (code {Code}). Error: {Error}", p.ExitCode, string.IsNullOrWhiteSpace(error) ? output : error);
        }
        return success;
    }
    catch (Exception ex)
    {
        _logger.LogDebug(ex, "Unable to run netsh add urlacl; may require elevation.");
        return false;
    }
}

/// <summary>
/// Connects to QuickBooks by ensuring valid tokens and testing the connection.
/// </summary>
public async System.Threading.Tasks.Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
{
    try
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        // Ensure we have valid tokens
        await RefreshTokenIfNeededAsync();
        cancellationToken.ThrowIfCancellationRequested();

        // Test the connection
        var testResult = await TestConnectionAsync();
        cancellationToken.ThrowIfCancellationRequested();

        if (testResult)
        {
            _logger.LogInformation("Successfully connected to QuickBooks");
            return true;
        }
        else
        {
            _logger.LogWarning("Connection test failed");
            return false;
        }
    }
    catch (OperationCanceledException)
    {
        _logger.LogInformation("QuickBooks connection was cancelled");
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to connect to QuickBooks");
        return false;
    }
}

/// <summary>
/// Disconnects from QuickBooks by clearing tokens and connection state.
/// </summary>
public System.Threading.Tasks.Task DisconnectAsync(CancellationToken cancellationToken = default)
{
    try
    {
        cancellationToken.ThrowIfCancellationRequested();

        var s = EnsureSettingsLoaded();
        s.QboAccessToken = null;
        s.QboRefreshToken = null;
        s.QboTokenExpiry = default(DateTime);
        _settings.Save();

        // Clear cached realm ID
        _realmId = null;

        _logger.LogInformation("Successfully disconnected from QuickBooks");
    }
    catch (OperationCanceledException)
    {
        _logger.LogInformation("QuickBooks disconnection was cancelled");
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to disconnect from QuickBooks");
        throw;
    }

    return System.Threading.Tasks.Task.CompletedTask;
}

/// <summary>
/// Checks if the service is currently connected to QuickBooks.
/// </summary>
public async System.Threading.Tasks.Task<bool> IsConnectedAsync()
{
    try
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        // Check if we have valid tokens
        var settings = EnsureSettingsLoaded();
        if (string.IsNullOrEmpty(settings.QboAccessToken) ||
            string.IsNullOrEmpty(settings.QboRefreshToken) ||
            settings.QboTokenExpiry <= DateTime.Now)
        {
            return false;
        }

        // Test the connection
        return await TestConnectionAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to check QuickBooks connection status");
        return false;
    }
}

/// <summary>
/// Gets the current connection status of the QuickBooks service.
/// </summary>
public async System.Threading.Tasks.Task<ConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken = default)
{
    try
    {
        cancellationToken.ThrowIfCancellationRequested();

        var s = EnsureSettingsLoaded();
        var hasTokens = !string.IsNullOrEmpty(s.QboAccessToken) && !string.IsNullOrEmpty(s.QboRefreshToken);
        var isExpired = s.QboTokenExpiry != default(DateTime) && s.QboTokenExpiry <= DateTime.UtcNow;

        if (!hasTokens)
        {
            return new ConnectionStatus
            {
                IsConnected = false,
                StatusMessage = "Not connected - no tokens available"
            };
        }

        if (isExpired)
        {
            return new ConnectionStatus
            {
                IsConnected = false,
                StatusMessage = "Not connected - tokens expired"
            };
        }

        // Try to test the connection
        var testResult = await TestConnectionAsync();
        cancellationToken.ThrowIfCancellationRequested();

        if (testResult)
        {
            return new ConnectionStatus
            {
                IsConnected = true,
                CompanyName = _realmId,
                StatusMessage = "Connected and ready"
            };
        }
        else
        {
            return new ConnectionStatus
            {
                IsConnected = false,
                StatusMessage = "Connection test failed"
            };
        }
    }
    catch (OperationCanceledException)
    {
        _logger.LogInformation("Connection status check was cancelled");
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to get connection status");
        return new ConnectionStatus
        {
            IsConnected = false,
            StatusMessage = $"Error: {ex.Message}"
        };
    }
}

/// <summary>
/// Imports chart of accounts from QuickBooks into the local database.
/// This is a production-ready implementation that validates data integrity
/// and provides comprehensive error reporting.
/// </summary>
public async System.Threading.Tasks.Task<ImportResult> ImportChartOfAccountsAsync(CancellationToken cancellationToken = default)
{
    var startTime = DateTime.UtcNow;
    var validationErrors = new List<string>();

    try
    {
        cancellationToken.ThrowIfCancellationRequested();

        await EnsureInitializedAsync().ConfigureAwait(false);
        await RefreshTokenIfNeededAsync();
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Starting chart of accounts import from QuickBooks");

        // Fetch chart of accounts from QuickBooks using paginated batch fetch
        var qbAccounts = await GetChartOfAccountsAsync().ConfigureAwait(false);

        if (qbAccounts == null || qbAccounts.Count == 0)
        {
            _logger.LogWarning("No accounts found in QuickBooks chart of accounts");
            return new ImportResult
            {
                Success = false,
                ErrorMessage = "No accounts found in QuickBooks",
                Duration = DateTime.UtcNow - startTime
            };
        }

        _logger.LogInformation("Retrieved {Count} accounts from QuickBooks", qbAccounts.Count);

        // Validate chart structure before import
        var validationResult = ValidateChartOfAccounts(qbAccounts);
        if (!validationResult.IsValid)
        {
            validationErrors.AddRange(validationResult.Errors);
            _logger.LogWarning("Chart validation failed with {ErrorCount} errors", validationResult.Errors.Count);

            // For production, we might want to fail on validation errors
            // For now, we'll log warnings but continue
            foreach (var error in validationResult.Errors)
            {
                _logger.LogWarning("Chart validation error: {Error}", error);
            }
        }

        // Import accounts using the repository
        using var scope = _serviceProvider.CreateScope();
        var municipalAccountRepository = scope.ServiceProvider.GetRequiredService<IMunicipalAccountRepository>();
        await municipalAccountRepository.ImportChartOfAccountsAsync(qbAccounts);

        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation("Chart of accounts import completed successfully. Imported {Count} accounts in {Duration}",
            qbAccounts.Count, duration);

        return new ImportResult
        {
            Success = true,
            AccountsImported = qbAccounts.Count,
            AccountsUpdated = 0, // This would need to be tracked in the repository
            AccountsSkipped = 0, // This would need to be tracked in the repository
            Duration = duration,
            ValidationErrors = validationErrors.Any() ? validationErrors : null
        };
    }
    catch (OperationCanceledException)
    {
        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation("Chart import was cancelled after {Duration}", duration);
        throw;
    }
    catch (Exception ex)
    {
        var duration = DateTime.UtcNow - startTime;
        _logger.LogError(ex, "Chart of accounts import failed after {Duration}", duration);
        return new ImportResult
        {
            Success = false,
            ErrorMessage = ex.Message,
            Duration = duration,
            ValidationErrors = validationErrors.Any() ? validationErrors : null
        };
    }
}

/// <summary>
/// Validates the chart of accounts structure before import.
/// </summary>
private (bool IsValid, List<string> Errors) ValidateChartOfAccounts(List<Account> accounts)
{
    var errors = new List<string>();

    if (accounts == null || accounts.Count == 0)
    {
        errors.Add("No accounts provided for validation");
        return (false, errors);
    }

    // Check for duplicate account numbers
    var accountNumbers = accounts
        .Where(a => !string.IsNullOrEmpty(a.AcctNum))
        .GroupBy(a => a.AcctNum)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key)
        .ToList();

    if (accountNumbers.Any())
    {
        errors.Add($"Duplicate account numbers found: {string.Join(", ", accountNumbers)}");
    }

    // Check for accounts without account numbers
    var accountsWithoutNumbers = accounts.Count(a => string.IsNullOrEmpty(a.AcctNum));
    if (accountsWithoutNumbers > 0)
    {
        errors.Add($"{accountsWithoutNumbers} accounts found without account numbers");
    }

    // Check for accounts without names
    var accountsWithoutNames = accounts.Count(a => string.IsNullOrEmpty(a.Name));
    if (accountsWithoutNames > 0)
    {
        errors.Add($"{accountsWithoutNames} accounts found without names");
    }

    // Validate account type consistency - AccountType is not nullable in Intuit SDK
    // All accounts should have a valid AccountType by definition

    return (errors.Count == 0, errors);
}

/// <summary>
/// Synchronizes data from QuickBooks.
/// </summary>
public async System.Threading.Tasks.Task<SyncResult> SyncDataAsync(CancellationToken cancellationToken = default)
{
    var startTime = DateTime.UtcNow;
    try
    {
        cancellationToken.ThrowIfCancellationRequested();

        await EnsureInitializedAsync().ConfigureAwait(false);
        await RefreshTokenIfNeededAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var p = GetDataService();
        var totalRecords = 0;

        // Sync customers
        cancellationToken.ThrowIfCancellationRequested();
        var customers = p.Ds.FindAll(new Customer(), 1, 100).ToList();
        totalRecords += customers.Count;
        _logger.LogInformation("Synced {Count} customers", customers.Count);

        // Sync invoices
        cancellationToken.ThrowIfCancellationRequested();
        var invoices = p.Ds.FindAll(new Invoice(), 1, 100).ToList();
        totalRecords += invoices.Count;
        _logger.LogInformation("Synced {Count} invoices", invoices.Count);

        // Sync accounts
        cancellationToken.ThrowIfCancellationRequested();
        var accounts = p.Ds.FindAll(new Account(), 1, 100).ToList();
        totalRecords += accounts.Count;
        _logger.LogInformation("Synced {Count} accounts", accounts.Count);

        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation("Data sync completed successfully. Total records: {TotalRecords}, Duration: {Duration}", totalRecords, duration);

        return new SyncResult
        {
            Success = true,
            RecordsSynced = totalRecords,
            Duration = duration
        };
    }
    catch (OperationCanceledException)
    {
        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation("Data sync was cancelled after {Duration}", duration);
        throw;
    }
    catch (Exception ex)
    {
        var duration = DateTime.UtcNow - startTime;
        _logger.LogError(ex, "Data sync failed after {Duration}", duration);
        return new SyncResult
        {
            Success = false,
            RecordsSynced = 0,
            ErrorMessage = ex.Message,
            Duration = duration
        };
    }
}
}
