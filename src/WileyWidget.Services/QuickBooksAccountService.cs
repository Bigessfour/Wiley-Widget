using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of IQuickBooksAccountService using QuickBooks REST API v3.
/// Handles account retrieval, caching, and balance calculations.
/// </summary>
public sealed class QuickBooksAccountService : IQuickBooksAccountService
{
    private readonly HttpClient _httpClient;
    private readonly IQuickBooksAuthService _authService;
    private readonly ILogger<QuickBooksAccountService> _logger;
    private readonly IMemoryCache _cache;
    private readonly QuickBooksTokenStore _tokenStore;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string AccountsCacheKey = "qb_accounts";
    private const string CacheDurationMinutes = "15";

    public QuickBooksAccountService(
        HttpClient httpClient,
        IQuickBooksAuthService authService,
        ILogger<QuickBooksAccountService> logger,
        IMemoryCache cache,
        QuickBooksTokenStore tokenStore)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    /// <summary>
    /// Retrieves all accounts from QuickBooks Chart of Accounts query.
    /// First checks cache, then makes API call if needed.
    /// </summary>
    public async Task<List<QuickBooksAccount>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_cache.TryGetValue(AccountsCacheKey, out List<QuickBooksAccount>? cachedAccounts) && cachedAccounts != null)
        {
            _logger.LogInformation("Returning {Count} cached accounts", cachedAccounts.Count);
            return cachedAccounts;
        }

        try
        {
            // Get valid access token
            var tokenData = await _authService.GetAccessTokenAsync(cancellationToken);
            if (tokenData?.IsValid != true)
            {
                _logger.LogWarning("No valid QuickBooks access token available");
                return new List<QuickBooksAccount>();
            }

            // Note: Company/Realm ID would need to be stored from OAuth callback
            // For now, we'll use a placeholder - this would be populated from company info
            var realmId = await GetRealmIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(realmId))
            {
                _logger.LogWarning("No QuickBooks realm ID available");
                return new List<QuickBooksAccount>();
            }

            // Build Chart of Accounts query
            var query = "SELECT * FROM Account";
            var encodedQuery = Uri.EscapeDataString(query);
            var host = _authService.GetEnvironment() == "sandbox"
                ? "sandbox-quickbooks.api.intuit.com"
                : "quickbooks.api.intuit.com";
            // minorversion=65 targets a stable, well-defined API surface
            var url = $"https://{host}/v3/company/{realmId}/query?query={encodedQuery}&minorversion=65";

            // Use per-request HttpRequestMessage to avoid mutating DefaultRequestHeaders (not thread-safe)
            using var accountRequest = new HttpRequestMessage(HttpMethod.Get, url);
            accountRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenData.AccessToken);
            accountRequest.Headers.Accept.ParseAdd("application/json");

            var response = await _httpClient.SendAsync(accountRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to retrieve accounts: {StatusCode} - {Content}",
                    response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
                return new List<QuickBooksAccount>();
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var accounts = ParseAccountsFromResponse(jsonContent);

            // Cache for 15 minutes
            _cache.Set(AccountsCacheKey, accounts, TimeSpan.FromMinutes(15));

            _logger.LogInformation("Retrieved and cached {Count} accounts from QuickBooks", accounts.Count);
            return accounts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accounts from QuickBooks");
            return new List<QuickBooksAccount>();
        }
    }

    /// <summary>
    /// Retrieves accounts filtered by classification type.
    /// </summary>
    public async Task<List<QuickBooksAccount>> GetAccountsByClassificationAsync(
        string classification, CancellationToken cancellationToken = default)
    {
        var allAccounts = await GetAccountsAsync(cancellationToken);
        return allAccounts
            .Where(a => a.Classification?.Equals(classification, StringComparison.OrdinalIgnoreCase) ?? false)
            .ToList();
    }

    /// <summary>
    /// Gets the current balance for a specific account.
    /// Returns null if account not found.
    /// </summary>
    public async Task<decimal?> GetAccountBalanceAsync(string accountId, CancellationToken cancellationToken = default)
    {
        var accounts = await GetAccountsAsync(cancellationToken);
        var account = accounts.FirstOrDefault(a => a.AccountId == accountId);
        return account?.CurrentBalance;
    }

    /// <summary>
    /// Returns cached accounts without making an API call.
    /// </summary>
    public List<QuickBooksAccount> GetCachedAccounts()
    {
        if (_cache.TryGetValue(AccountsCacheKey, out List<QuickBooksAccount>? cachedAccounts) && cachedAccounts != null)
        {
            return cachedAccounts;
        }
        return new List<QuickBooksAccount>();
    }

    /// <summary>
    /// Clears the account cache.
    /// </summary>
    public void ClearCache()
    {
        _cache.Remove(AccountsCacheKey);
        _logger.LogInformation("Cleared account cache");
    }

    /// <summary>
    /// Parses QuickBooks API response JSON to extract account objects.
    /// </summary>
    private List<QuickBooksAccount> ParseAccountsFromResponse(string jsonContent)
    {
        var accounts = new List<QuickBooksAccount>();

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // QuickBooks returns { QueryResponse: { Account: [...] } }
            if (!root.TryGetProperty("QueryResponse", out var queryResponse))
            {
                _logger.LogWarning("Unexpected API response format: missing QueryResponse");
                return accounts;
            }

            if (!queryResponse.TryGetProperty("Account", out var accountsArray))
            {
                _logger.LogInformation("No accounts returned in response");
                return accounts;
            }

            foreach (var accountElement in accountsArray.EnumerateArray())
            {
                try
                {
                    var account = new QuickBooksAccount
                    {
                        AccountId = accountElement.GetProperty("Id").GetString() ?? "",
                        Name = accountElement.GetProperty("Name").GetString() ?? "",
                        Description = accountElement.TryGetProperty("Description", out var desc)
                            ? desc.GetString()
                            : null,
                        AccountType = accountElement.GetProperty("AccountType").GetString() ?? "",
                        Classification = accountElement.TryGetProperty("Classification", out var classif)
                            ? classif.GetString()
                            : null,
                        AccountSubType = accountElement.TryGetProperty("AccountSubType", out var subtype)
                            ? subtype.GetString()
                            : null,
                        CurrentBalance = accountElement.TryGetProperty("CurrentBalance", out var balance)
                            ? balance.GetDecimal()
                            : 0m,
                        SyncToken = accountElement.TryGetProperty("SyncToken", out var sync)
                            ? sync.GetString()
                            : null,
                        Active = accountElement.TryGetProperty("Active", out var active)
                            ? active.GetBoolean()
                            : true,
                        CurrencyCode = accountElement.TryGetProperty("CurrencyRef", out var currencyRef) &&
                                      currencyRef.TryGetProperty("value", out var currencyValue)
                            ? currencyValue.GetString()
                            : "USD",
                        FetchedAtUtc = DateTime.UtcNow,
                    };

                    accounts.Add(account);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing individual account from response");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing accounts from JSON response");
        }

        return accounts;
    }

    /// <summary>
    /// Gets the QuickBooks realm ID (company ID).
    /// Retrieved from token store which is populated during OAuth callback.
    /// </summary>
    private Task<string?> GetRealmIdAsync(CancellationToken cancellationToken = default)
    {
        var realmId = _tokenStore.GetRealmId();
        if (string.IsNullOrEmpty(realmId))
        {
            _logger.LogWarning("RealmId not available in token store; ensure OAuth flow has completed");
        }
        return Task.FromResult(realmId);
    }
}
