# xAI API Key Setup Quick Start

**Quick Links:**

- 📖 [Full E2E Documentation](./E2E_XAI_API_CONFIGURATION_FLOW.md)
- 🔍 [Verification Script](../scripts/verify-xai-api-key.ps1)
- ⚠️ [Troubleshooting Guide](#troubleshooting)

---

## ⚡ 3-MINUTE SETUP

### Option 1: User Secrets (✅ Recommended)

```powershell
# 1. Set your API key
dotnet user-secrets set "XAI:ApiKey" "xai-xxxxxxxxxxxxxxxx"

# 2. Verify it was saved
dotnet user-secrets list
# Output: XAI:ApiKey = ******* (masked)

# 3. Run the app
dotnet run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj
```

✅ **Done!** Key is stored securely, never committed to git.

---

### Option 2: Environment Variable

#### PowerShell (Current Session Only)

```powershell
$Env:XAI__ApiKey = "xai-xxxxxxxxxxxxxxxx"
dotnet run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj
```

#### PowerShell (Permanent - User Level)

```powershell
[Environment]::SetEnvironmentVariable("XAI__ApiKey", "xai-xxxxxxxxxxxxxxxx", "User")
# Restart terminal/IDE for changes to take effect
```

#### Windows System Properties (Permanent - Machine Level)

1. Press `Win + X` → "Edit environment variables"
2. Click "Environment Variables"
3. Under "System variables", click "New"
4. Variable name: `XAI__ApiKey` (note: double underscore)
5. Variable value: `xai-xxxxxxxxxxxxxxxx`
6. Click "OK", restart application

#### CMD (Permanent)

```cmd
setx XAI__ApiKey "xai-xxxxxxxxxxxxxxxx"
```

---

## ✅ VERIFY YOUR SETUP

```powershell
# Run the verification script
.\scripts\verify-xai-api-key.ps1

# Or withAPI connectivity test:
.\scripts\verify-xai-api-key.ps1 -TestApi

# Expected output:
# ✅ XAI__ApiKey found
# ✅ appsettings.json ApiKey properly empty
# ✅ API connectivity test passed
```

---

## 🔍 HOW IT WORKS

**Configuration Priority (highest to lowest):**

```
┌─────────────────────────────────────────────────────┐
│ 1. User Secrets (Most Secure)                       │
│    dotnet user-secrets set "XAI:ApiKey" "key"       │
├─────────────────────────────────────────────────────┤
│ 2. Environment Variable (System-Wide)               │
│    XAI__ApiKey = "key"  (double underscore)         │
│    or XAI_API_KEY = "key" (legacy, deprecated)      │
├─────────────────────────────────────────────────────┤
│ 3. appsettings.json (NEVER USE - Public!)           │
│    ApiKey field intentionally empty for security    │
└─────────────────────────────────────────────────────┘
```

**Startup Flow:**

1. App loads configuration from all sources
2. GrokApiKeyProvider reads key from highest-priority source
3. Health check validates key works (logs "validation successful" or "401 Unauthorized")
4. JARVIS chat panel becomes available
5. API calls use the configured key

---

## ⚠️ TROUBLESHOOTING

### "API key validation failed (401 Unauthorized)"

**Problem:** Key is invalid, expired, or formatted incorrectly

**Solution:**

```powershell
# 1. Verify your key is correct and not expired on x.ai website

# 2. Check current configuration
dotnet user-secrets list
$Env:XAI__ApiKey  # or check Windows env vars

# 3. Clear and reset
dotnet user-secrets remove "XAI:ApiKey"
dotnet user-secrets set "XAI:ApiKey" "your-actual-key-here"

# 4. Restart IDE/terminal and app
# Watch logs for: "[Grok] API key validation successful"
```

### "API key is not configured"

**Problem:** No key found in any configuration source

**Solution:**

```powershell
# Option 1: User Secrets (recommended)
dotnet user-secrets set "XAI:ApiKey" "xai-xxxxx"

# Option 2: Environment Variable
$Env:XAI__ApiKey = "xai-xxxxx"

# Verify with script
.\scripts\verify-xai-api-key.ps1
```

### "Chat panel shows 'Service not available'"

**Problem:** GrokAgentService failed to initialize

**Solution:**

```powershell
# 1. Check health check passed:
# Look in logs for: "[Grok] Grok health check: Healthy"

# 2. Verify API key setup:
.\scripts\verify-xai-api-key.ps1 -TestApi

# 3. Check service is registered:
# Check DependencyInjection.cs has GrokAgentService registration

# 4. Restart app and watch startup logs
dotnet run ... 2>&1 | Select-String "Grok"
```

### "API calls timeout (15 seconds)"

**Problem:** Network connectivity or xAI server issue

**Solution:**

```powershell
# 1. Check network
Test-NetConnection api.x.ai -Port 443
# Should show: "ComputerName: api.x.ai TcpTestSucceeded: True"

# 2. Check firewall allows HTTPS outbound
# Windows: Check Windows Defender Firewall allows dotnet.exe

# 3. Manual API test (if you have curl)
curl -H "Authorization: Bearer your-key" `
  -H "Content-Type: application/json" `
  -d '{"input":[{"role":"user","content":"hi"}],"model":"grok-4.1"}' `
  https://api.x.ai/v1/responses

# 4. APP automatically retries (exponential backoff: 1s, 2s, 4s, 8s, 15s)
# If manual curl works but app fails: it's likely an env var propagation issue
# Try: restart terminal/IDE, or use user-secrets instead of env var
```

---

## 🚀 NEXT STEPS

1. **Setup:** Choose your method above and set the API key
2. **Verify:** Run `.\scripts\verify-xai-api-key.ps1`
3. **Build:** `dotnet build WileyWidget.sln -m:2`
4. **Run:** `dotnet run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj`
5. **Test:** Try JARVIS chat, watch logs for "API key validation successful"

---

## 📚 DETAILED DOCUMENTATION

For comprehensive information including production deployment, security best practices, and advanced troubleshooting:

👉 **[Read the Full E2E Documentation](./E2E_XAI_API_CONFIGURATION_FLOW.md)**

---

## 🔐 SECURITY NOTES

✅ **DO:**

- Store API keys in user-secrets (development)
- Store API keys in environment variables (production)
- Keep appsettings.json empty
- Never commit keys to git

❌ **DON'T:**

- Store real API keys in appsettings.json
- Commit .gitignore files that expose keys
- Hardcode keys in source code
- Share API keys in logs (they're masked automatically)

---

## 📋 QUICK REFERENCE

| Task       | Command                                                                     |
| ---------- | --------------------------------------------------------------------------- |
| Set Key    | `dotnet user-secrets set "XAI:ApiKey" "key"`                                |
| View Keys  | `dotnet user-secrets list`                                                  |
| Delete Key | `dotnet user-secrets remove "XAI:ApiKey"`                                   |
| Test Setup | `.\scripts\verify-xai-api-key.ps1 -TestApi`                                 |
| Build      | `dotnet build WileyWidget.sln -m:2`                                         |
| Run        | `dotnet run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj` |

---

**Last Updated:** February 6, 2026
**Status:** Production Ready
**Questions?** See [E2E_XAI_API_CONFIGURATION_FLOW.md](./E2E_XAI_API_CONFIGURATION_FLOW.md)
