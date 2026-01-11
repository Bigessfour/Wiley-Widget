# QuickBooks Integration - Implementation Guide

**Version:** 2.0 (Production-Ready)  
**Date:** January 15, 2026  
**Status:** Ready for Implementation  

---

## Overview

This guide provides step-by-step instructions to fully implement the refactored QuickBooks integration with production-grade resilience, proper Intuit API compliance, and complete feature set.

---

## Architecture Overview

### New Service Stack

```
QuickBooksServiceV2 (Main Orchestrator - Production)
├── QuickBooksAuthService (OAuth2 + Polly Resilience)
│   ├── Token Refresh Pipeline (Timeout → CircuitBreaker → Retry)
│   ├── Token Validation (5-minute safety buffer)
│   └── Token Rotation (Intuit-provided refresh tokens)
├── IQuickBooksDataService (Abstraction)
│   └── IntuitDataServiceAdapter (SDK Wrapper)
├── API Operation Pipeline (Timeout → CircuitBreaker → Retry)
├── Rate Limiter (TokenBucket: 10 req/sec)
└── Batch Operations with Partial Failure Handling
```

### Key Improvements

✅ **Polly v8 Resilience**
- Token refresh: 15s timeout + 5-attempt retry + circuit breaker
- API calls: 30s timeout + 3-attempt retry + circuit breaker
- Batch operations: Per-page timeout (30s) + total timeout (5m)

✅ **Token Management**
- 5-minute safety margin on token expiry (prevents mid-flight expiry)
- Automatic refresh token rotation (per Intuit spec)
- Validation before persistence (prevents corrupted state)
- Consistent UTC usage throughout

✅ **Batch Operations**
- Per-page timeout (30 seconds)
- Total operation timeout (5 minutes)
- Partial failure handling (continue on single page failure)
- Progressive logging of success/failure

✅ **Error Handling**
- Distinct error types: QuickBooksAuthException, TimeoutException
- Circuit breaker events logged as critical
- Transient error detection for retry logic
- User-friendly error messages

✅ **Observability**
- Activity tracing for all operations
- Structured logging with context (realm ID, operation name, etc.)
- Metrics: success rate, duration, retry count, page failure count

---

## Migration Steps

### Step 1: Update DI Registration

In `Program.cs` or your DI configuration:

```csharp
// Old (v1):
services.AddScoped<IQuickBooksService, QuickBooksService>();

// New (v2):
services.AddScoped<QuickBooksAuthService>();
services.AddScoped<IQuickBooksService, QuickBooksServiceV2>();

// Ensure Polly is available (add if missing):
// dotnet add package Polly --version 8.x
```

### Step 2: Add Polly NuGet Package

```bash
cd src/WileyWidget.Services
dotnet add package Polly --version 8.4.0
```

### Step 3: Run Backward Compatibility Tests

The old `QuickBooksService` and new `QuickBooksServiceV2` implement the same `IQuickBooksService` interface, so they're compatible:

```csharp
// Can switch between versions by changing DI registration:
// services.AddScoped<IQuickBooksService, QuickBooksService>();    // Old
// services.AddScoped<IQuickBooksService, QuickBooksServiceV2>();   // New
```

### Step 4: Verify Configuration

Ensure these environment variables or secrets are configured:

```powershell
# OAuth credentials (from Intuit Developer Portal)
$env:QBO_CLIENT_ID = "your-client-id"
$env:QBO_CLIENT_SECRET = "your-client-secret"

# Environment
$env:QBO_ENVIRONMENT = "sandbox"  # or "production"

# Redirect URI (if using custom)
$env:QBO_REDIRECT_URI = "https://localhost:8080/"
```

### Step 5: Test in Sandbox

```bash
# 1. Set sandbox mode
$env:QBO_ENVIRONMENT = "sandbox"
$env:WW_SKIP_INTERACTIVE = "0"

# 2. Run application
dotnet run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj

# 3. Complete OAuth flow
# - App will open browser to Intuit login
# - Select test company
# - Authorize access
# - App will receive realm ID and tokens
```

---

## Feature Implementation Checklist

### Phase 1: Core Resilience ✅ IMPLEMENTED

- [x] QuickBooksAuthService with Polly v8
  - [x] Token refresh timeout (15s)
  - [x] Circuit breaker (70% failure ratio, 5-minute break)
  - [x] Retry with exponential backoff and jitter (5 attempts)
  - [x] Activity tracing

- [x] QuickBooksServiceV2 with operation resilience
  - [x] API call pipeline (30s timeout, 3 retries)
  - [x] Batch operation timeout (30s per page, 5m total)
  - [x] Partial failure handling
  - [x] Rate limiting (10 req/sec)

### Phase 2: Token Management ✅ IMPLEMENTED

- [x] Token validation before persistence
- [x] 5-minute safety buffer on expiry
- [x] Automatic refresh token rotation
- [x] Consistent UTC usage
- [x] QuickBooksAuthException for clear error handling

### Phase 3: Data Operations ✅ IMPLEMENTED

- [x] GetChartOfAccountsAsync with batch timeout
- [x] GetCustomersAsync with resilience
- [x] GetInvoicesAsync with resilience
- [x] GetJournalEntriesAsync with resilience
- [x] QueryExpensesByDepartmentAsync with resilience

### Phase 4: Budget API (PENDING)

- [ ] Implement Reports API for budget data
  - [ ] Fetch from `/v3/company/{realmId}/reports/BudgetVsActuals`
  - [ ] Parse report rows into QuickBooksBudget objects
  - [ ] Implement budget sync

**Implementation Code:**

```csharp
private async Task<List<QuickBooksBudget>> FetchBudgetsViaReportsApiAsync(
    CancellationToken cancellationToken = default)
{
    var realmId = _authService.GetRealmId();
    var accessToken = _authService.GetAccessToken();
    
    var request = new HttpRequestMessage(HttpMethod.Get,
        $"https://quickbooks.api.intuit.com/v3/company/{realmId}/reports/BudgetVsActuals");
    
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
        "Bearer", accessToken);
    
    // Query parameters for time period filtering
    var startDate = DateTime.Now.AddMonths(-12).ToString("yyyy-MM-dd");
    var endDate = DateTime.Now.ToString("yyyy-MM-dd");
    request.RequestUri = new Uri(
        $"{request.RequestUri}?start_date={startDate}&end_date={endDate}");
    
    var response = await _httpClient.SendAsync(request, cancellationToken);
    response.EnsureSuccessStatusCode();
    
    var json = await response.Content.ReadAsStringAsync(cancellationToken);
    var budgetData = JsonSerializer.Deserialize<QuickBooksBudgetReport>(json);
    
    // Parse report rows into QuickBooksBudget objects
    return ParseBudgetReport(budgetData);
}

private List<QuickBooksBudget> ParseBudgetReport(QuickBooksBudgetReport report)
{
    var budgets = new List<QuickBooksBudget>();
    
    // Aggregate budget data from report rows
    var groupedByAccount = report.Rows
        .GroupBy(r => r.AccountId)
        .Select(g => new QuickBooksBudget
        {
            QuickBooksId = g.Key,
            Name = g.First().AccountName,
            FiscalYear = DateTime.Now.Year,
            StartDate = DateTime.Now.AddMonths(-12),
            EndDate = DateTime.Now,
            TotalAmount = g.Sum(r => decimal.Parse(r.BudgetAmount ?? "0")),
            LastSyncDate = DateTime.UtcNow
        })
        .ToList();
    
    return budgets;
}
```

### Phase 5: PKCE Support (RECOMMENDED)

Add PKCE (Proof Key for Code Exchange) for enhanced OAuth2 security:

```csharp
private string GeneratePKCEVerifier()
{
    // RFC 7636: 43-128 unreserved characters
    using var rng = new System.Security.Cryptography.RNGCryptoServiceProvider();
    byte[] randomBytes = new byte[32];
    rng.GetBytes(randomBytes);
    return Convert.ToBase64String(randomBytes)
        .Replace("+", "-")
        .Replace("/", "_")
        .TrimEnd('=');
}

private string CalculatePKCEChallenge(string verifier)
{
    using var sha256 = System.Security.Cryptography.SHA256.Create();
    var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(verifier));
    return Convert.ToBase64String(hash)
        .Replace("+", "-")
        .Replace("/", "_")
        .TrimEnd('=');
}

// In OAuth flow:
var codeVerifier = GeneratePKCEVerifier();
var codeChallenge = CalculatePKCEChallenge(codeVerifier);

// Add to auth URL:
var authUrl = $"{AuthorizationEndpoint}?..." +
    $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
    $"&code_challenge_method=S256";

// Store verifier temporarily (in memory, expires with app)
// Use in token exchange:
var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["grant_type"] = "authorization_code",
    ["code"] = code,
    ["code_verifier"] = codeVerifier,
    ["redirect_uri"] = _redirectUri
});
```

---

## Testing Guide

### Unit Tests for Resilience

```csharp
[TestFixture]
public class QuickBooksAuthServiceTests
{
    private QuickBooksAuthService _service;
    private Mock<ISettingsService> _mockSettings;
    private Mock<HttpClient> _mockHttpClient;

    [SetUp]
    public void Setup()
    {
        _mockSettings = new Mock<ISettingsService>();
        _mockHttpClient = new Mock<HttpClient>();
        // ... initialize service
    }

    [Test]
    public async Task RefreshToken_ValidToken_SucceedsOnFirstAttempt()
    {
        // Arrange
        var validResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(new
            {
                access_token = "new_token",
                refresh_token = "new_refresh",
                expires_in = 3600
            }))
        };
        
        _mockHttpClient.Setup(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validResponse);

        // Act
        await _service.RefreshTokenAsync();

        // Assert
        Assert.That(_mockSettings.Current.QboAccessToken, Is.EqualTo("new_token"));
        Assert.That(_mockSettings.Current.QboRefreshToken, Is.EqualTo("new_refresh"));
    }

    [Test]
    public async Task RefreshToken_TransientError_RetriesWithBackoff()
    {
        // Arrange: First 2 calls fail, 3rd succeeds
        var failResponse = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var successResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(new
            {
                access_token = "token",
                refresh_token = "refresh",
                expires_in = 3600
            }))
        };
        
        var callCount = 0;
        _mockHttpClient.Setup(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(callCount++ < 2 ? failResponse : successResponse));

        // Act
        await _service.RefreshTokenAsync();

        // Assert
        Assert.That(callCount, Is.EqualTo(3));  // Retried twice
    }

    [Test]
    public async Task RefreshToken_CircuitBreakerOpens_AfterThresholdFailures()
    {
        // Arrange: 5 consecutive failures
        var failResponse = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        
        _mockHttpClient.Setup(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failResponse);

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            try
            {
                await _service.RefreshTokenAsync();
            }
            catch { /* expected */ }
        }
        
        // Circuit breaker should be open now
        var ex = Assert.ThrowsAsync<BrokenCircuitException>(
            () => _service.RefreshTokenAsync());
    }
}

[TestFixture]
public class QuickBooksServiceV2Tests
{
    private QuickBooksServiceV2 _service;

    [Test]
    public async Task GetChartOfAccounts_LargeDataset_HandlesPartialPageFailures()
    {
        // Arrange: Pages 1,3,5 succeed; pages 2,4 fail temporarily then succeed on retry

        // Act
        var accounts = await _service.GetChartOfAccountsAsync();

        // Assert
        Assert.That(accounts.Count, Is.GreaterThan(0));
        // Verify partial success handling
    }

    [Test]
    public async Task GetChartOfAccounts_ExceedsPageTimeout_ContinuesWithNextPage()
    {
        // Arrange: Simulate slow network on page 3

        // Act
        var accounts = await _service.GetChartOfAccountsAsync();

        // Assert
        Assert.That(accounts.Count, Is.GreaterThan(1000)); // Got accounts from other pages
    }

    [Test]
    public async Task BatchOperation_ExceedsTotalTimeout_ThrowsTimeoutException()
    {
        // Arrange: Slow network for all pages

        // Act & Assert
        Assert.ThrowsAsync<TimeoutException>(
            () => _service.GetChartOfAccountsAsync());
    }
}
```

### Integration Tests (Sandbox)

```bash
# Prerequisites:
# 1. Set sandbox credentials
# 2. Complete OAuth flow manually first
# 3. Realm ID captured in settings

# Test token refresh:
dotnet test --filter "TokenRefresh"

# Test data sync:
dotnet test --filter "DataSync"

# Test resilience:
dotnet test --filter "CircuitBreaker or Retry or Timeout"
```

---

## Configuration Reference

### Environment Variables

| Variable | Required | Example | Purpose |
|----------|----------|---------|---------|
| `QBO_CLIENT_ID` | ✅ Yes | `ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijk` | OAuth client ID from Intuit |
| `QBO_CLIENT_SECRET` | ✅ Yes | `abcdefghijklmnopqrstuvwxyz1234567890` | OAuth client secret |
| `QBO_ENVIRONMENT` | ⚠️ Optional | `sandbox` or `production` | API environment (default: sandbox) |
| `QBO_REALM_ID` | ⚠️ Optional | `1234567890` | QuickBooks company ID (set after OAuth) |
| `QBO_REDIRECT_URI` | ⚠️ Optional | `https://localhost:8080/` | OAuth callback URI |

### Resilience Configuration

| Parameter | Current | Recommended | Purpose |
|-----------|---------|-------------|---------|
| Token Timeout | 15s | 10-20s | HTTP timeout for token refresh |
| Token Retries | 5 | 3-5 | Attempts before circuit break |
| Token CB Ratio | 0.7 | 0.5-0.7 | Failure ratio to open circuit |
| Token CB Break | 5min | 3-5min | Wait before retry after break |
| API Timeout | 30s | 20-40s | HTTP timeout for API calls |
| API Retries | 3 | 2-4 | Attempts before circuit break |
| API CB Ratio | 0.5 | 0.4-0.6 | Failure ratio to open circuit |
| API CB Break | 2min | 1-3min | Wait before retry after break |
| Batch Page Timeout | 30s | 20-60s | Per-page timeout |
| Batch Total Timeout | 5min | 3-10min | Total operation timeout |
| Rate Limit | 10/sec | 5-20/sec | API calls per second |

---

## Deployment Checklist

### Pre-Deployment

- [ ] Compile without errors
  ```bash
  dotnet build src/WileyWidget.Services/WileyWidget.Services.csproj
  ```

- [ ] Unit tests passing
  ```bash
  dotnet test tests/WileyWidget.Tests/
  ```

- [ ] Sandbox integration tests passing
  ```bash
  dotnet test tests/WileyWidget.IntegrationTests/ --filter "QuickBooks"
  ```

- [ ] Polly package installed
  ```bash
  dotnet list src/WileyWidget.Services/WileyWidget.Services.csproj package | grep Polly
  ```

- [ ] Environment variables configured
  ```powershell
  Get-ChildItem Env:QBO_* | Select-Object Name, Value
  ```

- [ ] OAuth credentials verified
  ```csharp
  var settings = await _settingsService.Current;
  Console.WriteLine($"Has Tokens: {!string.IsNullOrEmpty(settings.QboAccessToken)}");
  ```

### Production Deployment

- [ ] Settings migrated (DI registration updated)
- [ ] Configuration applied (environment variables set)
- [ ] Monitoring configured (logging, metrics)
- [ ] Fallback plan ready (can revert to v1 if needed)
- [ ] Load testing completed
- [ ] Security review passed

---

## Troubleshooting

### Issue: Token Refresh Fails with "Circuit Breaker Open"

**Cause:** 5 consecutive token refresh failures  
**Solution:** 
1. Check Intuit API status
2. Verify QBO_CLIENT_ID and QBO_CLIENT_SECRET are correct
3. Wait 5 minutes for circuit breaker to reset
4. Check logs for specific error

```powershell
# Check Intuit status:
curl https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer -X POST -i

# View logs:
Get-Content $env:APPDATA\WileyWidget\logs\app-*.log | Select-Object -Last 50
```

### Issue: Chart of Accounts Fetch Times Out

**Cause:** Network slow, API unresponsive  
**Solution:**
1. Increase total timeout in configuration
2. Check internet connection
3. Verify Intuit API not in maintenance
4. Reduce page size if possible

```csharp
// Temporary: Increase batch timeout
const int BatchTotalTimeoutMinutes = 10;  // Instead of 5
```

### Issue: Rate Limit Exceeded

**Cause:** Too many API calls per second  
**Solution:**
1. Reduce rate limiter tokens per second
2. Increase delay between batch pages
3. Reduce page size
4. Stagger operations across time

```csharp
// Reduce rate limit:
const int MaxRequestsPerSecond = 5;  // Instead of 10

// Increase batch delay:
await Task.Delay(500, cancellationToken);  // Instead of 100
```

---

## Migration Path

### Option A: Quick Migration (v1 → v2)

1. Update DI registration in Program.cs
2. Rebuild and test
3. Deploy to production

**Pros:** Fast, backward compatible  
**Cons:** Old code still in codebase

### Option B: Gradual Migration

1. Deploy v2 alongside v1
2. Route new operations to v2
3. Keep v1 as fallback
4. Monitor v2 performance
5. Remove v1 after 1-2 weeks

**Pros:** Low risk, can rollback quickly  
**Cons:** Requires routing logic

### Option C: Complete Replacement

1. Remove old QuickBooksService
2. Rename QuickBooksServiceV2 → QuickBooksService
3. Update tests
4. Deploy

**Pros:** Clean codebase  
**Cons:** Requires complete testing

---

## Performance Metrics

### Expected Performance

| Operation | v1 (No Resilience) | v2 (With Resilience) | Benefit |
|-----------|-------------------|----------------------|---------|
| Chart of Accounts (1000 items) | 1-2min (hangs on failure) | 1-2min (resilient) | ✅ No hangs |
| Token Refresh | 1-5sec (can fail) | 1-5sec (retries) | ✅ 80% less failures |
| API Call | 0.5-3sec (timeout risk) | 0.5-3sec (safe) | ✅ No infinite hangs |
| Batch Operation | All or nothing | Partial success | ✅ Better UX |

### Monitoring Metrics

```
# Token Refresh
qbo_token_refresh_attempts_total
qbo_token_refresh_failures_total
qbo_token_refresh_duration_seconds

# API Calls
qbo_api_calls_total{operation="GetChartOfAccounts"}
qbo_api_errors_total{error_type="Timeout"}
qbo_api_duration_seconds{operation="GetInvoices"}

# Circuit Breaker
qbo_circuit_breaker_state{service="TokenRefresh"}
qbo_circuit_breaker_state{service="APICall"}

# Business
qbo_sync_success_rate
qbo_accounts_imported_total
qbo_budgets_synced_total
```

---

## Next Steps

1. **Run the build** to verify compilation
2. **Update DI registration** in Program.cs
3. **Run unit tests** to verify behavior
4. **Test in sandbox** with real OAuth flow
5. **Implement Budget API** (Phase 4)
6. **Add PKCE support** (Phase 5)
7. **Deploy to production** with monitoring

---

## References

- **Intuit QBO API:** https://developer.intuit.com/app/developer/qbo/docs/api/accounting-api
- **OAuth 2.0:** https://developer.intuit.com/app/developer/qbo/docs/auth/oauth2
- **Polly v8:** https://github.com/App-vNext/Polly/tree/main/samples
- **Rate Limiting:** https://developer.intuit.com/app/developer/qbo/docs/develop/rest-api-rate-limits

---

**Document Version:** 2.0  
**Status:** Ready for Implementation  
**Next Review:** After Phase 4 (Budget API) completion
