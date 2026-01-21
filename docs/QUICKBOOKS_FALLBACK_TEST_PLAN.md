# QuickBooks Fallback & Sync Test Plan

**Date:** January 17, 2026
**Version:** 1.0
**Scope:** Testing QuickBooks account synchronization with fallback data mechanisms

---

## Overview

This test plan validates:

1. ✅ **Sandbox Sync** - Account retrieval from QuickBooks sandbox environment
2. ✅ **Fallback Data** - Graceful degradation when sync fails (uses cached/sample accounts)
3. ✅ **Token Refresh** - Automatic token refresh before API calls
4. ✅ **Token Revocation** - Revoke token and test forced re-authorization flow
5. ✅ **UI Integration** - "Sync Now" button in ribbon triggers manual sync
6. ✅ **Error Handling** - Clear error messaging for various failure scenarios

---

## Test Environment Setup

### Prerequisites

Before running any tests, ensure:

```powershell
# 1. QuickBooks Sandbox credentials configured
$env:QBO_CLIENT_ID = "your-client-id"
$env:QBO_CLIENT_SECRET = "your-client-secret"
$env:QBO_REDIRECT_URI = "http://localhost:8080/callback"
$env:QBO_REALM_ID = "your-realm-id"  # From OAuth callback
$env:QBO_ENVIRONMENT = "sandbox"

# 2. Run debug build
dotnet build WileyWidget.sln -c Debug

# 3. Verify database schema exists
dotnet ef database update
```

### Test Data

Sample QuickBooks sandbox company structure:

- **10 Default Accounts** (provided by Intuit sandbox)
- **4+ Custom Accounts** (created for testing)
- **Multiple account types** (Bank, Asset, Liability, Equity, Income, Expense)

---

## Test Cases

### Test 1: Sandbox Account Sync (Happy Path)

**Objective:** Verify accounts successfully sync from QuickBooks sandbox

**Steps:**

1. Launch WileyWidget application in debug mode
2. Navigate to Dashboard tab
3. Verify you are authorized to QuickBooks (check status indicator)
4. Click **"Sync Now"** button in ribbon (if not authorized, complete OAuth first)
5. Wait for sync to complete (progress indicator should show activity)
6. Verify Dashboard grid displays accounts

**Expected Results:**

✅ Sync completes in < 5 seconds
✅ Dashboard shows 14+ accounts (default + custom)
✅ Account columns visible:

- Account Name (e.g., "Operating Account")
- Account Number (e.g., "1000")
- Account Type (e.g., "Bank")
- Balance (current balance)
- Status message in UI: "✓ Synced X accounts in YYYms"

**Pass Criteria:**

- All accounts displayed in grid
- No error messages
- Sync duration logged
- Fallback indicator NOT shown

**Code Reference:**

```csharp
// MainViewModel.SyncQuickBooksAccountsAsync()
// Calls: IQuickBooksService.SyncAccountsAsync()
// Success path: DisplaysAccounts in Dashboard
```

---

### Test 2: Fallback Accounts on Sync Failure

**Objective:** Verify fallback/sample accounts display when sync fails

**Steps:**

1. Create a test scenario that forces sync failure:
   - Option A: Disconnect network (or use Fiddler to simulate network error)
   - Option B: Modify QuickBooksService to inject a test failure
   - Option C: Use expired/invalid token

2. Click **"Sync Now"** button
3. Wait for sync to timeout/fail
4. Observe Dashboard grid

**Expected Results:**

⚠️ Sync fails after 15 seconds (timeout)
✅ Dashboard displays 8 fallback accounts instead (see GetFallbackAccounts())
✅ Each fallback account marked with **[FALLBACK]** prefix in name
✅ Error message shows: **"⚠ Sync failed - showing X fallback accounts. Original error: ..."**
✅ Grid remains functional (not blank)

**Fallback Accounts Provided:**

1. [FALLBACK] Operating Account (Bank)
2. [FALLBACK] Equipment (Fixed Asset)
3. [FALLBACK] Accounts Payable (AP)
4. [FALLBACK] Retained Earnings (Equity)
5. [FALLBACK] Revenue (Income)
6. [FALLBACK] Salaries Expense (Expense)
7. [FALLBACK] Utilities Expense (Expense)
8. [FALLBACK] Depreciation Expense (Expense)

**Pass Criteria:**

- Fallback accounts appear in grid
- Error message indicates fallback mode
- All 8 fallback accounts present
- Grid is not empty/broken

**Code Reference:**

```csharp
// QuickBooksService.GetFallbackAccounts()
// Called from SyncAccountsAsync() catch block
// Returns: List<Account> with 8 sample accounts
```

---

### Test 3: Token Refresh on Sync

**Objective:** Verify automatic token refresh before API calls

**Steps:**

1. Authorize application (complete OAuth flow if needed)
2. Check token expiry in logs: "Token expires at: [timestamp]"
3. Manually set token expiry to NOW + 2 minutes (modify AppSettings)
   ```csharp
   var settings = _settingsService.Current;
   settings.QboTokenExpiry = DateTime.UtcNow.AddMinutes(2);
   _settingsService.Save();
   ```
4. Wait 3 minutes
5. Click **"Sync Now"** button
6. Monitor log output

**Expected Results:**

✅ Logs show: **"Token will expire soon, refreshing"**
✅ Before API call: **"Successfully refreshed QBO tokens (expires [new-time], buffer: 300s)"**
✅ Token refreshed with 5-minute safety buffer (not expired during call)
✅ Sync completes successfully with new token
✅ No user re-authorization required

**Pass Criteria:**

- Token automatically refreshed before expiry
- No manual OAuth re-entry needed
- Sync succeeds with refreshed token
- Expiry buffer respected (300 seconds before actual expiry)

**Code Reference:**

```csharp
// QuickBooksAuthService.RefreshTokenIfNeededAsync()
// Checks: HasValidAccessToken() (5-min buffer)
// Calls: RefreshTokenAsync() with Polly pipeline
```

---

### Test 4: Revoke Token & Re-Authorization Flow

**Objective:** Verify token revocation and forced re-authorization

**Steps:**

1. Authorize application (have valid token)
2. Open Settings → QuickBooks Integration tab
3. Click **"Revoke Token"** button (if available)
   - Or manually delete token: `%APPDATA%\WileyWidget\tokens.json`
4. Return to Dashboard
5. Click **"Sync Now"** button
6. Observe OAuth flow

**Expected Results:**

✅ Token successfully revoked on Intuit servers
✅ Token deleted from local storage
✅ Clicking "Sync Now" triggers **new OAuth authorization flow**
✅ Browser opens to Intuit login: `https://appcenter.intuit.com/connect/oauth2?client_id=...`
✅ After login, code exchanged for new token
✅ Sync completes with new token
✅ User only prompted **once** (not repeatedly)

**Pass Criteria:**

- Revocation succeeds
- Token deleted from storage
- OAuth flow triggered automatically
- No error dialogs
- Single re-authorization required

**Code Reference:**

```csharp
// IQuickBooksAuthService.RevokeTokenAsync()
// Calls: https://developer.intuit.com/v2/oauth
// QuickBooksService.AcquireTokensInteractiveAsync()
// Launches browser for authorization
```

---

### Test 5: Ribbon "Sync Now" Button Integration

**Objective:** Verify "Sync Now" button exists in ribbon and is wired correctly

**Steps:**

1. Launch WileyWidget in runtime mode
2. Look at main ribbon/toolbar
3. Find **"Sync Now"** button
4. Hover over button (tooltip should appear)
5. Click button
6. Observe sync in progress

**Expected Results:**

✅ "Sync Now" button visible in ribbon
✅ Tooltip shows: **"Manually sync accounts from QuickBooks"**
✅ Button disabled while sync in progress (prevents double-clicks)
✅ Clicking triggers `MainViewModel.SyncQuickBooksAccountsCommand`
✅ Progress indicator shows during sync
✅ Status updated in Dashboard after sync completes

**Pass Criteria:**

- Button visible and accessible
- Button wired to correct command
- Button disabled during operation
- Sync status displayed

**Code Reference:**

```csharp
// MainViewModel.SyncQuickBooksAccountsCommand
// Bound to: SyncQuickBooksAccountsAsync()
// Calls: IQuickBooksService.SyncAccountsAsync()
```

---

### Test 6: Error Handling & Message Display

**Objective:** Verify appropriate error messages for different failure scenarios

**Steps:**

Run each scenario and observe error message:

#### Scenario 6A: Network Error

- Disconnect network
- Click "Sync Now"
- Expected message: **"✗ Sync failed: Network error / timeout"**

#### Scenario 6B: Invalid Token

- Revoke token via Intuit
- Keep old token in storage (simulate)
- Click "Sync Now"
- Expected message: **"✗ Sync failed: Invalid OAuth token"** (triggers re-auth)

#### Scenario 6C: Cancelled Operation

- Click "Sync Now"
- Immediately close application
- Expected message: **"Sync cancelled by user"**

#### Scenario 6D: Fallback Success

- Trigger sync failure (see Test 2)
- Expected message: **"⚠ Sync failed - showing 8 fallback accounts. ..."**

**Expected Results:**

✅ Clear, user-friendly error messages
✅ Different messages for different failure types
✅ Fallback accounts shown when appropriate
✅ No technical stack traces in UI
✅ Suggestions for remediation when applicable

**Pass Criteria:**

- Errors displayed appropriately
- Messages are actionable
- No exception details leaked to UI
- Fallback shown when available

**Code Reference:**

```csharp
// MainViewModel.SyncQuickBooksAccountsAsync()
// Catches: OperationCanceledException, QuickBooksAuthException, Exception
// Displays: Formatted error messages to UI
```

---

### Test 7: Concurrent Operations & Rate Limiting

**Objective:** Verify rate limiting prevents API throttling

**Steps:**

1. Click "Sync Now" 5 times rapidly (within 1 second)
2. Monitor logs for rate limiter messages
3. Observe sync operations queue

**Expected Results:**

✅ First request processes immediately
✅ Subsequent requests queue (not lost)
✅ Logs show: **"Rate limiter: 5 requests queued"**
✅ Requests process at max 10 req/sec (per TokenBucketRateLimiter)
✅ All 5 syncs eventually complete (no errors)
✅ No "429 Too Many Requests" from QuickBooks API

**Pass Criteria:**

- Rate limiter engaged
- Requests queued properly
- All operations complete
- No API throttling errors

**Code Reference:**

```csharp
// QuickBooksService._rateLimiter
// Type: TokenBucketRateLimiter
// Config: 10 tokens/sec, queue up to 100 requests
```

---

### Test 8: Integration Test (Automated)

**Objective:** Run automated test suite to verify all components

**Command:**

```powershell
# Run QuickBooks integration tests
dotnet test tests/WileyWidget.Tests/QuickBooksIntegrationTests.cs -v normal

# Or specific test method
dotnet test tests/WileyWidget.Tests/QuickBooksIntegrationTests.cs::WileyWidget.Tests.QuickBooksIntegrationTests::TestSyncAccountsAsync
```

**Expected Output:**

```
✓ TestSyncAccountsAsync - PASSED (2543ms)
✓ TestFallbackAccountsLoaded - PASSED (156ms)
✓ TestTokenRefreshBeforeSync - PASSED (3102ms)
✓ TestRevokeTokenFlow - PASSED (4521ms)
✓ TestErrorHandling - PASSED (987ms)
✓ TestRateLimiting - PASSED (2134ms)

Test Run Summary: 6 passed, 0 failed
```

**Pass Criteria:**

- All tests pass
- No timeout failures
- Token operations verify
- Fallback tested

---

## Regression Test Checklist

After any code changes to QuickBooks service, run these smoke tests:

- [ ] Sync succeeds with valid token
- [ ] Fallback displays on failure
- [ ] Token auto-refresh works
- [ ] Error messages clear
- [ ] Rate limiting respected
- [ ] No performance regression (sync < 5 sec)
- [ ] Dashboard grid renders correctly
- [ ] Ribbon button functional

---

## Known Limitations

1. **Sandbox Only:** Tests use QuickBooks sandbox (separate from production data)
2. **Manual Token:** Initial OAuth requires manual browser login (cannot be automated)
3. **Rate Limits:** QuickBooks API limit is 200 req/min (we throttle to 10/sec for safety)
4. **Test Data:** Sandbox data resets every 30 days (plan accordingly)
5. **Fallback Accounts:** Sample data is hardcoded (not user-specific)

---

## Troubleshooting

### "OAuth Token Expired" on Every Sync

**Cause:** Token expiry timestamp incorrect
**Fix:**

```csharp
// Verify token has been set
var settings = _settingsService.Current;
if (settings.QboTokenExpiry == default)
{
    _logger.LogWarning("Token expiry not set - re-authorize");
    // Complete OAuth flow
}
```

### Sync Times Out (> 15 seconds)

**Cause:** Network slow or Intuit API degraded
**Fix:**

- Increase timeout in `QuickBooksOAuthOptions.TokenRequestTimeoutSeconds`
- Check Intuit Status: https://status.intuit.com/

### Fallback Accounts Show [FALLBACK] Prefix

**Expected:** This is correct (indicates cache mode)
**To Remove:** Implement cache persistence to load real accounts from previous sync

### "Invalid Realm ID" Error

**Cause:** RealmId not set or mismatched
**Fix:**

```csharp
// RealmId comes from OAuth callback
// Verify in AppSettings.QuickBooksRealmId
var settings = _settingsService.Current;
_logger.LogInformation("Current RealmId: {RealmId}", settings.QuickBooksRealmId ?? "<not set>");
```

---

## Test Execution Log Template

```markdown
### Test Execution: [Date]

**Tester:** [Name]
**Environment:** Sandbox / Production
**Build:** [Version]

| Test Case               | Status  | Notes                            | Duration |
| ----------------------- | ------- | -------------------------------- | -------- |
| Test 1: Sandbox Sync    | ✅ PASS | Synced 14 accounts               | 2.3s     |
| Test 2: Fallback        | ✅ PASS | Displayed 8 fallback accounts    | N/A      |
| Test 3: Token Refresh   | ✅ PASS | Auto-refreshed before expiry     | 1.5s     |
| Test 4: Revocation      | ✅ PASS | Token revoked, re-auth triggered | 3.2s     |
| Test 5: Ribbon Button   | ✅ PASS | Button visible and functional    | N/A      |
| Test 6: Error Messages  | ✅ PASS | All error scenarios handled      | N/A      |
| Test 7: Rate Limiting   | ✅ PASS | 10 req/sec respected             | N/A      |
| Test 8: Automated Tests | ✅ PASS | 6/6 tests passed                 | 12.1s    |

**Overall:** ✅ ALL TESTS PASSED

**Issues Found:**

- None

**Sign-Off:** [Date]
```

---

## Next Steps

1. **Test Execution:** Run through all test cases in controlled environment
2. **Documentation:** Update if any deviations found
3. **Production:** Deploy to production once all tests pass
4. **Monitoring:** Watch logs for "sync failed" patterns in first week
5. **Refinement:** Add additional tests as new features added

---

**Validation Date:** January 17, 2026
**Status:** 🟢 Ready for Testing
**Maintainer:** Wiley Widget Dev Team
| Test Case | Status | Notes | Duration |
|-----------|--------|-------|----------|
| Test 1: Sandbox Sync | ✅ PASS | Synced 14 accounts | 2.3s |
| Test 2: Fallback | ✅ PASS | Displayed 8 fallback accounts | N/A |
| Test 3: Token Refresh | ✅ PASS | Auto-refreshed before expiry | 1.5s |
| Test 4: Revocation | ✅ PASS | Token revoked, re-auth triggered | 3.2s |
| Test 5: Ribbon Button | ✅ PASS | Button visible and functional | N/A |
| Test 6: Error Messages | ✅ PASS | All error scenarios handled | N/A |
| Test 7: Rate Limiting | ✅ PASS | 10 req/sec respected | N/A |
| Test 8: Automated Tests | ✅ PASS | 6/6 tests passed | 12.1s |

**Overall:** ✅ ALL TESTS PASSED

**Issues Found:**
- None

**Sign-Off:** [Date]
```

---

## Next Steps

1. **Test Execution:** Run through all test cases in controlled environment
2. **Documentation:** Update if any deviations found
3. **Production:** Deploy to production once all tests pass
4. **Monitoring:** Watch logs for "sync failed" patterns in first week
5. **Refinement:** Add additional tests as new features added

---

**Validation Date:** January 17, 2026
**Status:** 🟢 Ready for Testing
**Maintainer:** Wiley Widget Dev Team
