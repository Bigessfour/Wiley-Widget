# Verify xAI API Key Configuration
# This script validates that your xAI/Grok API key is properly configured
# Run from workspace root: .\scripts\verify-xai-api-key.ps1

param(
    [switch]$TestApi = $false,
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Continue"
$WarningPreference = "Continue"

Write-Host "`n╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  xAI API Key Configuration Verification Script                 ║" -ForegroundColor Cyan
Write-Host "║  See: docs/E2E_XAI_API_CONFIGURATION_FLOW.md                    ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

$issues = @()
$successes = @()

# ==============================================================================
# STEP 1: Check Environment Variables
# ==============================================================================
Write-Host "STEP 1: Checking Environment Variables..." -ForegroundColor Yellow
Write-Host "─────────────────────────────────────────────────────────────────" -ForegroundColor Gray

$xaiModern = [Environment]::GetEnvironmentVariable("XAI__ApiKey")
$xaiLegacy = [Environment]::GetEnvironmentVariable("XAI_API_KEY")

if ($null -ne $xaiModern -and $xaiModern -ne "") {
    Write-Host "  ✅ XAI__ApiKey (modern) found: $(($xaiModern.Substring(0, 4)) + '...' + ($xaiModern.Substring($xaiModern.Length - 4)))" `
        -ForegroundColor Green
    $successes += "XAI__ApiKey environment variable set"
} elseif ($null -ne $xaiLegacy -and $xaiLegacy -ne "") {
    Write-Host "  ⚠️  XAI_API_KEY (LEGACY, single underscore) found" -ForegroundColor Yellow
    Write-Host "      Please migrate to XAI__ApiKey (double underscore) for Microsoft convention" -ForegroundColor Yellow
    $issues += "Using legacy XAI_API_KEY instead of XAI__ApiKey"
} else {
    Write-Host "  ❌ No XAI API key environment variable found" -ForegroundColor Red
    $issues += "XAI__ApiKey environment variable not set"
}

Write-Host ""

# ==============================================================================
# STEP 2: Check User Secrets
# ==============================================================================
Write-Host "STEP 2: Checking User Secrets..." -ForegroundColor Yellow
Write-Host "─────────────────────────────────────────────────────────────────" -ForegroundColor Gray

try {
    $secrets = dotnet user-secrets list 2>&1
    $secretsOutput = $secrets -join "`n"

    if ($secretsOutput -match "XAI:ApiKey") {
        Write-Host "  ✅ User secret 'XAI:ApiKey' found" -ForegroundColor Green
        $successes += "User secret XAI:ApiKey is configured"
    } else {
        Write-Host "  ⚠️  User secret 'XAI:ApiKey' not found" -ForegroundColor Yellow
        Write-Host "      Set it with: dotnet user-secrets set 'XAI:ApiKey' 'your-key'" -ForegroundColor Yellow
        $issues += "User secret XAI:ApiKey not configured"
    }
} catch {
    Write-Host "  ⚠️  Could not check user-secrets: $_" -ForegroundColor Yellow
    Write-Host "      Make sure you're in a project directory with valid .csproj" -ForegroundColor Yellow
}

Write-Host ""

# ==============================================================================
# STEP 3: Check appsettings.json
# ==============================================================================
Write-Host "STEP 3: Checking appsettings.json..." -ForegroundColor Yellow
Write-Host "─────────────────────────────────────────────────────────────────" -ForegroundColor Gray

$appsettingsPath = "src\WileyWidget.WinForms\appsettings.json"
if (Test-Path $appsettingsPath) {
    try {
        $config = Get-Content $appsettingsPath -Raw | ConvertFrom-Json

        if ($null -ne $config.XAI -and $config.XAI.Enabled -eq $true) {
            Write-Host "  ✅ XAI configuration found in appsettings.json" -ForegroundColor Green

            if ($config.XAI.ApiKey -eq "" -or $null -eq $config.XAI.ApiKey) {
                Write-Host "  ✅ ApiKey field is empty (correct - don't store real keys in source control)" -ForegroundColor Green
                $successes += "appsettings.json ApiKey properly empty"
            } else {
                Write-Host "  ❌ SECURITY WARNING: appsettings.json contains non-empty ApiKey!" -ForegroundColor Red
                Write-Host "     This should be empty. Move the key to user-secrets:" -ForegroundColor Red
                Write-Host "     dotnet user-secrets set 'XAI:ApiKey' '<your-key>'" -ForegroundColor Red
                $issues += "appsettings.json contains real API key (SECURITY RISK)"
            }

            Write-Host "     Endpoint: $($config.XAI.Endpoint)" -ForegroundColor Gray
            Write-Host "     Model: $($config.XAI.Model)" -ForegroundColor Gray
        } else {
            Write-Host "  ⚠️  XAI section disabled or not found" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  ❌ Error reading appsettings.json: $_" -ForegroundColor Red
        $issues += "Invalid appsettings.json format"
    }
} else {
    Write-Host "  ❌ appsettings.json not found at $appsettingsPath" -ForegroundColor Red
    $issues += "appsettings.json file not found"
}

Write-Host ""

# ==============================================================================
# STEP 4: Check Configuration Precedence
# ==============================================================================
Write-Host "STEP 4: Configuration Precedence Check..." -ForegroundColor Yellow
Write-Host "─────────────────────────────────────────────────────────────────" -ForegroundColor Gray

$foundSources = @()

if ($null -ne $xaiModern -and $xaiModern -ne "") {
    $foundSources += "XAI__ApiKey (environment variable - TIER 2)"
} elseif ($null -ne $xaiLegacy -and $xaiLegacy -ne "") {
    $foundSources += "XAI_API_KEY (legacy environment variable - TIER 2)"
}

try {
    if ($secretsOutput -match "XAI:ApiKey") {
        $foundSources = @("User Secrets (TIER 1 - highest priority)") + $foundSources
    }
} catch {
    # Continue
}

if ($foundSources.Count -gt 0) {
    Write-Host "  ✅ Configuration sources found (in priority order):" -ForegroundColor Green
    $foundSources | ForEach-Object {
        Write-Host "     • $_" -ForegroundColor Green
    }
} else {
    Write-Host "  ❌ No API key configuration found in any source!" -ForegroundColor Red
    Write-Host "     You must set the API key in one of these ways:" -ForegroundColor Red
    Write-Host "     1. User Secrets (recommended): dotnet user-secrets set 'XAI:ApiKey' 'key'" -ForegroundColor Yellow
    Write-Host "     2. Environment Variable: `$Env:XAI__ApiKey = 'key'" -ForegroundColor Yellow
    Write-Host "     3. appsettings.Development.json (dev only, in .gitignore)" -ForegroundColor Yellow
}

Write-Host ""

# ==============================================================================
# STEP 5: Optional - Test API Connectivity
# ==============================================================================
if ($TestApi) {
    Write-Host "STEP 5: Testing API Connectivity..." -ForegroundColor Yellow
    Write-Host "─────────────────────────────────────────────────────────────────" -ForegroundColor Gray

    # Get the API key from environment (this is what will be used)
    $apiKey = $xaiModern ?? $xaiLegacy

    if ($null -ne $apiKey -and $apiKey -ne "") {
        Write-Host "  Testing with API key: $(($apiKey.Substring(0, 4)) + '...' + ($apiKey.Substring($apiKey.Length - 4)))" -ForegroundColor Gray

        try {
            $headers = @{
                "Authorization" = "Bearer $apiKey"
                "Content-Type" = "application/json"
            }

            $body = @{
                input = @(
                    @{ role = "user"; content = "Hello, please respond with 'Hello, Grok!'" }
                )
                model = "grok-4.1"
                stream = $false
                max_tokens = 100
            } | ConvertTo-Json

            Write-Host "  Sending test request to https://api.x.ai/v1/responses..." -ForegroundColor Gray

            $response = Invoke-WebRequest -Uri "https://api.x.ai/v1/responses" `
                -Method Post `
                -Headers $headers `
                -Body $body `
                -TimeoutSec 15 `
                -ErrorAction Stop

            if ($response.StatusCode -eq 200) {
                Write-Host "  ✅ API Response: 200 OK" -ForegroundColor Green
                Write-Host "  ✅ API Key is VALID and accepted by xAI" -ForegroundColor Green
                $successes += "API connectivity test passed"
            }
        } catch {
            $statusCode = $_.Exception.Response.StatusCode.Value__ 2>$null

            if ($statusCode -eq 401) {
                Write-Host "  ❌ API Response: 401 Unauthorized" -ForegroundColor Red
                Write-Host "     Your API key is INVALID or EXPIRED" -ForegroundColor Red
                Write-Host "     Verify your key is correct and still active on x.ai" -ForegroundColor Red
                $issues += "API key validation failed (401 Unauthorized)"
            } elseif ($statusCode -eq 429) {
                Write-Host "  ⚠️  API Response: 429 Too Many Requests" -ForegroundColor Yellow
                Write-Host "     Rate limit exceeded, but API is reachable" -ForegroundColor Yellow
            } elseif ($statusCode -eq 503) {
                Write-Host "  ⚠️  API Response: 503 Service Unavailable" -ForegroundColor Yellow
                Write-Host "     xAI service is temporarily down" -ForegroundColor Yellow
            } else {
                Write-Host "  ❌ API Error: $($_.Exception.Message)" -ForegroundColor Red
                Write-Host "     Status Code: $statusCode" -ForegroundColor Red
                $issues += "API request failed: $statusCode"
            }
        }
    } else {
        Write-Host "  ⚠️  Cannot test API - no API key configured" -ForegroundColor Yellow
    }

    Write-Host ""
}

# ==============================================================================
# SUMMARY
# ==============================================================================
Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  SUMMARY                                                       ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

if ($successes.Count -gt 0) {
    Write-Host "`n✅ Successes ($($successes.Count)):" -ForegroundColor Green
    $successes | ForEach-Object { Write-Host "   • $_" -ForegroundColor Green }
}

if ($issues.Count -gt 0) {
    Write-Host "`n❌ Issues Found ($($issues.Count)):" -ForegroundColor Red
    $issues | ForEach-Object { Write-Host "   • $_" -ForegroundColor Red }
    Write-Host "`n📖 For detailed setup instructions, see: docs/E2E_XAI_API_CONFIGURATION_FLOW.md" -ForegroundColor Yellow
} else {
    Write-Host "`n✅ All checks passed! Your xAI API key is properly configured." -ForegroundColor Green
    Write-Host "`n🚀 Ready to use JARVIS chat and AI features!" -ForegroundColor Green
}

Write-Host "`n📋 Quick Commands:" -ForegroundColor Cyan
Write-Host "   Set User Secret:    dotnet user-secrets set 'XAI:ApiKey' 'your-key'" -ForegroundColor Gray
Write-Host "   List User Secrets:  dotnet user-secrets list" -ForegroundColor Gray
Write-Host "   Test API:           .\scripts\verify-xai-api-key.ps1 -TestApi" -ForegroundColor Gray
Write-Host "   Build:              dotnet build WileyWidget.sln -m:2" -ForegroundColor Gray
Write-Host "   Run App:            dotnet run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj" -ForegroundColor Gray

Write-Host ""

# Exit with appropriate code
if ($issues.Count -gt 0) {
    exit 1
} else {
    exit 0
}
