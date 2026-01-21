# Phase 1: QuickBooks OAuth Token & Basic Account Sync

**Status**: Infrastructure Complete ✅
**Date**: 2026-01-17
**Goal**: Get OAuth token working + pull account list/balances (proof-of-concept)

## What Was Implemented

### 1. Configuration Models ✅

- **`QuickBooksOAuthOptions`** - Centralized OAuth configuration (client ID, secret, endpoints, scopes)
- **`QuickBooksOAuthToken`** - Token model with expiry detection and refresh tracking
- **`QuickBooksTokenResponse`** - Intuit API response parsing
- **`QuickBooksCompanyInfo`** - Company/realm metadata
- **`TokenResult`** - Result wrapper for token operations

### 2. Token Persistence ✅

- **`QuickBooksTokenStore`** - In-memory cache + disk persistence
  - Automatic expiry detection with 5-minute buffer
  - Encrypted token storage (TODO: implement encryption layer)
  - Disk cache location: `./cache/qb-tokens.json`

### 3. Configuration Updates ✅

- **`appsettings.json`** enhanced with OAuth section:
  ```json
  "QuickBooks": {
    "OAuth": {
      "ClientId": "YOUR_CLIENT_ID",
      "ClientSecret": "YOUR_CLIENT_SECRET",
      "RedirectUri": "http://localhost:5000/callback",
      "Environment": "sandbox"
    }
  }
  ```

## Next Steps (Phase 1 Continued)

### 2–4 hours remaining:

1. **Create OAuth Authorization URL Generator**
   - Build authorization URL with scopes and state parameter
   - Implement PKCE support (code challenge/verifier)
   - File: `src/WileyWidget.Services/QuickBooksOAuthService.cs`

2. **Implement OAuth Callback Handler**
   - Create HTTP endpoint for OAuth redirect (port 5000)
   - Parse authorization code from callback
   - Exchange code for tokens
   - Store token in `QuickBooksTokenStore`
   - File: `src/WileyWidget.WinForms/Services/OAuthCallbackService.cs` (or similar)

3. **Implement Token Refresh Pipeline**
   - Detect token expiry before use
   - Automatically refresh expired tokens
   - Exponential backoff on token refresh failures
   - Already started in `QuickBooksAuthService`

4. **Add Account/Company Info Retrieval**
   - After OAuth, query Intuit for company information
   - Extract Realm ID (Company ID)
   - Store connection metadata
   - File: Extend `QuickBooksService.GetCompanyInfoAsync()`

5. **Create Connection UI Panel**
   - Button to "Connect to QuickBooks"
   - Display: Company name, realm ID, connection status
   - Button to "Disconnect"
   - File: `src/WileyWidget.WinForms/Controls/QuickBooksConnectionPanel.cs`

6. **Implement Basic Account Sync**
   - Pull chart of accounts from QB
   - Retrieve account balances
   - Display in UI (list view or grid)
   - File: Extend `QuickBooksService` with balance retrieval

7. **Add Tests**
   - Unit tests for token refresh
   - Mock OAuth responses
   - Test expiry detection

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│ WileyWidget.WinForms (UI Layer)                         │
├─────────────────────────────────────────────────────────┤
│ • QuickBooksViewModel (MVVM)                            │
│ • QuickBooksConnectionPanel (UI Control)                │
│ • OAuthCallbackService (HTTP Listener)                  │
└───────────────┬─────────────────────────────────────────┘
                │
┌───────────────▼─────────────────────────────────────────┐
│ WileyWidget.Services (Business Logic)                   │
├─────────────────────────────────────────────────────────┤
│ • QuickBooksService (IQuickBooksService)                │
│ • QuickBooksAuthService (Token management)              │
│ • QuickBooksTokenStore (Token persistence)              │
│ • QuickBooksOAuthService (Auth URL generation)          │
│ • QuickBooksApiClient (API calls)                       │
└───────────────┬─────────────────────────────────────────┘
                │
┌───────────────▼─────────────────────────────────────────┐
│ WileyWidget.Services.Configuration                      │
├─────────────────────────────────────────────────────────┤
│ • QuickBooksOAuthOptions                                │
│ • QuickBooksOAuthToken                                  │
│ • QuickBooksTokenResponse                               │
│ • QuickBooksCompanyInfo                                 │
│ • TokenResult                                           │
└─────────────────────────────────────────────────────────┘
                │
┌───────────────▼─────────────────────────────────────────┐
│ External: Intuit OAuth 2.0 API                          │
├─────────────────────────────────────────────────────────┤
│ • https://appcenter.intuit.com/connect/oauth2           │
│ • https://oauth.platform.intuit.com/oauth2/v1/tokens    │
└─────────────────────────────────────────────────────────┘
```

## Intuit API References

- **OAuth 2.0 Spec**: https://developer.intuit.com/app/developer/qbo/docs/auth/oauth2
- **App Center**: https://developer.intuit.com/app/developer/myapps
- **Realm ID (Company ID)**: https://developer.intuit.com/docs/api/accounting?e=0_0_0_103&cp=0_0_0_103#/doc/api/accounting-api/obj/%23Entity/CompanyInfo
- **Account Lists**: https://developer.intuit.com/docs/api/accounting?e=0_0_0_103&cp=0_0_0_103#/doc/api/accounting-api/obj/%23Entity/Account

## Configuration Notes

**Local Development (Sandbox)**:

```bash
# Set via user-secrets:
dotnet user-secrets set "Services:QuickBooks:OAuth:ClientId" "ABCDEFg..."
dotnet user-secrets set "Services:QuickBooks:OAuth:ClientSecret" "XYZ..."

# Or environment variables:
export QUICKBOOKS_CLIENT_ID="ABCDEFg..."
export QUICKBOOKS_CLIENT_SECRET="XYZ..."
```

**Important**: Never commit real OAuth credentials to repo. Use user-secrets for development, KeyVault for production.

## Testing Checklist

- [ ] `QuickBooksOAuthOptions` loads from appsettings correctly
- [ ] `QuickBooksTokenStore` persists token to disk
- [ ] Token expiry detection works (refresh on 5-min buffer)
- [ ] OAuth authorization URL generated with all required params
- [ ] Callback endpoint receives authorization code
- [ ] Token exchange completes successfully
- [ ] Company/Realm ID extracted and stored
- [ ] Account list retrieval works
- [ ] Account balances are accurate
- [ ] Token refresh happens automatically when needed

## Known Issues / TODO

1. **Token Encryption** - Currently tokens are saved as plain JSON. Implement encryption using `ISecretVaultService` before production.
2. **PKCE Support** - Add PKCE code challenge/verifier for enhanced security.
3. **Error Handling** - Add comprehensive error messages and retry logic for OAuth failures.
4. **UI Polish** - Add loading spinners, error toasts, success confirmations.
5. **Realm ID Caching** - Store realm ID separately so we don't need to fetch it every time.

## Time Estimate

- Infrastructure (done): 1.5 hours ✅
- Authorization URL + Callback: 1 hour
- Account/Balance Sync: 1 hour
- UI & Testing: 1–2 hours
- **Total Phase 1**: 4–5 hours
- Infrastructure (done): 1.5 hours ✅
- Authorization URL + Callback: 1 hour
- Account/Balance Sync: 1 hour
- UI & Testing: 1–2 hours
- **Total Phase 1**: 4–5 hours
