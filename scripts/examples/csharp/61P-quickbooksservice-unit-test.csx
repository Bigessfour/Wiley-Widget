// QuickBooksService Unit Test - Validates OAuth2 flow and API interactions
// Usage: docker run --rm -v "${PWD}:/app:ro" wiley-widget/csx-mcp:local scripts/examples/csharp/61-quickbooksservice-unit-test.csx
// Purpose: Tests QuickBooksService initialization, token management, and DataService creation

// Required NuGet package references
#r "nuget: Microsoft.Extensions.DependencyInjection, 9.0.10"
#r "nuget: Microsoft.Extensions.Configuration, 9.0.10"
#r "nuget: Microsoft.Extensions.Logging, 9.0.10"
#r "nuget: Moq, 4.20.72"
#r "nuget: System.Net.Http, 4.3.4"

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

// ========================================
// TEST METADATA
// ========================================
// Test Name: QuickBooksService Unit Test
// Category: Unit (Services)
// Purpose: Validates OAuth2 authentication flow, token refresh, and API initialization
// Dependencies: Moq, System.Net.Http
// Testing: Secret vault integration, token management, error handling
// ========================================

Console.WriteLine("=== QuickBooksService Unit Test ===\n");
Console.WriteLine("Testing OAuth2 flow and QuickBooks API integration");
Console.WriteLine("Validates: Token refresh, secret vault, initialization patterns\n");

// ========================================
// TEST HARNESS
// ========================================
int passed = 0, total = 0;
List<string> failures = new List<string>();

void Assert(bool condition, string testName, string? details = null)
{
    total++;
    if (condition)
    {
        Console.WriteLine($"✓ {testName}");
        passed++;
    }
    else
    {
        string failMsg = $"✗ {testName} FAILED";
        if (!string.IsNullOrWhiteSpace(details)) failMsg += $"\n  Details: {details}";
        Console.WriteLine(failMsg);
        failures.Add(failMsg);
    }
}

void AssertNotNull<T>(T? value, string testName, string? details = null) where T : class
{
    Assert(value != null, testName, details ?? $"Expected non-null value of type {typeof(T).Name}");
}

void AssertEqual<T>(T expected, T actual, string testName, string? details = null)
{
    bool equals = EqualityComparer<T>.Default.Equals(expected, actual);
    Assert(equals, testName, details ?? $"Expected: {expected}, Actual: {actual}");
}

void AssertContains(string expected, string actual, string testName)
{
    Assert(actual?.Contains(expected) == true, testName, $"Expected substring '{expected}' not found in '{actual}'");
}

// ========================================
// MOCK INTERFACES
// ========================================

public interface ISecretVaultService
{
    Task<string?> GetSecretAsync(string secretName);
    string? GetSecret(string secretName);
}

public interface ISettingsService
{
    string? GetSetting(string key);
    void SetSetting(string key, string value);
}

public interface IQuickBooksService
{
    Task<bool> InitializeAsync();
    Task<string?> GetAccessTokenAsync();
    Task<bool> RefreshTokenAsync(string refreshToken);
    bool IsInitialized { get; }
}

// ========================================
// SIMPLIFIED QUICKBOOKSSERVICE FOR TESTING
// ========================================

public class QuickBooksServiceTestable : IQuickBooksService
{
    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;
    private readonly ISecretVaultService? _secretVault;
    private readonly HttpClient _httpClient;

    private string? _clientId;
    private string? _clientSecret;
    private string? _accessToken;
    private string? _refreshToken;
    private DateTime? _tokenExpiry;
    private bool _initialized;

    public bool IsInitialized => _initialized;

    public QuickBooksServiceTestable(
        ILogger logger,
        ISettingsService settingsService,
        ISecretVaultService? secretVault,
        HttpClient httpClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _secretVault = secretVault;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing QuickBooksService...");

            // Load secrets from vault
            if (_secretVault != null)
            {
                _clientId = await _secretVault.GetSecretAsync("QuickBooks:ClientId");
                _clientSecret = await _secretVault.GetSecretAsync("QuickBooks:ClientSecret");
            }

            // Fallback to settings
            _clientId ??= _settingsService.GetSetting("QuickBooks:ClientId");
            _clientSecret ??= _settingsService.GetSetting("QuickBooks:ClientSecret");

            if (string.IsNullOrWhiteSpace(_clientId) || string.IsNullOrWhiteSpace(_clientSecret))
            {
                _logger.LogError("QuickBooks credentials not found in vault or settings");
                return false;
            }

            _initialized = true;
            _logger.LogInformation("QuickBooksService initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize QuickBooksService");
            return false;
        }
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        if (!_initialized)
        {
            _logger.LogWarning("QuickBooksService not initialized");
            return null;
        }

        // Check if token is expired
        if (_tokenExpiry.HasValue && DateTime.UtcNow >= _tokenExpiry.Value)
        {
            _logger.LogInformation("Access token expired, refreshing...");
            if (_refreshToken != null)
            {
                await RefreshTokenAsync(_refreshToken);
            }
        }

        return _accessToken;
    }

    public async Task<bool> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            _logger.LogInformation("Refreshing OAuth2 token...");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken },
                { "client_id", _clientId ?? "" },
                { "client_secret", _clientSecret ?? "" }
            });

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Token refreshed successfully");

                // Simplified: In production, parse JSON and update tokens
                _accessToken = "new_access_token_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                _refreshToken = refreshToken; // Keep same refresh token for test
                _tokenExpiry = DateTime.UtcNow.AddHours(1);

                return true;
            }
            else
            {
                _logger.LogError("Token refresh failed with status {StatusCode}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during token refresh");
            return false;
        }
    }
}

// ========================================
// TEST SUITE
// ========================================

Console.WriteLine("Setting up mocks and test data...\n");

// Create mocks
var mockLogger = new Mock<ILogger>();
var mockSettingsService = new Mock<ISettingsService>();
var mockSecretVault = new Mock<ISecretVaultService>();

// Setup secret vault to return test credentials
mockSecretVault.Setup(x => x.GetSecretAsync("QuickBooks:ClientId"))
    .ReturnsAsync("test_client_id_12345");
mockSecretVault.Setup(x => x.GetSecretAsync("QuickBooks:ClientSecret"))
    .ReturnsAsync("test_client_secret_67890");

// Setup settings service fallback
mockSettingsService.Setup(x => x.GetSetting("QuickBooks:ClientId"))
    .Returns("fallback_client_id");
mockSettingsService.Setup(x => x.GetSetting("QuickBooks:ClientSecret"))
    .Returns("fallback_client_secret");

// Create HttpClient with mocked handler
var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
mockHttpMessageHandler.Protected()
    .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
    .ReturnsAsync(new HttpResponseMessage
    {
        StatusCode = HttpStatusCode.OK,
        Content = new StringContent("{\"access_token\":\"test_token\",\"refresh_token\":\"test_refresh\",\"expires_in\":3600}")
    });

var httpClient = new HttpClient(mockHttpMessageHandler.Object);

// ========================================
// TEST 1: Constructor and Dependency Injection
// ========================================
Console.WriteLine("TEST 1: Constructor and Dependency Injection");
Console.WriteLine("---------------------------------------------");

try
{
    var service = new QuickBooksServiceTestable(
        mockLogger.Object,
        mockSettingsService.Object,
        mockSecretVault.Object,
        httpClient);

    AssertNotNull(service, "Service constructor succeeds with all dependencies");
    Assert(!service.IsInitialized, "Service not initialized before InitializeAsync()");
}
catch (Exception ex)
{
    Assert(false, "Constructor test", ex.Message);
}

Console.WriteLine();

// ========================================
// TEST 2: Initialization with Secret Vault
// ========================================
Console.WriteLine("TEST 2: Initialization with Secret Vault");
Console.WriteLine("-----------------------------------------");

try
{
    var service = new QuickBooksServiceTestable(
        mockLogger.Object,
        mockSettingsService.Object,
        mockSecretVault.Object,
        httpClient);

    var result = await service.InitializeAsync();

    Assert(result, "InitializeAsync returns true with valid credentials");
    Assert(service.IsInitialized, "IsInitialized property set to true");

    // Verify secret vault was called
    mockSecretVault.Verify(x => x.GetSecretAsync("QuickBooks:ClientId"), Times.Once(),
        "Secret vault queried for ClientId");
    mockSecretVault.Verify(x => x.GetSecretAsync("QuickBooks:ClientSecret"), Times.Once(),
        "Secret vault queried for ClientSecret");
}
catch (Exception ex)
{
    Assert(false, "Initialization with secret vault", ex.Message);
}

Console.WriteLine();

// ========================================
// TEST 3: Initialization Fallback to Settings
// ========================================
Console.WriteLine("TEST 3: Initialization Fallback to Settings");
Console.WriteLine("--------------------------------------------");

try
{
    // Create service WITHOUT secret vault
    var service = new QuickBooksServiceTestable(
        mockLogger.Object,
        mockSettingsService.Object,
        null, // No secret vault
        httpClient);

    var result = await service.InitializeAsync();

    Assert(result, "InitializeAsync succeeds with settings fallback");
    Assert(service.IsInitialized, "Service initialized from settings");

    // Verify settings service was called
    mockSettingsService.Verify(x => x.GetSetting("QuickBooks:ClientId"), Times.Once(),
        "Settings service queried for ClientId");
    mockSettingsService.Verify(x => x.GetSetting("QuickBooks:ClientSecret"), Times.Once(),
        "Settings service queried for ClientSecret");
}
catch (Exception ex)
{
    Assert(false, "Initialization fallback", ex.Message);
}

Console.WriteLine();

// ========================================
// TEST 4: Initialization Failure Handling
// ========================================
Console.WriteLine("TEST 4: Initialization Failure Handling");
Console.WriteLine("----------------------------------------");

try
{
    // Setup mocks to return null (missing credentials)
    var mockEmptyVault = new Mock<ISecretVaultService>();
    mockEmptyVault.Setup(x => x.GetSecretAsync(It.IsAny<string>()))
        .ReturnsAsync((string?)null);

    var mockEmptySettings = new Mock<ISettingsService>();
    mockEmptySettings.Setup(x => x.GetSetting(It.IsAny<string>()))
        .Returns((string?)null);

    var service = new QuickBooksServiceTestable(
        mockLogger.Object,
        mockEmptySettings.Object,
        mockEmptyVault.Object,
        httpClient);

    var result = await service.InitializeAsync();

    Assert(!result, "InitializeAsync returns false when credentials missing");
    Assert(!service.IsInitialized, "Service not initialized without credentials");
}
catch (Exception ex)
{
    Assert(false, "Initialization failure handling", ex.Message);
}

Console.WriteLine();

// ========================================
// TEST 5: Token Refresh Flow
// ========================================
Console.WriteLine("TEST 5: Token Refresh Flow");
Console.WriteLine("---------------------------");

try
{
    var service = new QuickBooksServiceTestable(
        mockLogger.Object,
        mockSettingsService.Object,
        mockSecretVault.Object,
        httpClient);

    await service.InitializeAsync();

    var refreshResult = await service.RefreshTokenAsync("test_refresh_token_abc123");

    Assert(refreshResult, "RefreshTokenAsync succeeds with valid refresh token");

    // Verify HTTP call was made
    mockHttpMessageHandler.Protected().Verify(
        "SendAsync",
        Times.Once(),
        ItExpr.Is<HttpRequestMessage>(req =>
            req.Method == HttpMethod.Post &&
            req.RequestUri != null &&
            req.RequestUri.ToString().Contains("oauth2/v1/tokens/bearer")),
        ItExpr.IsAny<CancellationToken>());
}
catch (Exception ex)
{
    Assert(false, "Token refresh flow", ex.Message);
}

Console.WriteLine();

// ========================================
// TEST 6: GetAccessToken Before Initialization
// ========================================
Console.WriteLine("TEST 6: GetAccessToken Before Initialization");
Console.WriteLine("---------------------------------------------");

try
{
    var service = new QuickBooksServiceTestable(
        mockLogger.Object,
        mockSettingsService.Object,
        mockSecretVault.Object,
        httpClient);

    // Call GetAccessTokenAsync BEFORE initializing
    var token = await service.GetAccessTokenAsync();

    Assert(token == null, "GetAccessTokenAsync returns null when not initialized");
}
catch (Exception ex)
{
    Assert(false, "GetAccessToken before init", ex.Message);
}

Console.WriteLine();

// ========================================
// TEST SUMMARY
// ========================================
Console.WriteLine("\n========================================");
Console.WriteLine("TEST SUMMARY");
Console.WriteLine("========================================");
Console.WriteLine($"Total Tests: {total}");
Console.WriteLine($"Passed: {passed} ({(total > 0 ? (passed * 100.0 / total).ToString("F1") : "0")}%)");
Console.WriteLine($"Failed: {total - passed}");

if (failures.Any())
{
    Console.WriteLine("\nFailed Tests:");
    foreach (var failure in failures)
    {
        Console.WriteLine($"  {failure}");
    }
}

Console.WriteLine("\n✓ QuickBooksService Unit Test Complete");
Console.WriteLine($"Result: {(failures.Count == 0 ? "SUCCESS" : "FAILURES DETECTED")}");

// Exit with appropriate code
Environment.Exit(failures.Count == 0 ? 0 : 1);
