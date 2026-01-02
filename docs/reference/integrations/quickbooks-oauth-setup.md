# QuickBooks Online OAuth 2.0 Setup (Wiley Widget)

This guide walks you through configuring QuickBooks Online (QBO) OAuth for Wiley Widget using Intuit’s official flow. It includes exact portal steps and local machine prerequisites.

## What you’ll set up

- An Intuit Developer App (QuickBooks Online)
- OAuth redirect URI pointing to your local machine
- Scopes for accounting data
- Client credentials stored in Wiley Widget’s Secret Vault

## Before you start

- Intuit Developer account: <https://developer.intuit.com/>
- Windows admin access (needed once to create a URL ACL for the local OAuth callback)

## 1) Create or configure your Intuit app

1. Open: <https://developer.intuit.com/> and sign in
2. Top-right: Dashboard
3. Left menu: My Apps
4. Create an app → Choose “QuickBooks Online and Payments” (or open your existing app)
5. App settings → Keys & OAuth
6. Note your “Client ID” and “Client Secret”
7. In “Redirect URIs”, add EXACTLY this value (case sensitive):
   - <http://localhost:8080/callback>
8. Save changes

Notes

- Sandbox vs Production is controlled by the environment you choose inside Wiley Widget. The same redirect works for both.
- Scopes are requested by Wiley Widget at runtime. We request: com.intuit.quickbooks.accounting

## 2) One-time Windows URL ACL for local callback

Wiley Widget listens locally to capture the OAuth redirect. Windows requires an ACL for <http://localhost:8080/>.

- Open an elevated PowerShell (Run as Administrator)
- Execute:

```powershell
netsh http add urlacl url=http://localhost:8080/ user=%USERNAME%
```

If you ever change the port, run the same command with the new URL.

## 3) Enter credentials in Wiley Widget

1. Launch Wiley Widget
2. Go to: Settings → QuickBooks Integration
3. Fill in:
   - Client ID: from Intuit portal (Keys & OAuth)
   - Client Secret: from Intuit portal
   - Redirect URI: <http://localhost:8080/callback>
   - Environment: Sandbox first (recommended)
4. Click “Check URL ACL” → Should report Ready (green). If not, re-run the netsh command above as admin.
5. Click “Connect to QuickBooks”
6. Your default browser opens → Sign in → Select company → Authorize
7. On success, the app captures tokens and your realmId (company ID). Status should switch to Connected.

## 4) Validation

- Click “Test QuickBooks Connection”.
- Expected: Status shows Connected. The app performs a tiny read (e.g., customers) to verify API access.

## 5) Token management

- Access tokens expire quickly (~60 minutes). Wiley Widget refreshes them automatically using the refresh token.
- Tokens are stored via the app’s Secret Vault service; keep your machine user profile secure.

## Troubleshooting

- The browser returned but Wiley Widget still says “Connection Failed”
  - Ensure Redirect URI matches exactly in the Intuit portal and in Settings
  - Confirm URL ACL is configured and no firewall is blocking localhost
- “QuickBooks company (realmId) is not set”
  - Re-run Connect to QuickBooks and complete the authorization; the app captures realmId from the callback
- HTTP 400/401 during token exchange or refresh
  - Re-check Client ID/Secret and redirect URI
  - If you rotated keys in Intuit, update them in Settings and reconnect
- Multiple Intuit apps
  - Verify you copied credentials from the app you actually authorized in the browser

Links (official docs)

- OAuth 2.0 overview: <https://developer.intuit.com/app/developer/qbo/docs/develop/authentication-and-authorization/oauth-2.0>
- Scopes: <https://developer.intuit.com/app/developer/qbo/docs/develop/authentication-and-authorization/scopes>
- Keys & OAuth: <https://developer.intuit.com/app/developer/qbo/docs/get-started/authentication-keys>
- Sandboxes: <https://developer.intuit.com/app/developer/qbo/docs/develop/sandboxes>
