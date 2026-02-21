<#
.SYNOPSIS
Setup Assistant for xAI Grok and QuickBooks Integration

.DESCRIPTION
Interactive setup script for configuring both xAI Grok API and QuickBooks services.
Handles user-secrets configuration, environment variables, and GitHub Actions secrets.

.PARAMETER XaiKey
xAI API key (optional - will prompt if not provided)

.PARAMETER QuickBooksClientId
QuickBooks OAuth Client ID (optional - will prompt if not provided)

.PARAMETER QuickBooksClientSecret
QuickBooks OAuth Client Secret (optional - will prompt if not provided)

.PARAMETER EnableUserSecrets
If $true, configure local user-secrets (default: $true)

.PARAMETER EnableGitHubSecrets
If $true, prompt to configure GitHub Actions secrets (default: $false)

.EXAMPLE
# Interactive setup (prompts for all values)
.\scripts\setup\setup-integration-services.ps1

# Non-interactive (for CI/CD pipelines)
.\scripts\setup\setup-integration-services.ps1 `
  -XaiKey "your-xai-key" `
  -QuickBooksClientId "your-client-id" `
  -QuickBooksClientSecret "your-client-secret" `
  -EnableUserSecrets $true

# GitHub Actions secrets only
.\scripts\setup\setup-integration-services.ps1 -EnableGitHubSecrets $true
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$XaiKey,

    [Parameter(Mandatory = $false)]
    [string]$QuickBooksClientId,

    [Parameter(Mandatory = $false)]
    [string]$QuickBooksClientSecret,

    [Parameter(Mandatory = $false)]
    [string]$QuickBooksRealmId,

    [Parameter(Mandatory = $false)]
    [bool]$EnableUserSecrets = $true,

    [Parameter(Mandatory = $false)]
    [bool]$EnableGitHubSecrets = $false,

    [Parameter(Mandatory = $false)]
    [string]$WinFormsProjectDir = "src/WileyWidget.WinForms"
)

# ============================================================================
# UTILITIES
# ============================================================================

function Show-Banner {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Wiley Widget Integration Services Setup" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "This script configures:" -ForegroundColor Yellow
    Write-Host "  • xAI Grok API (for AI-powered recommendations)" -ForegroundColor Yellow
    Write-Host "  • QuickBooks API (for real expense data)" -ForegroundColor Yellow
    Write-Host ""
}

function Read-SecureInput {
    param(
        [string]$Prompt,
        [bool]$Secure = $false,
        [bool]$AllowEmpty = $false
    )

    while ($true) {
        if ($Secure) {
            $value = Read-Host -AsSecureString "$Prompt (or press Enter to skip)"
            if (-not $value) {
                if ($AllowEmpty) { return $null }
                Write-Host "  ⚠️  Value required" -ForegroundColor Yellow
                continue
            }
            # Convert to plaintext for validation/return
            $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($value)
            return [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
        }
        else {
            $value = Read-Host $Prompt
            if ([string]::IsNullOrWhiteSpace($value)) {
                if ($AllowEmpty) { return $null }
                Write-Host "  ⚠️  Value required" -ForegroundColor Yellow
                continue
            }
            return $value
        }
    }
}

function Test-UserSecretsInitialized {
    param([string]$ProjectDir)

    $csprojPath = Join-Path $ProjectDir "*.csproj" | Get-Item -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $csprojPath) {
        Write-Host "  ❌ Project file not found in $ProjectDir" -ForegroundColor Red
        return $false
    }

    $csproj = Get-Content $csprojPath.FullName -Raw
    if ($csproj -match '<UserSecretsId>') {
        Write-Host "  ✅ User Secrets Initialized" -ForegroundColor Green
        return $true
    }
    else {
        Write-Host "  ⚠️  User Secrets not initialized. Initializing..." -ForegroundColor Yellow
        Push-Location $ProjectDir
        try {
            $guidStr = [guid]::NewGuid().ToString()
            dotnet user-secrets init --id $guidStr 2>&1 | Out-Null
            Write-Host "  ✅ User Secrets initialized with ID: $guidStr" -ForegroundColor Green
            return $true
        }
        catch {
            Write-Host "  ❌ Failed to initialize user-secrets: $_" -ForegroundColor Red
            return $false
        }
        finally {
            Pop-Location
        }
    }
}

function Set-EnvironmentVariable {
    param(
        [string]$Name,
        [string]$Value,
        [ValidateSet("User", "Machine", "Process")]
        [string]$Scope = "User"
    )

    try {
        [System.Environment]::SetEnvironmentVariable($Name, $Value, $Scope)
        Write-Host "  ✅ Set $Name in $Scope scope" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "  ❌ Failed to set environment variable: $_" -ForegroundColor Red
        return $false
    }
}

function Set-UserSecret {
    param(
        [string]$ProjectDir,
        [string]$Key,
        [string]$Value
    )

    try {
        Push-Location $ProjectDir
        $env:DOTNET_CLI_TELEMETRY_OPTOUT = "true"

        # Use dotnet user-secrets set
        $output = dotnet user-secrets set $Key $Value 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ User-secret set: $Key" -ForegroundColor Green
            return $true
        }
        else {
            Write-Host "  ❌ Failed to set user-secret $Key : $output" -ForegroundColor Red
            return $false
        }
    }
    catch {
        Write-Host "  ❌ Error setting user-secret: $_" -ForegroundColor Red
        return $false
    }
    finally {
        Pop-Location
    }
}

function Test-UserSecret {
    param([string]$ProjectDir, [string]$Key)

    try {
        Push-Location $ProjectDir
        $output = dotnet user-secrets list 2>&1

        if ($output -match [regex]::Escape($Key)) {
            return $true
        }
        return $false
    }
    catch {
        return $false
    }
    finally {
        Pop-Location
    }
}

function Show-Configuration-Summary {
    param(
        [bool]$XaiConfigured,
        [bool]$QuickBooksConfigured
    )

    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Configuration Summary" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""

    if ($XaiConfigured) {
        Write-Host "✅ xAI Grok API" -ForegroundColor Green
        Write-Host "   Status: CONFIGURED"
        Write-Host "   Location: User Secrets (XAI:ApiKey)"
        Write-Host "   Next: GrokRecommendationService will use this key at startup"
    }
    else {
        Write-Host "⚠️  xAI Grok API" -ForegroundColor Yellow
        Write-Host "   Status: NOT CONFIGURED"
        Write-Host "   To configure manually:"
        Write-Host "   > dotnet user-secrets set 'XAI:ApiKey' 'your-key'"
    }

    Write-Host ""

    if ($QuickBooksConfigured) {
        Write-Host "✅ QuickBooks API" -ForegroundColor Green
        Write-Host "   Status: CONFIGURED"
        Write-Host "   Location: User Secrets (Services:QuickBooks:OAuth:ClientId/Secret)"
        Write-Host "   Next: DepartmentExpenseService will use these credentials"
    }
    else {
        Write-Host "⚠️  QuickBooks API" -ForegroundColor Yellow
        Write-Host "   Status: NOT CONFIGURED"
        Write-Host "   To configure manually:"
        Write-Host "   > dotnet user-secrets set 'Services:QuickBooks:OAuth:ClientId' 'your-id'"
        Write-Host "   > dotnet user-secrets set 'Services:QuickBooks:OAuth:ClientSecret' 'your-secret'"
    }

    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
}

# ============================================================================
# MAIN LOGIC
# ============================================================================

Show-Banner

$xaiConfigured = $false
$quickBooksConfigured = $false

# Check if we should proceed with user-secrets setup
if ($EnableUserSecrets) {
    Write-Host "Step 1: Initializing User Secrets" -ForegroundColor Cyan
    Write-Host ""

    if (-not (Test-Path $WinFormsProjectDir)) {
        Write-Host "  ❌ Project directory not found: $WinFormsProjectDir" -ForegroundColor Red
        Write-Host ""
        exit 1
    }

    if (Test-UserSecretsInitialized -ProjectDir $WinFormsProjectDir) {
        Write-Host ""

        # ============================================================
        # xAI Configuration
        # ============================================================
        Write-Host "Step 2: xAI Grok API Configuration" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Grok API provides AI-powered budget recommendations." -ForegroundColor DarkGray
        Write-Host "Get your API key from: https://console.x.ai/" -ForegroundColor DarkGray
        Write-Host ""

        if (-not $XaiKey) {
            $XaiKey = Read-SecureInput -Prompt "Enter xAI API Key" -Secure $true -AllowEmpty $true
        }

        if ($XaiKey) {
            if (Set-UserSecret -ProjectDir $WinFormsProjectDir -Key "XAI:ApiKey" -Value $XaiKey) {
                $xaiConfigured = Test-UserSecret -ProjectDir $WinFormsProjectDir -Key "XAI:ApiKey"
                if (-not $xaiConfigured) {
                    Write-Host "  ⚠️  Warning: Could not verify secret was set" -ForegroundColor Yellow
                }
            }
        }
        else {
            Write-Host "  ⏭️  Skipping xAI configuration" -ForegroundColor Yellow
        }

        Write-Host ""

        # ============================================================
        # QuickBooks Configuration
        # ============================================================
        Write-Host "Step 3: QuickBooks OAuth Configuration" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "QuickBooks provides real company expense data." -ForegroundColor DarkGray
        Write-Host "Get OAuth credentials from: https://developer.intuit.com/app/developer/myapps" -ForegroundColor DarkGray
        Write-Host ""

        if (-not $QuickBooksClientId) {
            $QuickBooksClientId = Read-SecureInput -Prompt "Enter QuickBooks Client ID" -Secure $false -AllowEmpty $true
        }

        if ($QuickBooksClientId) {
            if (-not $QuickBooksClientSecret) {
                $QuickBooksClientSecret = Read-SecureInput -Prompt "Enter QuickBooks Client Secret" -Secure $true -AllowEmpty $true
            }

            if ($QuickBooksClientSecret) {
                $id_ok = Set-UserSecret -ProjectDir $WinFormsProjectDir -Key "Services:QuickBooks:OAuth:ClientId" -Value $QuickBooksClientId
                $secret_ok = Set-UserSecret -ProjectDir $WinFormsProjectDir -Key "Services:QuickBooks:OAuth:ClientSecret" -Value $QuickBooksClientSecret

                if ($id_ok -and $secret_ok) {
                    $quickBooksConfigured = $true
                }
            }
        }
        else {
            Write-Host "  ⏭️  Skipping QuickBooks configuration" -ForegroundColor Yellow
        }

        Write-Host ""
    }
}

# GitHub Actions Secrets (optional)
if ($EnableGitHubSecrets) {
    Write-Host "Step 4: GitHub Actions Secrets" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Use this to configure CI/CD pipeline secrets." -ForegroundColor DarkGray
    Write-Host ""

    $proceed = Read-Host "Configure GitHub Actions secrets? (y/n)"
    if ($proceed -eq 'y') {
        Write-Host ""
        Write-Host "Ensure you have GitHub CLI installed (gh)." -ForegroundColor DarkGray
        Write-Host "Run: pwsh ./scripts/setup/add-github-secrets.ps1" -ForegroundColor Cyan
        Write-Host ""
    }
}

# Summary
Show-Configuration-Summary -XaiConfigured $xaiConfigured -QuickBooksConfigured $quickBooksConfigured

# Verification
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Rebuild the application:" -ForegroundColor Yellow
Write-Host "   > dotnet build src/WileyWidget.WinForms/WileyWidget.WinForms.csproj" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. Run the application:" -ForegroundColor Yellow
Write-Host "   > dotnet run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj" -ForegroundColor Cyan
Write-Host ""
Write-Host "3. Check startup logs in logs/startup*.txt for configuration status" -ForegroundColor Yellow
Write-Host ""
Write-Host "4. Test services:" -ForegroundColor Yellow
if ($xaiConfigured) {
    Write-Host "   ✅ JARVIS Recommendation Service (Chat Panel)" -ForegroundColor Green
}
if ($quickBooksConfigured) {
    Write-Host "   ✅ Department Expense Service (Budget Panel)" -ForegroundColor Green
}
Write-Host ""

# Offer help resources
Write-Host "📚 Help & Documentation:" -ForegroundColor Cyan
Write-Host "   • User Secrets: docs/USER-SECRETS.md" -ForegroundColor White
Write-Host "   • xAI Configuration: docs/E2E_XAI_API_CONFIGURATION_FLOW.md" -ForegroundColor White
Write-Host "   • QuickBooks Setup: docs/USER-SECRETS.md (QuickBooks section)" -ForegroundColor White
Write-Host ""

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
