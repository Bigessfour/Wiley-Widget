# QuickBooks Desktop App Registration Guide

## For Wiley Widget - WPF Desktop Application

### Registration Form Answers

#### ✅ Step 1: Review your Intuit Developer Portal Profile

- Complete your developer profile
- Verify your email address

#### ✅ Step 2: Add Privacy Policy and End-User License Agreement

**For Sandbox/Development:**

- **Privacy Policy URL**: `https://app.townofwiley.gov/privacy`
- **End-User License Agreement**: `https://app.townofwiley.gov/eula`

**For Production** (create actual pages later):

- Host these on your company website or GitHub Pages
- Example: `https://yourdomain.com/wiley-widget/privacy`

#### ✅ Step 3: Add Host Domain, Launch URL, Disconnect URL, and Redirect URIs

We’re using a desktop loopback redirect and a public domain for launch/disconnect/webhooks.

**Host Domain (Portal field):**

- `app.townofwiley.gov` (no protocol)

**Launch URL (Portal field):**

- `https://app.townofwiley.gov/app/launch`

**Disconnect URL (Portal field):**

- `https://app.townofwiley.gov/app/disconnect`

**Redirect URIs (OAuth callback for desktop loopback):**

- `http://localhost:8080/callback` ✅ Primary
- Optional: `http://127.0.0.1:8080/callback`

**Important**: Check the box for "Desktop App" if available.

#### ✅ Step 4: Select App Category

Choose the most appropriate:

- **Accounting** (recommended for Wiley Widget)
- Or **Business Management**

#### ✅ Step 5: Regulated Industries

Select any that apply to Town of Wiley:

- ☑️ Government/Municipality
- ☐ Financial Services (if applicable)

#### ✅ Step 6: App Hosting

Select:

- ☑️ **On-premise** (Desktop application)
- ☐ Cloud/SaaS (not applicable)

---

## After Registration: Getting Your Credentials

Once registered, you'll get:

1. **Client ID** - Public identifier for your app
2. **Client Secret** - Private key (keep secure!)
3. **Realm ID** - QuickBooks company ID (get this after OAuth)

---

## Configure Wiley Widget with Your Credentials

### Method 1: Using PowerShell Script (Recommended)

```powershell
# Run this in the project directory
.\setup-quickbooks-sandbox.ps1 `
    -ClientId 'YOUR_CLIENT_ID_HERE' `
    -ClientSecret 'YOUR_CLIENT_SECRET_HERE'
```

### Method 2: Manual Environment Variables

```powershell
# Set environment variables
[Environment]::SetEnvironmentVariable('QBO_CLIENT_ID', 'your_client_id', 'User')
[Environment]::SetEnvironmentVariable('QBO_CLIENT_SECRET', 'your_client_secret', 'User')
[Environment]::SetEnvironmentVariable('QBO_REDIRECT_URI', 'http://localhost:8080/callback', 'User')
[Environment]::SetEnvironmentVariable('QBO_ENVIRONMENT', 'sandbox', 'User')

# Verify they're set
[Environment]::GetEnvironmentVariable('QBO_CLIENT_ID', 'User')
```

### Method 3: Using appsettings.Development.json (Less Secure)

Edit `appsettings.Development.json`:

```json
{
  "QuickBooks": {
    "ClientId": "YOUR_CLIENT_ID_HERE",
    "ClientSecret": "YOUR_CLIENT_SECRET_HERE",
    "RedirectUri": "http://localhost:8080/callback",
    "Environment": "sandbox"
  }
}
```

**⚠️ Warning**: Never commit `ClientSecret` to Git!

---

## Testing Your Configuration

### Step 1: Set Environment Variables

```powershell
.\setup-quickbooks-sandbox.ps1 -ClientId 'ABC123...' -ClientSecret 'xyz789...'
```

### Step 2: Validate Connection

```powershell
.\test-quickbooks-connection.ps1
```

### Step 3: Run the Application

```powershell
dotnet run --project WileyWidget.csproj
```

### Step 4: Authenticate

1. Go to Settings in the app
2. Click "Connect to QuickBooks"
3. Log in with your Intuit Developer account
4. Authorize the app

---

## Sandbox Test Company

Intuit provides a sandbox company for testing:

- **Company Name**: "Sandbox Company_US_1"
- Access through: https://developer.intuit.com/app/developer/sandbox

---

## Troubleshooting

### "Invalid Redirect URI" Error

- Ensure `http://localhost:8080/callback` is added to your app's redirect URIs
- Match the exact URL (including https/http and port)

### "Invalid Client" Error

- Check that `QBO_CLIENT_ID` and `QBO_CLIENT_SECRET` are set correctly
- Verify you're using sandbox credentials with sandbox environment

### "Realm ID Required" Error

- You get the Realm ID after first OAuth authorization
- It will be saved automatically in settings

### Environment Variables Not Loading

```powershell
# Restart PowerShell after setting variables
# Or reload them manually:
$env:QBO_CLIENT_ID = [Environment]::GetEnvironmentVariable('QBO_CLIENT_ID', 'User')
```

---

## Production Deployment Checklist

Before going to production:

- [ ] Create actual Privacy Policy page
- [ ] Create actual End-User License Agreement page
- [ ] Update redirect URIs to production URLs
- [ ] Get production QuickBooks credentials (separate from sandbox)
- [ ] Set `QBO_ENVIRONMENT=production`
- [ ] Store secrets in Azure Key Vault (already configured in project)
- [ ] Test OAuth flow with production company
- [ ] Submit app for Intuit review if public distribution

---

## Additional Resources

- **QuickBooks API Docs**: https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/account
- **OAuth Guide**: https://developer.intuit.com/app/developer/qbo/docs/develop/authentication-and-authorization/oauth-2.0
- **Sample Code**: https://github.com/IntuitDeveloper/SampleApp-CRUD-DotNet

---

## Configuration Summary for appsettings.json

Current configuration in your project:

````json
"QuickBooks": {
  "ClientId": "${QBO_CLIENT_ID}",
  "ClientSecret": "${QBO_CLIENT_SECRET}",
  "RedirectUri": "${QBO_REDIRECT_URI}",
  "Environment": "${QBO_ENVIRONMENT}"
}
---

## Webhooks (recommended for sandbox verification)

Enable Webhooks in your Intuit app and set:

- Endpoint URL: `https://app.townofwiley.gov/qbo/webhooks`
- Webhooks Verifier Token: copy the value from Intuit portal and set it locally:

```powershell
[Environment]::SetEnvironmentVariable('QBO_WEBHOOKS_VERIFIER','<verifier-from-intuit>','User')
````

Local service listens at `https://localhost:7207/qbo/webhooks` and is exposed via the Cloudflare Tunnel to the public URL above.

```

These reference environment variables (recommended for security).
```
