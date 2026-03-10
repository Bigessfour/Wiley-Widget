# QuickBooks Sandbox Seeding - Option 2 Quick Start

## Important

Use the current Wiley Widget sandbox localhost callback flow, not the Intuit OAuth Playground redirect.

Before running any seed step:

- run `scripts/quickbooks/prepare-sandbox-seed.ps1`
- connect to sandbox in Wiley Widget so diagnostics show `Redirect OK : YES`
- export the active session into the shell with `scripts/quickbooks/export-qbo-session.ps1`

Treat the rest of this document as legacy background until it is fully refreshed.

## Overview

**Option 2: API Seeding** uses PowerShell scripts to automatically populate your QuickBooks sandbox with municipal finance accounts via the REST API.

**Time Required:** ~20 minutes (including OAuth flow)
**Complexity:** Medium (requires command-line PowerShell)
**Benefit:** Fully automated, repeatable, documented

---

## Prerequisites

1. ✅ Intuit Developer Account
2. ✅ QuickBooks test app created at https://developer.intuit.com/app/developer/myapps
3. ✅ Client ID and Client Secret from app settings
4. ✅ PowerShell 7+ installed
5. ✅ Internet connection

---

## Step 1: Prepare Credentials

Get your app credentials from Intuit Developer Portal:

1. Log into https://developer.intuit.com
2. Go to **My Apps** → select your app
3. Copy these values:
   - **Client ID** (e.g., `ABCDEFGHIJKLMNOPQRSTUVWxyz`)
   - **Client Secret** (e.g., `1234567890abcdefghijklmnop`)

---

## Step 2: Run the Setup Script

### Option A: Interactive Setup (Recommended)

```powershell
cd "C:\Users\biges\Desktop\Wiley-Widget\scripts\quickbooks"

.\setup-sandbox-complete.ps1 `
    -ClientId "YOUR_CLIENT_ID" `
    -ClientSecret "YOUR_CLIENT_SECRET" `
    -RealmId "1234567890"
```

**What it does:**

1. ✓ Verifies your credentials
2. ✓ Opens browser for OAuth authorization
3. ✓ Exchanges code for access token
4. ✓ Creates 21 municipal accounts in sandbox
5. ✓ Saves token for future use
6. ✓ Displays results summary

**Example Output:**

```
================================================================
Wiley Widget - QuickBooks Sandbox Setup (Option 2: API Seeding)
================================================================

✓ ClientId provided: ABCDEFGHIJKLMNOPQRSTUVWxyz...
✓ ClientSecret provided: ****
✓ RealmId provided: 1234567890

STEP 2: Obtain OAuth Access Token

ℹ Access token needed. Starting OAuth flow...

Browser opened. After authorization, paste the authorization code below.
Enter authorization code from redirect URL: < user pastes code here >

✓ Access token obtained
ℹ Token expires in 3600 seconds

STEP 3: Seed Sandbox with Municipal Accounts

🌱 Seeding QuickBooks sandbox Sandbox for Wiley Widget
   RealmId: 1234567890
   Base URL: https://sandbox-quickbooks.api.intuit.com

📋 Creating 21 accounts...

  ➤ Creating: 1000 - General Fund Checking
    ✓ Success (ID: 1)
  ➤ Creating: 1100 - General Fund Savings
    ✓ Success (ID: 2)
  ... (19 more accounts)

📊 Seeding Complete
=========================================================
✓ Created: 21 accounts
✗ Failed:  0 accounts
Total:    21 accounts

Created Accounts Summary:

AcctNum Name                        Type
------- ----                        ----
1000    General Fund Checking       Bank
1100    General Fund Savings        Bank
1500    Equipment & Vehicles        FixedAsset
... (18 more rows)

🎯 Next Steps:
  1. Verify accounts in QuickBooks Sandbox:
     https://app.sandbox.qbo.intuit.com → Settings → Chart of Accounts
  2. Run Wiley Widget and sync accounts:
     Dashboard → 'Sync Now' button
  3. Verify in Wiley Widget Dashboard grid:
     All 21 accounts should appear
```

---

## Step 3: Verify in QuickBooks

1. Log into QuickBooks Sandbox: https://app.sandbox.qbo.intuit.com
2. Click **Settings** (⚙️ icon, bottom left)
3. Select **Chart of Accounts**
4. Verify all 21 accounts appear:
   - ✓ General Fund Checking (1000)
   - ✓ General Fund Savings (1100)
   - ✓ Equipment & Vehicles (1500)
   - ... and 18 more

---

## Step 4: Test in Wiley Widget

1. **Open Wiley Widget** application
2. **Navigate to Dashboard** tab
3. **Click "Sync Now"** button in ribbon
4. **Verify accounts appear** in grid:
   - Should show all 21 accounts
   - Account names, numbers, and types visible
   - No errors in status bar

**Expected Result:**

```
✓ Synced 21 accounts in 2345ms
```

---

## Accounts Created

### Assets (1000-1500)

| Account # | Name                     | Type                |
| --------- | ------------------------ | ------------------- |
| 1000      | General Fund Checking    | Bank                |
| 1100      | General Fund Savings     | Bank                |
| 1500      | Equipment & Vehicles     | Fixed Asset         |
| 1550      | Accumulated Depreciation | Other Current Asset |

### Liabilities (2000-2100)

| Account # | Name             | Type                    |
| --------- | ---------------- | ----------------------- |
| 2000      | Accounts Payable | Accounts Payable        |
| 2100      | Accrued Payroll  | Other Current Liability |

### Equity (3000)

| Account # | Name         | Type   |
| --------- | ------------ | ------ |
| 3000      | Fund Balance | Equity |

### Revenue (4000-4300)

| Account # | Name                      | Type   |
| --------- | ------------------------- | ------ |
| 4000      | Property Tax Revenue      | Income |
| 4100      | License & Permit Revenue  | Income |
| 4200      | Intergovernmental Revenue | Income |
| 4300      | Fines & Forfeitures       | Income |

### Expenses (5000-5700)

| Account # | Name                       | Type    |
| --------- | -------------------------- | ------- |
| 5100      | Police Department Salaries | Expense |
| 5110      | Fire Department Salaries   | Expense |
| 5120      | Public Works Salaries      | Expense |
| 5200      | Police Department Supplies | Expense |
| 5210      | Fire Department Supplies   | Expense |
| 5300      | Utilities Expense          | Expense |
| 5400      | Equipment Maintenance      | Expense |
| 5500      | Depreciation Expense       | Expense |
| 5600      | Professional Services      | Expense |
| 5700      | Training & Development     | Expense |

---

## Troubleshooting

### "Access token needed" but browser won't open

**Solution:** Manually copy/paste the OAuth URL from the console:

```
https://appcenter.intuit.com/connect/oauth2?client_id=...&scope=...
```

### "Failed to obtain access token"

**Check:**

- [ ] Client ID is correct (no spaces, full value)
- [ ] Client Secret is correct
- [ ] Authorization code copied exactly
- [ ] Authorization code not expired (< 5 minutes)

**Fix:** Run again and copy authorization code carefully

### "Seeding failed: 401 Unauthorized"

**Cause:** Access token expired
**Fix:** Run script again, it will get fresh token

### "No accounts appear in Wiley Widget after sync"

**Check:**

- [ ] All 21 accounts created in QuickBooks
- [ ] Sync completed without errors
- [ ] Dashboard refreshed (F5)
- [ ] Correct RealmId set in Wiley Widget settings

**Fix:**

1. Manually verify in QuickBooks: https://app.sandbox.qbo.intuit.com/app/settings/qboaccount/sa/accountlist
2. Check logs for detailed error: `C:\Users\<user>\AppData\Local\WileyWidget\logs\`

### "Some accounts failed to create"

**This is normal** - QuickBooks may reject some account types depending on company setup.

**Check output file:**

```powershell
type "$env:APPDATA\WileyWidget\created-accounts.json"
```

Successfully created accounts will still be usable in Wiley Widget.

---

## Advanced: Run Just the Seeding Script

If you already have an access token:

```powershell
.\seed-sandbox-qbo.ps1 `
    -AccessToken "your-oauth-token" `
    -RealmId "1234567890" `
    -ClientId "YOUR_CLIENT_ID" `
    -ClientSecret "YOUR_CLIENT_SECRET" `
    -Environment "sandbox" `
    -OutputFile "C:\accounts.json"
```

---

## Cleanup: Delete Accounts

To start over, manually delete accounts from QuickBooks:

1. Log into https://app.sandbox.qbo.intuit.com
2. Settings → Chart of Accounts
3. Click account → **Deactivate** (or Delete if brand new)

Or wait 30 days for sandbox auto-reset.

---

## Next Steps

✅ **Sandbox seeded with municipal accounts**
➡️ **Run Wiley Widget and test account sync**
➡️ **Add sample transactions (optional)**
➡️ **Test budget vs. actual reports**

---

## Files Reference

| File                                             | Purpose                              |
| ------------------------------------------------ | ------------------------------------ |
| `setup-sandbox-complete.ps1`                     | Main orchestration script (run this) |
| `seed-sandbox-qbo.ps1`                           | Low-level seeding via API            |
| `$env:APPDATA\WileyWidget\oauth-token.json`      | Cached OAuth token (auto-created)    |
| `$env:APPDATA\WileyWidget\created-accounts.json` | List of created accounts             |

---

**Setup Duration:** 15-20 minutes
**Difficulty:** ⭐⭐⭐ (Moderate - command-line)
**Reliability:** ⭐⭐⭐⭐⭐ (Fully automated)

**Questions?** Check `docs/QUICKBOOKS_SANDBOX_SETUP.md` for detailed info.
