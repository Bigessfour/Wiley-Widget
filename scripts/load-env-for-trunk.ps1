# Load .env file for Trunk CLI and terminal environment
# This script loads environment variables from .env file for use with Trunk CLI

param(
    [string]$EnvFilePath = ".env",
    [switch]$OverrideExisting,
    [switch]$Quiet
)

function Load-EnvFile {
    param(
        [string]$FilePath,
        [switch]$OverrideExisting,
        [switch]$Quiet
    )

    if (-not (Test-Path $FilePath)) {
        if (-not $Quiet) {
            Write-Warning "Environment file not found: $FilePath"
            Write-Host "💡 Create a .env file in the project root with your environment variables" -ForegroundColor Yellow
        }
        return $false
    }

    if (-not $Quiet) {
        Write-Host "🔧 Loading environment variables from: $FilePath" -ForegroundColor Magenta
    }

    $loadedCount = 0

    try {
        $content = Get-Content $FilePath -ErrorAction Stop

        foreach ($line in $content) {
            # Skip comments and empty lines
            if ($line.Trim().StartsWith('#') -or [string]::IsNullOrWhiteSpace($line)) {
                continue
            }

            # Parse KEY=VALUE pairs
            if ($line -match '^([^=]+)=(.*)$') {
                $key = $matches[1].Trim()
                $value = $matches[2].Trim()

                # Remove quotes if present
                if ($value.StartsWith('"') -and $value.EndsWith('"')) {
                    $value = $value.Substring(1, $value.Length - 2)
                } elseif ($value.StartsWith("'") -and $value.EndsWith("'")) {
                    $value = $value.Substring(1, $value.Length - 2)
                }

                # Set environment variable
                if ($OverrideExisting -or -not (Test-Path "env:$key")) {
                    Set-Item -Path "env:$key" -Value $value -ErrorAction Stop
                    $loadedCount++

                    if (-not $Quiet) {
                        Write-Host "   ✅ $key" -ForegroundColor Green
                    }
                } else {
                    if (-not $Quiet) {
                        Write-Host "   ⏭️  $key (already exists, skipped)" -ForegroundColor Yellow
                    }
                }
            }
        }

        if (-not $Quiet) {
            Write-Host "✅ Loaded $loadedCount environment variables" -ForegroundColor Green
        }

        return $true
    }
    catch {
        if (-not $Quiet) {
            Write-Error "❌ Failed to load environment file: $_"
        }
        return $false
    }
}

function Set-TrunkEnvironment {
    param([switch]$Quiet)

    # Set Trunk-specific environment variables for .env integration
    $env:TRUNK_DOTENV_ENABLED = "true"
    $env:TRUNK_ENV_FILE_PATH = ".env"

    if (-not $Quiet) {
        Write-Host "🔧 Trunk environment configured:" -ForegroundColor Magenta
        Write-Host "   ✅ Dotenv support: Enabled" -ForegroundColor Green
        Write-Host "   📄 Env file path: .env" -ForegroundColor Cyan
    }
}

# Main execution
if (-not $Quiet) {
    Write-Host "🌍 Trunk CLI Environment Loader" -ForegroundColor Magenta
    Write-Host "===============================" -ForegroundColor Magenta
}

# Load environment variables from .env file
$success = Load-EnvFile -FilePath $EnvFilePath -OverrideExisting:$OverrideExisting -Quiet:$Quiet

if ($success) {
    # Configure Trunk environment
    Set-TrunkEnvironment -Quiet:$Quiet

    if (-not $Quiet) {
        Write-Host ""
        Write-Host "🎯 Environment ready for Trunk CLI!" -ForegroundColor Green
        Write-Host "💡 You can now run trunk commands with access to your .env variables" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "📝 Example commands:" -ForegroundColor White
        Write-Host "   trunk check --all --ci" -ForegroundColor Gray
        Write-Host "   trunk check --scope security" -ForegroundColor Gray
        Write-Host "   trunk fmt --ci" -ForegroundColor Gray
    }
} else {
    if (-not $Quiet) {
        Write-Host ""
        Write-Host "⚠️  Environment loading failed, but you can still use trunk commands" -ForegroundColor Yellow
    }
}

# Return success status
$success
