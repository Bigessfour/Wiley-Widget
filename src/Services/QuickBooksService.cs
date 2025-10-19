using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Intuit.Ipp.Core;
using Intuit.Ipp.Data;
using Intuit.Ipp.DataService;
using Intuit.Ipp.OAuth2PlatformClient;
using Intuit.Ipp.Security;
using Intuit.Ipp.QueryFilter;
using Microsoft.Extensions.Logging;
using System.IO;

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
    private string _redirectUri = "http://localhost:8080/callback";
    private string? _realmId;
    private string _environment = "sandbox";
    private OAuth2Client? _oauthClient;
    private bool _settingsLoaded;

    private volatile bool _initialized;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);

    // Intuit sandbox base URL documented at https://developer.intuit.com/app/developer/qbo/docs/develop/sandboxes
    private static readonly IReadOnlyList<string> DefaultScopes = new[] { "com.intuit.quickbooks.accounting" };

    public QuickBooksService(SettingsService settings, ISecretVaultService keyVaultService, ILogger<QuickBooksService> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _secretVault = keyVaultService; // may be null in some test contexts

        // Secrets and OAuth client are loaded lazily via EnsureInitializedAsync()
        EnsureSettingsLoaded();
    }

    public void Dispose()
    {
        _initSemaphore.Dispose();
    }

    private static async System.Threading.Tasks.Task<string?> TryGetFromSecretVaultAsync(ISecretVaultService? keyVaultService, string secretName, ILogger logger)
    {
        try
        {
            if (keyVaultService == null)
            {
                logger.LogDebug("Secret vault service not available for {SecretName}", secretName);
                return null;
            }

            var secretValue = await keyVaultService.GetSecretAsync(secretName).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(secretValue))
            {
                logger.LogInformation("Successfully loaded {SecretName} from secret vault", secretName);
                return secretValue;
            }
            else
            {
                logger.LogDebug("{SecretName} not found in secret vault", secretName);
                return null;
            }
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
                       ?? Environment.GetEnvironmentVariable("QBO_REALM_ID", EnvironmentVariableTarget.User)
                       ?? throw new InvalidOperationException("QBO_REALM_ID not found in the secret vault or environment variables.");

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

            try
            {
                _oauthClient = new OAuth2Client(_clientId, _clientSecret, _redirectUri, _environment);
            }
            catch (System.MissingMethodException ex)
            {
                // Intuit OAuth2 library has Serilog version conflict - disable advanced logging
                _logger.LogWarning(ex, "QuickBooks OAuth2Client failed to initialize with advanced logging - continuing without it");
                // OAuth2Client still works for basic operations even if logging initialization fails
                _oauthClient = null; // Will need to handle null checks
            }

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
        var response = await _oauthClient!.RefreshTokenAsync(s.QboRefreshToken).ConfigureAwait(false);
        s.QboAccessToken = response.AccessToken;
        s.QboRefreshToken = response.RefreshToken;
        // SDK response no longer exposes ExpiresIn strongly-typed; assume 55 minutes (typical 60) unless reflection finds property.
        var assumedLifetime = TimeSpan.FromMinutes(55);
        var expiresInProp = response.GetType().GetProperty("ExpiresIn");
        if (expiresInProp != null)
        {
            try
            {
                var val = expiresInProp.GetValue(response);
                if (val is int seconds && seconds > 0) assumedLifetime = TimeSpan.FromSeconds(seconds);
            }
            catch { }
        }
        s.QboTokenExpiry = DateTime.UtcNow.Add(assumedLifetime);
        _settings.Save();
        Serilog.Log.Information("QBO token refreshed (exp {Expiry}). Reminder: protect tokens at rest in production.", s.QboTokenExpiry);
    }

    private (ServiceContext Ctx, DataService Ds) GetDataService()
    {
        var s = EnsureSettingsLoaded();
        if (!HasValidAccessToken()) throw new InvalidOperationException("Access token invalid – refresh required.");
        var validator = new OAuth2RequestValidator(s.QboAccessToken);
        var ctx = new ServiceContext(_realmId!, IntuitServicesType.QBO, validator);
        ctx.IppConfiguration.BaseUrl.Qbo = _environment == "sandbox" ? "https://sandbox-quickbooks.api.intuit.com/" : "https://quickbooks.api.intuit.com/";
        return (ctx, new DataService(ctx));
    }

    public async System.Threading.Tasks.Task<bool> TestConnectionAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        if (_oauthClient == null)
        {
            _logger.LogWarning("QuickBooks OAuth client not initialized - skipping connection test");
            return false;
        }

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
        var prefix = (redirectUri ?? _redirectUri);
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

    public async System.Threading.Tasks.Task<List<Invoice>> GetInvoicesAsync(string enterprise = null)
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
            var p = GetDataService();
            // Fetch all active accounts from QuickBooks
            return p.Ds.FindAll(new Account(), 1, 500).ToList();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "QBO chart of accounts fetch failed");
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
        if (_oauthClient == null)
        {
            _logger.LogWarning("QuickBooks OAuth client not initialized; cannot start interactive authorization.");
            return false;
        }
        if (!HttpListener.IsSupported)
        {
            _logger.LogError("HttpListener is not supported on this platform; cannot perform QuickBooks OAuth authorization.");
            return false;
        }

        var s = EnsureSettingsLoaded();
        var listenerPrefix = _redirectUri.EndsWith("/") ? _redirectUri : _redirectUri + "/";
        using var listener = new HttpListener();
        const string fallbackPrefix = "http://localhost:8080/";
        var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { fallbackPrefix, listenerPrefix };
        foreach (var prefix in prefixes)
        {
            listener.Prefixes.Add(prefix);
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

    var state = Guid.NewGuid().ToString("N");
    var authUrl = _oauthClient.GetAuthorizationURL(DefaultScopes.ToList(), state);
        _logger.LogWarning("Launching QuickBooks OAuth flow. Complete sign-in for realm {RealmId}.", _realmId);
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
            var tokenResponse = await _oauthClient.GetBearerTokenAsync(code, _redirectUri).ConfigureAwait(false);
            s.QboAccessToken = tokenResponse.AccessToken;
            s.QboRefreshToken = tokenResponse.RefreshToken;

            var assumedLifetime = TimeSpan.FromMinutes(55);
            var expiresInProp = tokenResponse.GetType().GetProperty("ExpiresIn");
            if (expiresInProp != null)
            {
                try
                {
                    var val = expiresInProp.GetValue(tokenResponse);
                    if (val is int seconds && seconds > 0) assumedLifetime = TimeSpan.FromSeconds(seconds);
                }
                catch { }
            }

            s.QboTokenExpiry = DateTime.UtcNow.Add(assumedLifetime);
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
}
