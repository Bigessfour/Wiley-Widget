# WileyWidget.Webhooks (QuickBooks Online)

Production-ready receiver for Intuit QuickBooks Online Webhooks.

## Quick Start

### Development

```powershell
cd src/WileyWidget.Webhooks
dotnet run
```

Listens on:

- HTTPS: <https://localhost:7207>
- HTTP: <http://localhost:5207>

### Production

See `docs/PRODUCTION_QUICKBOOKS_SETUP.md` for complete deployment guide.

---

## Configuration

### User Secrets (Required)

```powershell
dotnet user-secrets set "Services:QuickBooks:Webhooks:VerifierToken" "YOUR_TOKEN" --project src/WileyWidget.Webhooks/WileyWidget.Webhooks.csproj
```

### appsettings.json

```json
{
  "Services": {
    "QuickBooks": {
      "OAuth": {
        "Environment": "sandbox" // or "production"
      },
      "Webhooks": {
        "EndpointUrl": "https://app.townofwiley.gov/qbo/webhooks",
        "VerifierToken": "" // Use user secrets, not appsettings
      }
    }
  }
}
```

**Priority:** User Secrets > Environment Variables > appsettings.json

---

## Endpoints

### Public Endpoints

| Path              | Method | Description                                           |
| ----------------- | ------ | ----------------------------------------------------- |
| `/health`         | GET    | Health check - returns `{"status":"ok"}`              |
| `/`               | GET    | Landing page                                          |
| `/app/launch`     | GET    | Intuit app launch handler                             |
| `/app/disconnect` | GET    | Intuit app disconnect handler                         |
| `/privacy`        | GET    | Privacy policy page                                   |
| `/eula`           | GET    | End-user license agreement                            |
| `/qbo/webhooks`   | POST   | **QuickBooks webhook receiver** (signature validated) |

### Webhook Endpoint Details

**URL:** `https://app.townofwiley.gov/qbo/webhooks`

**Authentication:** HMACSHA256 signature validation

- Header: `intuit-signature` or `X-Intuit-Signature`
- Algorithm: `base64(HMACSHA256(body, verifier-token))`
- Comparison: Constant-time to prevent timing attacks

**Behavior:**

- **Sandbox mode** (`Environment: "sandbox"`): Accepts requests without signature for testing
- **Production mode** (`Environment: "production"`): **Requires valid signature** or returns 503

**Response Codes:**

- `200 OK`: Webhook received and validated
- `401 Unauthorized`: Invalid or missing signature
- `503 Service Unavailable`: Production mode without verifier token configured

---

## Production Deployment

### 1. Configure User Secrets

```powershell
# Must match the token in Intuit Developer Portal
dotnet user-secrets set "Services:QuickBooks:Webhooks:VerifierToken" "772614ab-07db-4f6a-b7eb-d86985774d71" --project src/WileyWidget.Webhooks/WileyWidget.Webhooks.csproj
```

### 2. Set Environment to Production

**appsettings.Production.json:**

```json
{
  "Services": {
    "QuickBooks": {
      "OAuth": {
        "Environment": "production"
      }
    }
  }
}
```

### 3. Deploy to Server

**Option A: Windows Service (NSSM)**

```powershell
nssm install WileyWidgetWebhooks "C:\Program Files\dotnet\dotnet.exe"
nssm set WileyWidgetWebhooks AppDirectory "C:\inetpub\WileyWidget\Webhooks"
nssm set WileyWidgetWebhooks AppParameters "WileyWidget.Webhooks.dll"
nssm set WileyWidgetWebhooks Start SERVICE_AUTO_START
net start WileyWidgetWebhooks
```

**Option B: IIS Reverse Proxy**

- Install ASP.NET Core Hosting Bundle
- Configure IIS site with reverse proxy to port 7207
- Use application pool with .NET CLR Version: No Managed Code

### 4. Configure Cloudflare Tunnel

See `docs/PRODUCTION_QUICKBOOKS_SETUP.md` for complete tunnel setup.

**Quick reference:**

```powershell
cloudflared tunnel create wiley-widget
cloudflared tunnel route dns wiley-widget app.townofwiley.gov
cloudflared service install
net start cloudflared
```

### 5. Configure Intuit Developer Portal

**Webhooks Settings:**

- Endpoint URL: `https://app.townofwiley.gov/qbo/webhooks`
- Verifier Token: `772614ab-07db-4f6a-b7eb-d86985774d71`
- Events: Subscribe to relevant entity types

---

## Testing

### Local Testing (Sandbox)

```powershell
# Start server
cd src/WileyWidget.Webhooks
dotnet run

# Test health check
Invoke-WebRequest -Uri https://localhost:7207/health

# Test webhook endpoint (no signature required in sandbox)
$body = '{"test":"data"}'
Invoke-WebRequest -Uri https://localhost:7207/qbo/webhooks -Method POST -Body $body
```

### Production Testing

```powershell
# Test public health check
Invoke-WebRequest -Uri https://app.townofwiley.gov/health

# Test webhook with valid signature
# (Signature generation script - see docs/PRODUCTION_QUICKBOOKS_SETUP.md)
```

### Manual Signature Validation Test

```powershell
# PowerShell signature generator
$body = '{"eventNotifications":[{"realmId":"123456789"}]}'
$secret = "772614ab-07db-4f6a-b7eb-d86985774d71"
$hmac = New-Object System.Security.Cryptography.HMACSHA256
$hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($secret)
$hash = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($body))
$signature = [Convert]::ToBase64String($hash)

# Send request with signature
$headers = @{ "intuit-signature" = $signature }
Invoke-WebRequest -Uri https://localhost:7207/qbo/webhooks -Method POST -Body $body -Headers $headers
```

---

## Security

### Signature Validation

**Implementation:** `Program.cs:36-77`

1. Reads raw request body
2. Extracts `intuit-signature` header
3. Computes HMACSHA256(body, verifier-token)
4. Compares using `CryptographicOperations.FixedTimeEquals()` (constant-time)
5. Rejects invalid signatures with 401 Unauthorized

### Production Enforcement

- **Sandbox mode:** Accepts unsigned requests (logs warning)
- **Production mode:** **Requires** valid signature or returns 503
- No bypass mechanism in production - security enforced at runtime

### Best Practices

1. **Never log the verifier token** - it's a secret!
2. **Always use user secrets** for local development
3. **Use Azure Key Vault** or environment variables for production
4. **Rotate tokens regularly** (every 180 days recommended)
5. **Monitor for signature failures** - may indicate attack attempts

---

## Troubleshooting

### Signature Validation Failing

**Check:**

1. Verifier token in user secrets matches Intuit portal exactly
2. Request body is read as raw string (not parsed JSON)
3. Signature header name is correct: `intuit-signature` or `X-Intuit-Signature`

**Debug:**

```powershell
# Enable detailed logging
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/WileyWidget.Webhooks/WileyWidget.Webhooks.csproj
```

### Production Mode Rejecting All Requests

**Symptom:** All webhooks return 503 Service Unavailable

**Cause:** VerifierToken not configured in user secrets

**Fix:**

```powershell
dotnet user-secrets set "Services:QuickBooks:Webhooks:VerifierToken" "YOUR_TOKEN" --project src/WileyWidget.Webhooks/WileyWidget.Webhooks.csproj
```

### Tunnel Connection Issues

**Symptom:** 502 Bad Gateway from `app.townofwiley.gov`

**Check:**

1. Webhook server is running: `netstat -an | findstr :7207`
2. Tunnel is active: `sc query cloudflared`
3. config.yml has correct port and noTLSVerify setting

**Fix:** See `docs/PRODUCTION_QUICKBOOKS_SETUP.md` Section "Troubleshooting"

---

## Monitoring

### Health Checks

**Automated check script:**

```powershell
while ($true) {
    try {
        $response = Invoke-WebRequest -Uri https://app.townofwiley.gov/health -TimeoutSec 5
        Write-Host "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - Health check OK" -ForegroundColor Green
    } catch {
        Write-Host "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - Health check FAILED" -ForegroundColor Red
    }
    Start-Sleep -Seconds 60
}
```

### Log Monitoring

```powershell
# View recent logs
Get-Content "logs\*.log" -Tail 50 -Wait

# Search for signature failures
Select-String -Path "logs\*.log" -Pattern "Signature mismatch" |Select-Object -Last 20
```

---

## References

- **Intuit Webhooks Docs:** https://developer.intuit.com/app/developer/qbo/docs/develop/webhooks
- **Full Production Setup:** `docs/PRODUCTION_QUICKBOOKS_SETUP.md`
- **Deployment Checklist:** `docs/QUICKBOOKS_DEPLOYMENT_CHECKLIST.md`
- **Architecture:** `docs/ARCHITECTURE_PATTERNS.md`

---

**Status:** ✅ Production Ready
**Last Updated:** 2026-02-09
**Version:** 2.0
