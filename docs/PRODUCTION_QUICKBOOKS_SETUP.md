# QuickBooks Production Setup Guide

**Last Updated:** 2026-02-09
**Status:** Production Ready

## Overview

This guide covers the complete production setup for QuickBooks integration including OAuth, Webhooks, and Cloudflare Tunnel configuration.

---

## Architecture

### Components

1. **WinForms Desktop App** - Runs on user's machine
   - OAuth callback listener on `http://localhost:5000/`
   - Initiates OAuth flow
   - Manages tokens locally

2. **Webhooks Server** - Runs on production server
   - ASP.NET Core minimal API
   - Receives webhook notifications from Intuit
   - Validates HMACSHA256 signatures
   - Port: 7207 (HTTPS), 5207 (HTTP)

3. **Cloudflare Tunnel** - Bridges localhost to public internet
   - Named tunnel (persistent)
   - Routes `app.townofwiley.gov` → `localhost:7207`
   - Handles SSL/TLS termination

---

## User Secrets Configuration

### WinForms App (`src/WileyWidget.WinForms`)

```powershell
# Set QuickBooks OAuth credentials
dotnet user-secrets set "Services:QuickBooks:OAuth:ClientId" "ABwfJ8obzq6SVLemLAVurAFCQYRxjoWiGDE4KKU380RCcFNWiD" --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj

dotnet user-secrets set "Services:QuickBooks:OAuth:ClientSecret" "BGiENFpuLDO1vfFPCnQLsA2pwT43VP2UZoioneh7" --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj

# Set webhook verifier token
dotnet user-secrets set "Services:QuickBooks:Webhooks:VerifierToken" "772614ab-07db-4f6a-b7eb-d86985774d71" --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj
```

### Webhooks Server (`src/WileyWidget.Webhooks`)

```powershell
# Set webhook verifier token (MUST match WinForms app)
dotnet user-secrets set "Services:QuickBooks:Webhooks:VerifierToken" "772614ab-07db-4f6a-b7eb-d86985774d71" --project src/WileyWidget.Webhooks/WileyWidget.Webhooks.csproj
```

**Verify secrets:**

```powershell
dotnet user-secrets list --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj
dotnet user-secrets list --project src/WileyWidget.Webhooks/WileyWidget.Webhooks.csproj
```

---

## Environment Configuration

### Development (Sandbox)

**appsettings.json** (both projects):

```json
{
  "Services": {
    "QuickBooks": {
      "OAuth": {
        "Environment": "sandbox",
        "RedirectUri": "http://localhost:5000/"
      }
    }
  }
}
```

### Production

**appsettings.Production.json**:

```json
{
  "Services": {
    "QuickBooks": {
      "OAuth": {
        "Environment": "production",
        "RedirectUri": "http://localhost:5000/"
      },
      "Webhooks": {
        "EndpointUrl": "https://app.townofwiley.gov/qbo/webhooks"
      }
    }
  }
}
```

---

## Cloudflare Tunnel Setup (Production)

### 1. Install Cloudflared

**Windows:**

```powershell
winget install --id Cloudflare.cloudflared
```

**Verify installation:**

```powershell
cloudflared --version
```

### 2. Authenticate with Cloudflare

```powershell
cloudflared tunnel login
```

This opens a browser to authorize the tunnel with your Cloudflare account.

### 3. Create Named Tunnel

```powershell
cloudflared tunnel create wiley-widget
```

**Output:**

```
Tunnel credentials written to: C:\Users\<user>\.cloudflared\<tunnel-id>.json
Created tunnel wiley-widget with id <tunnel-id>
```

**Save the tunnel ID - you'll need it!**

### 4. Configure DNS

```powershell
cloudflared tunnel route dns wiley-widget app.townofwiley.gov
```

Or manually in Cloudflare Dashboard:

1. Go to DNS settings
2. Add CNAME record:
   - **Name:** `app`
   - **Target:** `<tunnel-id>.cfargotunnel.com`
   - **Proxy status:** Proxied (orange cloud)

### 5. Create Tunnel Configuration

**File:** `C:\Users\<user>\.cloudflared\config.yml`

```yaml
tunnel: <tunnel-id>
credentials-file: C:\Users\<user>\.cloudflared\<tunnel-id>.json

ingress:
  # Webhooks server
  - hostname: app.townofwiley.gov
    service: https://localhost:7207
    originRequest:
      noTLSVerify: true # For localhost SSL cert

  # Catch-all rule (required)
  - service: http_status:404
```

### 6. Test Tunnel Locally

```powershell
cloudflared tunnel run wiley-widget
```

**Verify:**

- Open `https://app.townofwiley.gov/health`
- Should return: `{"status":"ok"}`

### 7. Install as Windows Service (Production)

```powershell
cloudflared service install
```

**Start service:**

```powershell
net start cloudflared
```

**Set to auto-start:**

```powershell
sc config cloudflared start=auto
```

---

## Intuit Developer Portal Configuration

### 1. App Settings

**Location:** https://developer.intuit.com/app/developer/qbo/apps

**Keys & OAuth:**

- Client ID: `ABwfJ8obzq6SVLemLAVurAFCQYRxjoWiGDE4KKU380RCcFNWiD`
- Client Secret: `BGiENFpuLDO1vfFPCnQLsA2pwT43VP2UZoioneh7`

**Redirect URIs:**

```
http://localhost:5000/
```

### 2. Webhooks Settings

**Webhooks Endpoint URL:**

```
https://app.townofwiley.gov/qbo/webhooks
```

**Verifier Token:**

```
772614ab-07db-4f6a-b7eb-d86985774d71
```

**Events to Subscribe:**

- [ ] Customer
- [ ] Invoice
- [ ] Payment
- [ ] Account
- [ ] Vendor
- [ ] Bill
- [ ] Purchase
- [x] (Select events your app needs)

### 3. App URLs

**Host Domain:**

```
app.townofwiley.gov
```

**Launch URL:**

```
https://app.townofwiley.gov/app/launch
```

**Disconnect URL:**

```
https://app.townofwiley.gov/app/disconnect
```

**Privacy Policy URL:**

```
https://app.townofwiley.gov/privacy
```

**EULA URL:**

```
https://app.townofwiley.gov/eula
```

---

## Deployment Checklist

### Pre-Production

- [ ] User secrets configured in both projects
- [ ] Cloudflare tunnel created and tested
- [ ] DNS CNAME record configured
- [ ] Webhooks server deployed and running
- [ ] Firewall rules configured (allow port 7207)
- [ ] SSL certificate valid for localhost (dev certificate)

### Intuit Portal

- [ ] App settings configured with production values
- [ ] Redirect URIs added
- [ ] Webhooks endpoint configured
- [ ] Verifier token matches user secrets
- [ ] App URLs configured
- [ ] Privacy policy and EULA pages accessible

### Testing

- [ ] Health check accessible: `https://app.townofwiley.gov/health`
- [ ] OAuth flow completes successfully
- [ ] Webhooks validated with signature
- [ ] Token refresh working
- [ ] Error handling verified

### Production Switch

1. Update `appsettings.json`:

   ```json
   "Environment": "production"
   ```

2. Restart services:

   ```powershell
   net stop cloudflared
   net start cloudflared
   Restart-Service WileyWidgetWebhooks
   ```

3. Test OAuth flow in production mode
4. Verify webhooks receiving events
5. Monitor logs for errors

---

## Maintenance

### View Tunnel Logs

```powershell
Get-Content "C:\Users\<user>\.cloudflared\*.log" -Tail 50 -Wait
```

### Restart Tunnel Service

```powershell
net stop cloudflared
net start cloudflared
```

### Update Tunnel Configuration

1. Edit `C:\Users\<user>\.cloudflared\config.yml`
2. Restart tunnel service
3. Verify with health check

### Rotate Secrets

1. Generate new values in Intuit portal
2. Update user secrets:
   ```powershell
   dotnet user-secrets set "Services:QuickBooks:OAuth:ClientSecret" "NEW_SECRET" --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj
   ```
3. Restart applications
4. Test OAuth flow

---

## Troubleshooting

### Tunnel Not Working

**Check tunnel status:**

```powershell
cloudflared tunnel info wiley-widget
```

**Verify DNS:**

```powershell
nslookup app.townofwiley.gov
```

**Check service status:**

```powershell
sc query cloudflared
```

### Webhooks Not Receiving Events

1. **Check signature validation:**
   - Verify VerifierToken in user secrets
   - Check webhook logs for signature errors

2. **Test endpoint manually:**

   ```powershell
   Invoke-WebRequest -Uri https://app.townofwiley.gov/qbo/webhooks -Method POST -Body '{"test":"data"}'
   ```

3. **Verify Intuit configuration:**
   - Endpoint URL matches exactly
   - Verifier token is correct
   - Events are subscribed

### OAuth Flow Failing

1. **Check redirect URI:**
   - Must exactly match `http://localhost:5000/`
   - Trailing slash matters!
   - Use http:// not https:// for local HttpListener

2. **Verify user secrets:**

   ```powershell
   dotnet user-secrets list --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj
   ```

3. **Check URL ACL (Windows):**

   ```powershell
   netsh http show urlacl url=http://localhost:5000/
   ```

   **Add if missing:**

   ```powershell
   netsh http add urlacl url=http://localhost:5000/ user=EVERYONE
   ```

---

## Security Best Practices

### Production

1. **Never commit secrets to source control**
   - Use user secrets for development
   - Use Azure Key Vault or environment variables for production

2. **Enforce HTTPS**
   - Tunnel handles SSL termination
   - Webhooks server validates signatures

3. **Rotate credentials regularly**
   - Client Secret: Every 90 days
   - Verifier Token: Every 180 days

4. **Monitor webhook logs**
   - Alert on signature validation failures
   - Track unusual traffic patterns

5. **Restrict network access**
   - Firewall rules for webhooks server
   - IP allowlist in Cloudflare

### Development

1. **Use sandbox environment**
   - Set `"Environment": "sandbox"` in appsettings.json
   - Separate credentials from production

2. **Local-only OAuth callbacks**
   - No tunnel needed for OAuth
   - Callbacks stay on localhost

3. **Test signature validation**
   - Verify with real webhook events
   - Test signature mismatch handling

---

## Support Resources

- **Intuit Developer Docs:** https://developer.intuit.com/app/developer/qbo/docs
- **Cloudflare Tunnel Docs:** https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/
- **Internal Docs:** `docs/USER-SECRETS.md`, `docs/XAI_API_SETUP_QUICKSTART.md`

---

**Document Version:** 1.0
**Last Verified:** 2026-02-09
**Next Review:** 2026-04-09
