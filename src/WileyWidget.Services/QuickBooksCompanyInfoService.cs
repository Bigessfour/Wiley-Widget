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
            var host = _authService.GetEnvironment() == "sandbox"
                ? "sandbox-quickbooks.api.intuit.com"
                : "quickbooks.api.intuit.com";
            var url = $"https://{host}/v3/company/{realmId}/query?query={encodedQuery}&minorversion=65";

            // Use HttpRequestMessage to avoid mutating DefaultRequestHeaders (not thread-safe)
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenData.AccessToken);
            request.Headers.Accept.ParseAdd("application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
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

            if (!queryResponse.TryGetProperty("CompanyInfo", out var companyInfoNode))
            {
                _logger.LogWarning("No CompanyInfo in response");
                return null;
            }

            if (!TryGetCompanyInfoElement(companyInfoNode, out var companyInfoElement))
            {
                _logger.LogWarning("CompanyInfo payload is empty or has an unexpected JSON shape");
                return null;
            }

            var companyInfo = new QuickBooksCompanyInfo
            {
                CompanyName = GetStringValue(companyInfoElement, "CompanyName") ?? "Unknown",
                LegalName = GetStringValue(companyInfoElement, "LegalName"),
                PrimaryEmailAddress = GetStringValue(companyInfoElement, "PrimaryEmailAddr", "Address"),
                CountryCode = GetStringValue(companyInfoElement, "Country")
                    ?? GetStringValue(companyInfoElement, "CountrySubDivisionCode")
                    ?? GetStringValue(companyInfoElement, "CountrySubDivisionCode", "value"),
                TaxIdentifier = GetStringValue(companyInfoElement, "TaxIdentifier"),
                WebAddr = GetStringValue(companyInfoElement, "WebAddr", "URI")
                    ?? GetStringValue(companyInfoElement, "WebAddr", "value")
                    ?? GetStringValue(companyInfoElement, "WebAddr"),
                RealmId = realmId,
                CurrencyCode = GetStringValue(companyInfoElement, "CurrencyRef", "value")
                    ?? GetStringValue(companyInfoElement, "CurrencyRef", "name")
                    ?? GetStringValue(companyInfoElement, "CurrencyRef")
                    ?? "USD",
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

    private static bool TryGetCompanyInfoElement(JsonElement companyInfoNode, out JsonElement companyInfoElement)
    {
        switch (companyInfoNode.ValueKind)
        {
            case JsonValueKind.Array:
                companyInfoElement = companyInfoNode.EnumerateArray().FirstOrDefault();
                return companyInfoElement.ValueKind != JsonValueKind.Undefined;

            case JsonValueKind.Object:
                companyInfoElement = companyInfoNode;
                return true;

            default:
                companyInfoElement = default;
                return false;
        }
    }

    private static string? GetStringValue(JsonElement element, params string[] path)
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

            return TryConvertElementToString(current);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryConvertElementToString(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.ToString();

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var value = TryConvertElementToString(item);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                return null;

            case JsonValueKind.Object:
            {
                var preferredKeys = new[] { "value", "Value", "name", "Name", "Address", "URI", "Id", "id" };
                foreach (var key in preferredKeys)
                {
                    if (element.TryGetProperty(key, out var nested))
                    {
                        var nestedValue = TryConvertElementToString(nested);
                        if (!string.IsNullOrWhiteSpace(nestedValue))
                        {
                            return nestedValue;
                        }
                    }
                }

                return null;
            }

            default:
                return null;
        }
    }
}
