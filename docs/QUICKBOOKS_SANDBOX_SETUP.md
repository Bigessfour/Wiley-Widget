# QuickBooks Sandbox Setup Guide for Wiley Widget

**Date:** January 17, 2026
**Status:** Configuration Documentation

---

## How QuickBooks Works in Wiley Widget

The implementation **works TWO WAYS**:

### 1. ✅ Sandbox Mode (Development/Testing)

- **API Endpoint:** `https://sandbox-quickbooks.api.intuit.com/`
- **Environment Flag:** `"sandbox"` (default)
- **Use Case:** Testing OAuth flow, account sync, without affecting real data
- **Data:** Test company provided by Intuit

### 2. ✅ Production Mode (Live Data)

- **API Endpoint:** `https://quickbooks.api.intuit.com/`
- **Environment Flag:** `"production"`
- **Use Case:** Real QuickBooks Online companies
- **Data:** User's actual QuickBooks data

**Code Implementation:**

```csharp
// QuickBooksApiClient.cs line 41
ctx.IppConfiguration.BaseUrl.Qbo = s.QuickBooksEnvironment == "sandbox"
    ? "https://sandbox-quickbooks.api.intuit.com/"
    : "https://quickbooks.api.intuit.com/";
```

---

## The Problem: Sandbox Not Matching Wiley Widget

### Issue: Default Sandbox Company Structure ≠ Municipal Finance

**What Intuit Provides (Default Sandbox):**

- Generic small business chart of accounts
- Sample customers, invoices, vendors
- Generic account types (Operating Account, Checking, etc.)
- **No** municipal finance accounts (utilities, departments, bonds, etc.)

**What Wiley Widget Needs:**

- Municipal account hierarchy (General Fund, Special Revenue, Debt Service, etc.)
- Public sector account types (Government structures)
- Department-based allocation
- Compliance with GASB accounting standards

### Example Mismatch:

| Intuit Sandbox Provides    | Wiley Widget Needs                                 |
| -------------------------- | -------------------------------------------------- |
| "Operating Account" (Bank) | "General Fund - Checking Account"                  |
| "Accounts Payable"         | "General Fund - Accounts Payable"                  |
| "Sales Revenue"            | "Property Tax Revenue", "License & Permit Revenue" |
| No budget tracking         | Budget vs. Actual comparison                       |

---

## Solutions for Sandbox Setup

### Option 1: Manual Sandbox Configuration (Recommended for Development)

**Steps:**

1. **Log into Intuit Sandbox** at https://developer.intuit.com/app/developer/myapps
   - Use sandbox admin credentials
   - Navigate to your test company

2. **Create Municipal-Style Accounts:**

   ```
   1000 - General Fund Cash
   1100 - General Fund Receivables
   1500 - Capital Equipment
   2000 - General Fund Payables
   3000 - Unrestricted Fund Balance
   4000 - Property Tax Revenue
   4100 - License & Permit Revenue
   5000 - Personnel Expenses
   5100 - Department: Police
   5200 - Department: Fire
   5300 - Department: Public Works
   6000 - Supplies & Materials
   ```

3. **Create Sample Data:**
   - Add 2-3 sample customers (for billing)
   - Add 2-3 sample vendors (for expenses)
   - Create 5-10 journal entries with department allocations

4. **Verify in Wiley Widget:**
   - Click "Sync Now" in Dashboard
   - Should see your custom accounts in grid
   - Verify account hierarchy displays correctly

**Duration:** ~15 minutes manual setup

### Option 2: Use Intuit's REST API to Seed Data

**Setup Script Example:**

```powershell
# scripts/seed-sandbox-qbo.ps1
$clientId = $env:QBO_CLIENT_ID
$clientSecret = $env:QBO_CLIENT_SECRET
$accessToken = "<obtained-from-oauth>"
$realmId = "<from-oauth-callback>"

$municipalAccounts = @(
    @{ Name = "General Fund Checking"; AcctNum = "1000"; Type = "Bank" },
    @{ Name = "Property Tax Revenue"; AcctNum = "4000"; Type = "Income" },
    @{ Name = "Police Department Expense"; AcctNum = "5100"; Type = "Expense" }
    # ... more accounts
)

# POST to https://quickbooks.api.intuit.com/v2/company/{realmId}/account
# Create each account via REST API
```

**Duration:** ~20 minutes (including API learning curve)

### Option 3: Use Wiley Widget's Fallback Accounts (Quick Testing)

**Current Implementation:**

```csharp
// QuickBooksService.GetFallbackAccounts()
// Returns 8 sample accounts suitable for testing:
// - [FALLBACK] Operating Account (Bank)
// - [FALLBACK] Equipment (Asset)
// - [FALLBACK] Accounts Payable (Liability)
// - [FALLBACK] Revenue (Income)
// - [FALLBACK] Salaries Expense
// - [FALLBACK] Utilities Expense
// - [FALLBACK] Depreciation Expense
// - [FALLBACK] Retained Earnings (Equity)
```

**How to Use:**

1. Intentionally fail sandbox sync (disconnect network or use invalid token)
2. Dashboard automatically displays 8 fallback accounts
3. Test UI/Grid functionality without actual QBO data
4. When sandbox is ready, toggle `QuickBooksEnvironment = "sandbox"` in settings

**Duration:** Immediate (0 minutes)

---

## Configuration for Different Environments

### For Development (Local Machine)

```json
{
  "Services": {
    "QuickBooks": {
      "OAuth": {
        "ClientId": "from-intuit-dev-portal",
        "ClientSecret": "from-intuit-dev-portal",
        "RedirectUri": "http://localhost:8080/callback",
        "Environment": "sandbox", // ← DEV SETTING
        "EnableTokenPersistence": true,
        "TokenCachePath": "%APPDATA%\\WileyWidget\\tokens.json"
      }
    }
  }
}
```

**Environment Variables:**

```powershell
$env:QBO_ENVIRONMENT = "sandbox"
$env:QBO_CLIENT_ID = "your-dev-client-id"
$env:QBO_CLIENT_SECRET = "your-dev-client-secret"
$env:QBO_REALM_ID = "1234567890"  # From first OAuth flow
```

### For Production (Staging/Live)

```json
{
  "Services": {
    "QuickBooks": {
      "OAuth": {
        "ClientId": "from-intuit-prod-portal",
        "ClientSecret": "from-intuit-prod-portal",
        "RedirectUri": "https://your-domain.com/callback",
        "Environment": "production", // ← PROD SETTING
        "EnableTokenPersistence": true,
        "TokenCachePath": "C:\\secure\\vault\\tokens.json" // Encrypted
      }
    }
  }
}
```

**Environment Variables:**

```powershell
$env:QBO_ENVIRONMENT = "production"
$env:QBO_CLIENT_ID = "your-prod-client-id"
$env:QBO_CLIENT_SECRET = "your-prod-client-secret"
$env:QBO_REALM_ID = "production-realm-id"
```

---

## Troubleshooting Sandbox Issues

### Issue 1: Sandbox Accounts Not Showing

**Check:**

- [ ] RealmId correctly set (from OAuth callback)
- [ ] Token is valid (not expired, not revoked)
- [ ] Environment = "sandbox" in settings
- [ ] Internet connection active
- [ ] Intuit API status: https://status.intuit.com/

**Fix:**

```powershell
# Clear cached data and re-authorize
Remove-Item "$env:APPDATA\WileyWidget\tokens.json"
# Run "Sync Now" in Dashboard - will trigger fresh OAuth
```

### Issue 2: Wrong Accounts Appearing

**Possible Causes:**

- Syncing against production (not sandbox)
- RealmId points to different company
- Account cache is stale

**Fix:**

```csharp
// Verify environment in code
var settings = _settingsService.Current;
_logger.LogInformation("Using environment: {Env}, RealmId: {RealmId}",
    settings.QuickBooksEnvironment,  // Should be "sandbox"
    settings.QuickBooksRealmId);     // Should be sandbox realmId
```

### Issue 3: "Sync Failed - Using Fallback Accounts"

**This is Normal For:**

- Network connectivity issues
- Token expired (auto-refresh didn't work)
- Intuit API temporary downtime
- Sandbox reset (happens every 30 days)

**Fix:**

- Check error message in Dashboard
- If "Invalid Token": Revoke and re-authorize
- If "Network Error": Check internet connection
- If "Sandbox Reset": Re-authenticate (Intuit automatically resets test data)

---

## Switching Between Sandbox & Production

### Runtime Toggle (Advanced)

In `MainViewModel.cs`:

```csharp
// Add a method to switch environments
public async Task SwitchEnvironmentAsync(string environment)
{
    if (environment != "sandbox" && environment != "production")
        throw new ArgumentException("Must be 'sandbox' or 'production'");

    var settings = _settingsService.Current;
    settings.QuickBooksEnvironment = environment;
    _settings.Save();

    _logger.LogInformation("Switched QuickBooks environment to {Environment}", environment);

    // Force token refresh in case RealmId differs
    await _quickBooksService.RefreshTokenIfNeededAsync();

    // Refresh Dashboard to show new data
    await RefreshDataAsync();
}
```

### Configuration File Toggle

Edit `appsettings.json`:

```json
{
  "Services": {
    "QuickBooks": {
      "OAuth": {
        "Environment": "sandbox" // Change this line
      }
    }
  }
}
```

Then restart application.

---

## Data Synchronization Strategy

### For Development Testing (Sandbox)

1. **Week 1:** Create basic account structure manually (Option 1)
2. **Week 2:** Test sync with small dataset (8-10 accounts)
3. **Week 3:** Add sample transactions and verify filtering
4. **Week 4:** Test fallback handling (intentionally break sync)

### For Production (Real Companies)

1. **Initial Setup:** User connects their QuickBooks Online company via OAuth
2. **First Sync:** Pulls real company chart of accounts
3. **Ongoing:** Automatic sync on Dashboard load, or manual "Sync Now"
4. **Fallback:** If sync fails, shows last cached data (future enhancement)

---

## Summary: Two-Way Configuration

| Aspect              | Sandbox Mode                      | Production Mode                   |
| ------------------- | --------------------------------- | --------------------------------- |
| **API Endpoint**    | sandbox-quickbooks.api.intuit.com | quickbooks.api.intuit.com         |
| **Setting**         | `Environment: "sandbox"`          | `Environment: "production"`       |
| **Data**            | Test company (Intuit-provided)    | Real user company                 |
| **RealmId**         | Test realmId from OAuth           | Production realmId from OAuth     |
| **Use Case**        | Development, CI/CD testing        | Live municipal finance operations |
| **Reset Frequency** | Every 30 days (automatic)         | Never (user's real data)          |
| **OAuth App**       | Dev app from dev portal           | Prod app from dev portal          |
| **Risk**            | None (test data only)             | High (affects real accounts)      |

---

## Next Steps

**To Fix Sandbox Mismatch:**

1. **Short-term:** Use Fallback Accounts (Option 3) for UI testing
2. **Medium-term:** Manually configure sandbox with municipal accounts (Option 1)
3. **Long-term:** Build Account Seeding tool (Option 2) for automated setup

**Current Status:**

- ✅ Code supports both sandbox and production
- ✅ Fallback accounts available
- ⚠️ Sandbox account structure generic (needs manual configuration)
- 🔄 Recommend: Set up Option 1 (manual account creation) for realistic testing

---

**Maintainer:** Wiley Widget Dev Team
**Last Updated:** January 17, 2026
