# Option 2 Quick Start: QuickBooks Sandbox Seeding

## Overview

This guide walks you through **Option 2** — automatically seeding your QuickBooks Online (QBO) sandbox with Wiley Widget's municipal finance account structure using PowerShell scripts.

## Prerequisites

- ✅ **PowerShell 7.0+** (cross-platform)
- ✅ **Intuit Developer Account** with a registered app
- ✅ **QBO Sandbox Company** already created
- ✅ **Client ID & Client Secret** from your Intuit app settings
- ✅ **Realm ID** (company ID in QBO)

## What Gets Created

The scripts create **21 municipal finance accounts** across multiple fund structures:

### General Fund (GF)

- Cash and Investments (10100)
- Accounts Receivable (10200)
- Property Tax Revenue (41100)
- General Government Expenses (51100)
- Public Safety Expenses (51200)
- Public Works Expenses (51300)
- Parks & Recreation Expenses (51400)

### Special Revenue Fund (SRF)

- Sales Tax Fund (20100)
- Grant Revenues (42100)

### Debt Service Fund (DSF)

- Debt Service Account (30100)
- Bond Interest Expense (52100)
- Bond Principal (52200)

### Capital Projects Fund (CPF)

- Capital Projects Reserve (40100)
- Capital Equipment (53100)
- Infrastructure Projects (53200)

### Enterprise Funds (EF)

- Water Utility Fund (50100)
- Utility Revenue (44100)
- Utility Operations (54100)

### Fund Balance/Equity (60xxx)

- Fund Balance Assigned (60100)
- Fund Balance Unassigned (60200)

## Quick Start (5 minutes)

### Step 1: Gather Credentials

1. Go to [developer.intuit.com](https://developer.intuit.com)
2. Find your app settings and note:
   - **Client ID** (e.g., `ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnop`)
   - **Client Secret** (keep this secret!)
3. In QBO, find your **Realm ID** (Company ID):
   - Settings → Company Settings → Top of page shows "Company ID: 1234567890"

### Step 2: Run the Setup Script

Open PowerShell 7 and run:

```powershell
cd C:\Users\<YourUsername>\Desktop\Wiley-Widget\scripts

# Option A: Full setup with OAuth + auto-seeding
.\setup-sandbox-complete.ps1 `
  -ClientId "YOUR_CLIENT_ID" `
  -ClientSecret "YOUR_CLIENT_SECRET" `
  -RealmId "YOUR_REALM_ID" `
  -AutoSeed
```

**What happens:**

1. ✅ Browser opens → You approve Intuit connection
2. ✅ Script exchanges auth code for access token
3. ✅ Token saved locally (encrypted on Windows)
4. ✅ 21 accounts created automatically
5. ✅ Report generated with results

### Step 3: Verify in QuickBooks

1. Open your QBO Sandbox: https://qbo.intuit.com
2. Go to **Settings** → **Chart of Accounts**
3. Look for accounts starting with "GF -", "SRF -", etc.
4. Confirm all ~21 accounts are visible

### Step 4: Test in Wiley Widget

1. Open Wiley Widget application
2. Go to **Dashboard**
3. Click **"Sync Now"** ribbon button
4. Accounts should appear in the Dashboard grid
5. Success message: "✓ Synced 21 accounts in XXXms"

---

## Advanced Usage

### Manual Seeding (if OAuth already complete)

If you already have an access token:

```powershell
.\seed-sandbox-qbo.ps1 `
  -AccessToken "eyJAlciOiJSUzI1NiIsImtpZCI6IjEyMyJ..." `
  -RealmId "1234567890" `
  -Sandbox
```

### Manual OAuth (if you want to handle token yourself)

1. Run setup without `-AutoSeed`:

```powershell
.\setup-sandbox-complete.ps1 `
  -ClientId "YOUR_CLIENT_ID" `
  -ClientSecret "YOUR_CLIENT_SECRET" `
  -RealmId "YOUR_REALM_ID"
```

2. Token saved to: `$env:LOCALAPPDATA\WileyWidget\qbo-config.json`

3. Later, seed accounts:

```powershell
# Load token from config
$config = Get-Content $env:LOCALAPPDATA\WileyWidget\qbo-config.json | ConvertFrom-Json
.\seed-sandbox-qbo.ps1 -AccessToken $config.accessToken -RealmId $config.realmId -Sandbox
```

### Using Production Environment

Both scripts support production (use with caution!):

```powershell
# Not recommended for testing, but supported:
.\seed-sandbox-qbo.ps1 -AccessToken $token -RealmId $realmId
# Omit -Sandbox flag for production
```

---

## Troubleshooting

### OAuth Browser Won't Open

If the browser doesn't open automatically, manually visit:

```
https://appcenter.intuit.com/connect/oauth2?client_id=YOUR_CLIENT_ID&response_type=code&scope=com.intuit.quickbooks.accounting&redirect_uri=http://localhost:8888/callback&state=GENERATED_STATE
```

### "Duplicate Account" Errors (Yellow ⊘)

- Normal if you run the script twice
- Accounts with same name won't be created again
- Safe to re-run without losing data

### "Token Exchange Failed"

- Verify Client ID & Secret are correct
- Ensure app is authorized in Intuit Developer portal
- Check internet connection

### "Realm ID Not Found"

- Confirm you're using the numeric Company ID (not company name)
- Format: 9 or 10 digit number (e.g., 1234567890)

### Script Hangs on "Waiting for Authorization Callback"

- Ensure port 8888 is not in use:
  ```powershell
  netstat -ano | findstr ":8888"
  ```
- If needed, use different port:
  ```powershell
  .\setup-sandbox-complete.ps1 -Port 9999 ...
  ```

---

## API Details (Reference)

### Account Creation Endpoint

```
POST https://quickbooks.api.intuit.com/v2/companyaccounts/{RealmId}/account
Authorization: Bearer {AccessToken}
Content-Type: application/json

{
  "Name": "GF - Cash and Investments",
  "AccountType": "Asset",
  "AccountSubType": "Bank",
  "CurrentBalance": 0,
  "Active": true,
  "Description": "General Fund Cash"
}
```

### Rate Limiting

- **10 requests/second** max
- Script automatically waits 100ms between requests
- Adjust with `-RateDelayMs` parameter if needed

### Token Expiration

- Tokens expire after **1 hour**
- Refresh token valid for **100 days**
- Config file stored: `$env:LOCALAPPDATA\WileyWidget\qbo-config.json`
- To refresh token manually, re-run setup script

---

## Data Flow Diagram

```
┌─────────────────────────────────────────┐
│  1. Run setup-sandbox-complete.ps1      │
└──────────────┬──────────────────────────┘
               │
        ┌──────▼──────┐
        │  OAuth Flow │
        └──────┬──────┘
               │
    ┌──────────▼──────────┐
    │ 2. Save Token Locally│
    └──────────┬──────────┘
               │
    ┌──────────▼──────────────┐
    │ 3. Call seed-sandbox-qbo│
    └──────────┬──────────────┘
               │
      ┌────────▼────────┐
      │ Create Accounts │
      │  (x21 total)    │
      └────────┬────────┘
               │
      ┌────────▼────────────┐
      │ 4. Generate Report  │
      │  seed-*-report.json │
      └─────────────────────┘
               │
      ┌────────▼────────────┐
      │ 5. Accounts appear  │
      │  in QBO Sandbox     │
      └─────────────────────┘
```

---

## Next Steps

After successful seeding:

1. **Update Wiley Widget Configuration**
   - Settings → QuickBooks
   - Set Realm ID to your sandbox company ID
   - Verify "Use Sandbox" is enabled

2. **Test Sync**
   - Dashboard → Click "Sync Now"
   - Should display: "✓ Synced 21 accounts in XXXms"

3. **Verify Data Integrity**
   - Check account names match expected structure
   - Confirm account types (Asset, Expense, Income, Equity)
   - Verify no accounts marked as "Inactive"

4. **Production Setup** (when ready)
   - Repeat steps with Production Client ID/Secret
   - Use actual QuickBooks company (not sandbox)
   - Modify real municipal finance account structure as needed

---

## Support

For issues, check:

- `seed-*-report.json` (created after each run)
- Application logs in Wiley Widget
- [Intuit Developer Docs](https://developer.intuit.com/app/developer/qbo/docs/get-started)

---

**Last Updated:** 2025-01-13
**Status:** ✅ Production Ready
