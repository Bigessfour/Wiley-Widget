# Test script to verify .env variables are accessible to Trunk CLI
# This script demonstrates that environment variables from .env are loaded

param(
    [switch]$ShowAll,
    [switch]$Quiet,
    [switch]$CompareWithEnvFile
)

if (-not $Quiet) {
    Write-Host "🧪 Testing .env Variable Access for Trunk CLI" -ForegroundColor Magenta
    Write-Host "=============================================" -ForegroundColor Magenta
}

# Test key environment variables that should be loaded from .env
$testVars = @(
    "AZURE_SUBSCRIPTION_ID",
    "AZURE_TENANT_ID",
    "QBO_CLIENT_ID",
    "QBO_CLIENT_SECRET",
    "QBO_REDIRECT_URI",
    "QBO_ENVIRONMENT",
    "KEY_VAULT_NAME",
    "ASPNETCORE_ENVIRONMENT",
    "ASPNETCORE_URLS"
)

$foundVars = @()
$existingVars = @()
$missingVars = @()

# Load .env file content for comparison if requested
$envFileContent = @{}
if ($CompareWithEnvFile -and (Test-Path ".env")) {
    try {
        $content = Get-Content ".env" -ErrorAction Stop
        foreach ($line in $content) {
            if ($line -match '^([^=]+)=(.*)$' -and -not $line.Trim().StartsWith('#')) {
                $key = $matches[1].Trim()
                $value = $matches[2].Trim()
                if ($value.StartsWith('"') -and $value.EndsWith('"')) {
                    $value = $value.Substring(1, $value.Length - 2)
                } elseif ($value.StartsWith("'") -and $value.EndsWith("'")) {
                    $value = $value.Substring(1, $value.Length - 2)
                }
                $envFileContent[$key] = $value
            }
        }
    } catch {
        if (-not $Quiet) {
            Write-Host "⚠️  Could not read .env file for comparison" -ForegroundColor Yellow
        }
    }
}

foreach ($var in $testVars) {
    $envVar = Get-Item "env:$var" -ErrorAction SilentlyContinue
    if ($envVar) {
        $foundVars += $var
        $existingVars += $var

        if (-not $Quiet) {
            $displayValue = if ($ShowAll) {
                $envVar.Value
            } elseif ($envVar.Value.Length -gt 20) {
                "$($envVar.Value.Substring(0, 20))..."
            } else {
                $envVar.Value
            }

            # Check if value matches .env file
            $status = "✅"
            $statusColor = "Green"
            if ($CompareWithEnvFile -and $envFileContent.ContainsKey($var)) {
                if ($envVar.Value -eq $envFileContent[$var]) {
                    $status = "✅ (matches .env)"
                } else {
                    $status = "⚠️  (differs from .env)"
                    $statusColor = "Yellow"
                }
            }

            Write-Host "   $status $var = $displayValue" -ForegroundColor $statusColor
        }
    } else {
        $missingVars += $var
        if (-not $Quiet) {
            Write-Host "   ❌ $var (not set)" -ForegroundColor Red
        }
    }
}

# Test trunk command availability
$trunkAvailable = $false
$trunkVersion = ""
try {
    $trunkVersion = & trunk --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        $trunkAvailable = $true
    }
} catch {
    $trunkAvailable = $false
}

if (-not $Quiet) {
    Write-Host ""
    Write-Host "📊 Results:" -ForegroundColor Cyan
    Write-Host "   Environment Variables: $($foundVars.Count) found, $($missingVars.Count) missing" -ForegroundColor White
    Write-Host "   Trunk CLI: $(if ($trunkAvailable) { "✅ Available ($trunkVersion)" } else { '❌ Not available' })" -ForegroundColor $(if ($trunkAvailable) { 'Green' } else { 'Red' })

    if ($foundVars.Count -gt 0) {
        Write-Host ""
        Write-Host "🎯 Environment variables are accessible!" -ForegroundColor Green
        Write-Host "💡 Trunk CLI can access these environment variables" -ForegroundColor Cyan
    }

    if ($missingVars.Count -gt 0) {
        Write-Host ""
        Write-Host "⚠️  Missing variables (not set in environment):" -ForegroundColor Yellow
        foreach ($var in $missingVars) {
            Write-Host "   - $var" -ForegroundColor White
            if ($CompareWithEnvFile -and $envFileContent.ContainsKey($var)) {
                Write-Host "     Expected from .env: $($envFileContent[$var])" -ForegroundColor Gray
            }
        }
        Write-Host ""
        Write-Host "💡 To load missing variables from .env, run:" -ForegroundColor Cyan
        Write-Host "   .\scripts\load-env-for-trunk.ps1 -OverrideExisting" -ForegroundColor White
    }

    if ($CompareWithEnvFile) {
        Write-Host ""
        Write-Host "📄 .env file comparison:" -ForegroundColor Blue
        Write-Host "   $(if ($envFileContent.Count -gt 0) { '✅ .env file found and parsed' } else { '❌ .env file not found or empty' })" -ForegroundColor $(if ($envFileContent.Count -gt 0) { 'Green' } else { 'Red' })
    }
}

# Return success if we have variables and trunk is available
return ($foundVars.Count -gt 0 -and $trunkAvailable)
