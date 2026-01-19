using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of IQuickBooksCompanyInfoService using QuickBooks CompanyInfo query.
/// Retrieves company/realm information and caches it for subsequent API calls.
/// </summary>
public sealed class QuickBooksCompanyInfoService : IQuickBooksCompanyInfoService
{
    private readonly HttpClient _httpClient;
    private readonly IQuickBooksAuthService _authService;
    private readonly QuickBooksTokenStore? _tokenStore;
    private readonly ILogger<QuickBooksCompanyInfoService> _logger;
    private readonly IMemoryCache _cache;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string CompanyInfoCacheKey = "qb_company_info";

    public QuickBooksCompanyInfoService(
        ILogger<QuickBooksCompanyInfoService> logger,
        IQuickBooksAuthService authService,
        QuickBooksTokenStore? tokenStore,
        HttpClient httpClient,
        IMemoryCache cache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _tokenStore = tokenStore;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    /// <summary>
    /// Retrieves company information from QuickBooks CompanyInfo query.
    /// First checks cache, then makes API call if needed.
    /// </summary>
    public async Task<QuickBooksCompanyInfo?> GetCompanyInfoAsync(CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_cache.TryGetValue(CompanyInfoCacheKey, out QuickBooksCompanyInfo? cachedInfo) && cachedInfo != null)
        {
            _logger.LogInformation("Returning cached company info: {CompanyName}", cachedInfo.CompanyName);
            return cachedInfo;
        }

        try
        {
            // Get valid access token
            var tokenData = await _authService.GetAccessTokenAsync(cancellationToken);
            if (tokenData?.IsValid != true)
            {
                _logger.LogWarning("No valid QuickBooks access token available");
                return null;
            }

            // Get realm ID from token store
            var realmId = _tokenStore?.GetRealmId();
            if (string.IsNullOrEmpty(realmId))
            {
                _logger.LogWarning("No QuickBooks realm ID available - call OAuth flow first");
                return null;
            }

            // Build CompanyInfo query
            var query = "SELECT * FROM CompanyInfo";
            var encodedQuery = Uri.EscapeDataString(query);
            var url = $"https://quickbooks.api.intuit.com/v2/company/{realmId}/query?query={encodedQuery}";

            // Set authorization header
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenData.AccessToken);

            var response = await _httpClient.GetAsync(new Uri(url), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to retrieve company info: {StatusCode} - {Content}",
                    response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var companyInfo = ParseCompanyInfoFromResponse(jsonContent, realmId);

            if (companyInfo != null)
            {
                // Cache for 1 hour (company info changes infrequently)
                _cache.Set(CompanyInfoCacheKey, companyInfo, TimeSpan.FromHours(1));
                _logger.LogInformation("Retrieved and cached company info: {CompanyName} (RealmId: {RealmId})",
                    companyInfo.CompanyName, companyInfo.RealmId);
            }

            return companyInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company info from QuickBooks");
            return null;
        }
    }

    /// <summary>
    /// Returns cached company information without making an API call.
    /// </summary>
    public QuickBooksCompanyInfo? GetCachedCompanyInfo()
    {
        if (_cache.TryGetValue(CompanyInfoCacheKey, out QuickBooksCompanyInfo? cachedInfo))
        {
            return cachedInfo;
        }
        return null;
    }

    /// <summary>
    /// Clears the company info cache.
    /// </summary>
    public void ClearCache()
    {
        _cache.Remove(CompanyInfoCacheKey);
        _logger.LogInformation("Cleared company info cache");
    }

    /// <summary>
    /// Parses QuickBooks API response JSON to extract company info.
    /// </summary>
    private QuickBooksCompanyInfo? ParseCompanyInfoFromResponse(string jsonContent, string realmId)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // QuickBooks returns { QueryResponse: { CompanyInfo: [...] } }
            if (!root.TryGetProperty("QueryResponse", out var queryResponse))
            {
                _logger.LogWarning("Unexpected API response format: missing QueryResponse");
                return null;
            }

            if (!queryResponse.TryGetProperty("CompanyInfo", out var companyInfoArray))
            {
                _logger.LogWarning("No CompanyInfo in response");
                return null;
            }

            // CompanyInfo query returns array with single element
            var companyInfoElement = companyInfoArray.EnumerateArray().FirstOrDefault();
            if (companyInfoElement.ValueKind == JsonValueKind.Undefined)
            {
                _logger.LogWarning("CompanyInfo array is empty");
                return null;
            }

            var companyInfo = new QuickBooksCompanyInfo
            {
                CompanyName = companyInfoElement.TryGetProperty("CompanyName", out var name)
                    ? name.GetString() ?? "Unknown"
                    : "Unknown",
                LegalName = companyInfoElement.TryGetProperty("LegalName", out var legalName)
                    ? legalName.GetString()
                    : null,
                PrimaryEmailAddress = companyInfoElement.TryGetProperty("PrimaryEmailAddr", out var email) &&
                                     email.TryGetProperty("Address", out var emailAddr)
                    ? emailAddr.GetString()
                    : null,
                CountryCode = companyInfoElement.TryGetProperty("Country", out var country)
                    ? country.GetString()
                    : null,
                TaxIdentifier = companyInfoElement.TryGetProperty("TaxIdentifier", out var taxId)
                    ? taxId.GetString()
                    : null,
                WebAddr = companyInfoElement.TryGetProperty("WebAddr", out var web)
                    ? web.GetString()
                    : null,
                RealmId = realmId,
                CurrencyCode = companyInfoElement.TryGetProperty("CurrencyRef", out var currencyRef) &&
                              currencyRef.TryGetProperty("value", out var currencyValue)
                    ? currencyValue.GetString()
                    : "USD",
                FetchedAtUtc = DateTime.UtcNow,
            };

            return companyInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing company info from JSON response");
            return null;
        }
    }
}
