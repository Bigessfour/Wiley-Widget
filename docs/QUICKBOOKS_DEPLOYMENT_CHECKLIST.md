# QuickBooks Production Deployment - Quick Reference

## ✅ Pre-Deployment Checklist

### 1. User Secrets (COMPLETED ✓)

- [x] WinForms: OAuth ClientId set
- [x] WinForms: OAuth ClientSecret set
- [x] WinForms: Webhooks VerifierToken set
- [x] Webhooks Server: VerifierToken set (must match WinForms)

**Verify:**

```powershell
dotnet user-secrets list --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj
dotnet user-secrets list --project src/WileyWidget.Webhooks/WileyWidget.Webhooks.csproj
```

### 2. Configuration Files

**WinForms appsettings.json:**

- [x] `OAuth.Environment`: "sandbox" (for dev) → "production" (for prod)
- [x] `OAuth.RedirectUri`: "http://localhost:5000/" (local dev)
- [x] `Webhooks.EndpointUrl`: "https://app.townofwiley.gov/qbo/webhooks"
- [x] `Webhooks.VerifierToken`: "" (empty - use user secrets)
- [x] `App.HostDomain`: "app.townofwiley.gov"
- [x] `App.LaunchUrl`: "https://app.townofwiley.gov/app/launch"
- [x] `App.DisconnectUrl`: "https://app.townofwiley.gov/app/disconnect"
- [x] `App.EulaUrl`: "https://app.townofwiley.gov/eula"
- [x] `App.PrivacyPolicyUrl`: "https://app.townofwiley.gov/privacy"

### 2.5. Development Redirect URIs (SANDBOX ONLY)

**For testing platforms during development:**

- [ ] **OAuth Playground**: `https://developer.intuit.com/v2/OAuth2Playground/RedirectUrl`
- [ ] **Postman**: `https://oauth.pstmn.io/v1/callback`
- [ ] **Insomnia**: `http://localhost:7744/`
- [ ] **Custom testing tools**: Add your specific redirect URIs

**Note:** These are only needed for sandbox/development. Remove them for production apps.

- [ ] Install cloudflared: `winget install --id Cloudflare.cloudflared`
- [ ] Authenticate: `cloudflared tunnel login`
- [ ] Create tunnel: `cloudflared tunnel create wiley-widget`
- [ ] Configure DNS: `cloudflared tunnel route dns wiley-widget app.townofwiley.gov`
- [ ] Create config.yml (see below)
- [ ] Test tunnel: `cloudflared tunnel run wiley-widget`
- [ ] Install as service: `cloudflared service install`
- [ ] Start service: `net start cloudflared`
- [ ] Set auto-start: `sc config cloudflared start=auto`

**Config file location:** `C:\Users\<user>\.cloudflared\config.yml`

```yaml
tunnel: <YOUR-TUNNEL-ID>
credentials-file: C:\Users\<user>\.cloudflared\<YOUR-TUNNEL-ID>.json

ingress:
  - hostname: app.townofwiley.gov
    service: https://localhost:7207
    originRequest:
      noTLSVerify: true
  - service: http_status:404
```

### 4. Intuit Developer Portal (https://developer.intuit.com)

**Keys & OAuth section:**

- [x] Client ID: ABwfJ8obzq6SVLemLAVurAFCQYRxjoWiGDE4KKU380RCcFNWiD
- [x] Client Secret: BGiENFpuLDO1vfFPCnQLsA2pwT43VP2UZoioneh7
- [ ] Redirect URIs: Add `http://localhost:5000/` (local dev) and `https://app.townofwiley.gov/oauth/callback` (production)

**Webhooks section:**

- [ ] Endpoint URL: `https://app.townofwiley.gov/qbo/webhooks`
- [x] Verifier Token: 772614ab-07db-4f6a-b7eb-d86985774d71
- [ ] Subscribe to events (Customer, Invoice, etc.)

**App Settings section:**

- [x] Host Domain: `app.townofwiley.gov`
- [x] Launch URL: `https://app.townofwiley.gov/app/launch`
- [x] Disconnect URL: `https://app.townofwiley.gov/app/disconnect`
- [x] Privacy Policy: `https://app.townofwiley.gov/privacy`
- [x] EULA: `https://app.townofwiley.gov/eula`

### 5. Production Server Setup

**Deploy Webhooks Server:**

```powershell
# Copy webhook server files to production server
# C:\inetpub\WileyWidget\Webhooks\

# Install as Windows Service (optional) or run via IIS
cd C:\inetpub\WileyWidget\Webhooks
dotnet WileyWidget.Webhooks.dll
```

**Firewall Rules:**

```powershell
# Allow HTTPS on port 7207
New-NetFirewallRule -DisplayName "WileyWidget Webhooks HTTPS" -Direction Inbound -LocalPort 7207 -Protocol TCP -Action Allow
```

**Windows Service Setup (optional):**

```powershell
# Using NSSM (Non-Sucking Service Manager)
nssm install WileyWidgetWebhooks "C:\Program Files\dotnet\dotnet.exe"
nssm set WileyWidgetWebhooks AppDirectory "C:\inetpub\WileyWidget\Webhooks"
nssm set WileyWidgetWebhooks AppParameters "WileyWidget.Webhooks.dll"
nssm set WileyWidgetWebhooks Start SERVICE_AUTO_START
net start WileyWidgetWebhooks
```

---

## 🧪 Testing Procedures

### Local Development Testing

1. **Start Webhooks Server:**

   ```powershell
   cd src/WileyWidget.Webhooks
   dotnet run
   ```

2. **Verify Health Check:**

   ```powershell
   Invoke-WebRequest -Uri https://localhost:7207/health
   # Should return: {"status":"ok"}
   ```

3. **Run WinForms App:**

   ```powershell
   cd src/WileyWidget.WinForms
   dotnet run
   ```

4. **Test OAuth Flow:**
   - Click "Connect to QuickBooks" button
   - Browser opens to Intuit authorization page
   - Log in with sandbox credentials
   - Authorize the app
   - Browser should close automatically
   - App should show "Connected" status

### Production Testing (After Tunnel Setup)

1. **Test Public Endpoint:**

   ```powershell
   Invoke-WebRequest -Uri https://app.townofwiley.gov/health
   # Should return: {"status":"ok"}
   ```

2. **Test Webhook Endpoint (without signature):**

   ```powershell
   $body = '{"test":"data"}'
   Invoke-WebRequest -Uri https://app.townofwiley.gov/qbo/webhooks -Method POST -Body $body
   # Should return 503 in production mode without valid signature
   ```

3. **Test App URLs:**
   - https://app.townofwiley.gov/app/launch
   - https://app.townofwiley.gov/app/disconnect
   - https://app.townofwiley.gov/privacy
   - https://app.townofwiley.gov/eula

4. **Test OAuth Flow (Production Mode):**
   - Update appsettings.json: `"Environment": "production"`
   - Restart WinForms app
   - Test OAuth flow with production credentials
   - Verify tokens are stored securely

5. **Test Webhook Delivery:**
   - Trigger an event in QuickBooks (create invoice, etc.)
   - Check webhook server logs for incoming request
   - Verify signature validation passes
   - Confirm event is processed

---

## 🚨 Troubleshooting

### OAuth Callback Not Working

**Symptom:** Browser opens but doesn't redirect back to app

**Check:**

1. Callback handler started: Look for log message "OAuth callback handler started and listening"
2. Redirect URI matches exactly: `http://localhost:5000/` (trailing slash! http not https!)
3. URL ACL configured (Windows):
   ```powershell
   netsh http show urlacl url=http://localhost:5000/
   ```

**Fix:**

```powershell
netsh http add urlacl url=http://localhost:5000/ user=EVERYONE
```

### Webhooks Not Receiving Events

**Symptom:** Events in QuickBooks but no webhook calls

**Check:**

1. Tunnel is running: `sc query cloudflared`
2. Public endpoint accessible: `Invoke-WebRequest https://app.townofwiley.gov/health`
3. Intuit portal configured with correct endpoint URL
4. Events are subscribed in Intuit portal
5. Webhook server logs for errors

**Fix:**

```powershell
# Restart tunnel
net stop cloudflared
net start cloudflared

# Restart webhook server
Restart-Service WileyWidgetWebhooks
```

### Signature Validation Failing

**Symptom:** Webhooks rejected with "Signature mismatch"

**Check:**

1. VerifierToken in user secrets matches Intuit portal
2. Request body is read raw (not parsed)
3. Signature header name: "intuit-signature" or "X-Intuit-Signature"

**Fix:**

```powershell
# Verify token matches
dotnet user-secrets list --project src/WileyWidget.Webhooks/WileyWidget.Webhooks.csproj
# Compare with Intuit portal → Webhooks section → Verifier Token

# Update if needed
dotnet user-secrets set "Services:QuickBooks:Webhooks:VerifierToken" "YOUR_TOKEN" --project src/WileyWidget.Webhooks/WileyWidget.Webhooks.csproj
```

### Tunnel Connection Issues

**Symptom:** 502 Bad Gateway or connection timeout

**Check:**

1. Webhook server is running on port 7207
2. Tunnel config.yml points to correct port
3. Certificate errors (noTLSVerify should be true for localhost)

**Fix:**

```yaml
# Update config.yml
ingress:
  - hostname: app.townofwiley.gov
    service: https://localhost:7207 # Ensure correct protocol and port
    originRequest:
      noTLSVerify: true # Required for localhost dev certs
```

---

## 📊 Monitoring

### View Logs

**Webhook Server:**

```powershell
# Real-time logs
Get-Content "C:\inetpub\WileyWidget\Webhooks\logs\*.log" -Tail 50 -Wait

# or if using dotnet run
# Check console output
```

**Cloudflare Tunnel:**

```powershell
Get-Content "C:\Users\<user>\.cloudflared\*.log" -Tail 50 -Wait
```

**WinForms App:**

```powershell
Get-Content "C:\Users\biges\Desktop\Wiley-Widget\logs\errors-*.log" -Tail 50 -Wait
```

### Health Checks

**Create monitoring script** (`monitor-health.ps1`):

```powershell
$endpoints = @(
    "https://app.townofwiley.gov/health",
    "https://app.townofwiley.gov/app/launch"
)

foreach ($endpoint in $endpoints) {
    try {
        $response = Invoke-WebRequest -Uri $endpoint -TimeoutSec 5
        Write-Host "✓ $endpoint - OK ($($response.StatusCode))" -ForegroundColor Green
    } catch {
        Write-Host "✗ $endpoint - FAILED ($($_.Exception.Message))" -ForegroundColor Red
    }
}
```

**Run every 5 minutes:**

```powershell
while ($true) {
    .\monitor-health.ps1
    Start-Sleep -Seconds 300
}
```

---

## 📚 Additional Documentation

- **Full Setup Guide:** `docs/PRODUCTION_QUICKBOOKS_SETUP.md`
- **User Secrets Guide:** `docs/USER-SECRETS.md`
- **Architecture Details:** `docs/ARCHITECTURE_PATTERNS.md`
- **Cloudflare Docs:** https://developers.cloudflare.com/cloudflare-one/
- **Intuit Docs:** https://developer.intuit.com/app/developer/qbo/docs

---

**Status:** ✅ Ready for Production Deployment
**Last Updated:** 2026-02-09
**Version:** 1.0
