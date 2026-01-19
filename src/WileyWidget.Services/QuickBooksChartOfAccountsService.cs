using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of IQuickBooksChartOfAccountsService.
/// Fetches and caches the Chart of Accounts from QuickBooks.
/// </summary>
public sealed class QuickBooksChartOfAccountsService : IQuickBooksChartOfAccountsService
{
    private readonly ILogger _logger;
    private readonly IQuickBooksAuthService _authService;
    private readonly QuickBooksTokenStore? _tokenStore;
    private readonly HttpClient _httpClient;
    private List<QuickBooksAccountInfo>? _cachedAccounts;
    private readonly object _cacheLock = new object();

    // Intuit QBO Query API endpoint - uses QL (Query Language)
    private const string QueryEndpoint = "https://quickbooks.api.intuit.com/v2/company/{0}/query";
    private const string AccountQuery = "SELECT * FROM Account ORDER BY Name";

    public QuickBooksChartOfAccountsService(
        ILogger<QuickBooksChartOfAccountsService> logger,
        IQuickBooksAuthService authService,
        QuickBooksTokenStore? tokenStore,
        HttpClient httpClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _tokenStore = tokenStore;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Fetches the Chart of Accounts from QuickBooks.
    /// </summary>
    public async Task<List<QuickBooksAccountInfo>> FetchAccountsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Get access token
            var token = await _authService.GetAccessTokenAsync(cancellationToken);
            if (token == null)
            {
                _logger.LogWarning("No valid OAuth token available for Chart of Accounts request");
                return new List<QuickBooksAccountInfo>();
            }

            // Get realm ID
            var realmId = await GetRealmIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(realmId))
            {
                _logger.LogWarning("RealmId not available; cannot fetch Chart of Accounts");
                return new List<QuickBooksAccountInfo>();
            }

            // Build request
            var endpoint = $"{string.Format(QueryEndpoint, realmId)}?query={Uri.EscapeDataString(AccountQuery)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

            // Execute request
            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Parse response
            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var accounts = ParseAccountsFromResponse(jsonContent);

            // Cache the results
            lock (_cacheLock)
            {
                _cachedAccounts = accounts;
            }

            _logger.LogInformation("Successfully fetched {AccountCount} accounts from Chart of Accounts", accounts.Count);
            return accounts;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching Chart of Accounts from QuickBooks");
            return new List<QuickBooksAccountInfo>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error in Chart of Accounts response");
            return new List<QuickBooksAccountInfo>();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Chart of Accounts request was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching Chart of Accounts");
            return new List<QuickBooksAccountInfo>();
        }
    }

    /// <summary>
    /// Gets cached accounts.
    /// </summary>
    public List<QuickBooksAccountInfo> GetCachedAccounts()
    {
        lock (_cacheLock)
        {
            return _cachedAccounts ?? new List<QuickBooksAccountInfo>();
        }
    }

    /// <summary>
    /// Gets a specific account by ID from cache.
    /// </summary>
    public QuickBooksAccountInfo? GetAccountById(string accountId)
    {
        lock (_cacheLock)
        {
            return _cachedAccounts?.FirstOrDefault(a => a.Id == accountId);
        }
    }

    /// <summary>
    /// Clears the cached accounts.
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cachedAccounts = null;
        }
        _logger.LogInformation("Chart of Accounts cache cleared");
    }

    /// <summary>
    /// Checks if accounts are cached.
    /// </summary>
    public bool HasCachedAccounts()
    {
        lock (_cacheLock)
        {
            return _cachedAccounts?.Count > 0;
        }
    }

    /// <summary>
    /// Parses accounts from Intuit API response JSON.
    /// The response contains a QueryResponse with Account array.
    /// </summary>
    private List<QuickBooksAccountInfo> ParseAccountsFromResponse(string jsonContent)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            var accounts = new List<QuickBooksAccountInfo>();

            // Navigate to Account array in response
            if (root.TryGetProperty("QueryResponse", out var queryResponse))
            {
                if (queryResponse.TryGetProperty("Account", out var accountArray))
                {
                    foreach (var accountElement in accountArray.EnumerateArray())
                    {
                        var account = ParseAccountElement(accountElement);
                        if (account != null)
                        {
                            accounts.Add(account);
                        }
                    }
                }
            }

            _logger.LogDebug("Parsed {Count} accounts from API response", accounts.Count);
            return accounts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing accounts from API response");
            return new List<QuickBooksAccountInfo>();
        }
    }

    /// <summary>
    /// Parses a single account element from the JSON response.
    /// </summary>
    private QuickBooksAccountInfo? ParseAccountElement(JsonElement element)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var id = GetStringValue(element, "Id");
            var name = GetStringValue(element, "Name");

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
            {
                return null;
            }

            var account = new QuickBooksAccountInfo
            {
                Id = id,
                Name = name,
                AccountNumber = GetStringValue(element, "AcctNum"),
                Type = GetStringValue(element, "Type"),
                SubType = GetStringValue(element, "SubType"),
                CurrentBalance = GetDecimalValue(element, "CurrentBalance"),
                CurrencyId = GetStringValue(element, "CurrencyRef", "value"),
                Active = GetBoolValue(element, "Active", true),
                Description = GetStringValue(element, "Description"),
                Classification = GetStringValue(element, "Classification")
            };

            return account;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing individual account element");
            return null;
        }
    }

    /// <summary>
    /// Helper to extract string value from JSON element.
    /// </summary>
    private string? GetStringValue(JsonElement element, params string[] path)
    {
        try
        {
            var current = element;
            foreach (var key in path)
            {
                if (!current.TryGetProperty(key, out current))
                {
                    return null;
                }
            }
            return current.GetString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Helper to extract decimal value from JSON element.
    /// </summary>
    private decimal GetDecimalValue(JsonElement element, string key)
    {
        try
        {
            if (element.TryGetProperty(key, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number)
                {
                    return value.GetDecimal();
                }
                else if (value.ValueKind == JsonValueKind.String)
                {
                    var str = value.GetString();
                    if (decimal.TryParse(str, out var result))
                    {
                        return result;
                    }
                }
            }
        }
        catch { }
        return 0m;
    }

    /// <summary>
    /// Helper to extract boolean value from JSON element.
    /// </summary>
    private bool GetBoolValue(JsonElement element, string key, bool defaultValue)
    {
        try
        {
            if (element.TryGetProperty(key, out var value))
            {
                if (value.ValueKind == JsonValueKind.True) return true;
                if (value.ValueKind == JsonValueKind.False) return false;
            }
        }
        catch { }
        return defaultValue;
    }

    /// <summary>
    /// Gets the realm ID for API requests.
    /// </summary>
    private async Task<string?> GetRealmIdAsync(CancellationToken cancellationToken)
    {
        // Retrieve realm ID from token store (set during OAuth flow)
        await Task.CompletedTask;
        return _tokenStore?.GetRealmId();
    }
}
