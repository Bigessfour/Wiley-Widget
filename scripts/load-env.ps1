#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Secure Environment Variable Loader for WileyWidget (PowerShell)
    This script loads environment variables from .env file securely

.DESCRIPTION
    Loads environment variables from a .env-style file with support for:
    - Basic KEY=VALUE pairs
    - Azure Key Vault references (@AzureKeyVault(...))
    - Font configuration for multi-machine development
    - WPF rendering settings

.PARAMETER EnvFile
    Path to the .env file to load. Defaults to .env in the script's parent directory.

.PARAMETER Force
    Force reload even if variables are already set.

.EXAMPLE
    .\load-env.ps1

.EXAMPLE
    .\load-env.ps1 -EnvFile "C:\path\to\.env"

.NOTES
    Requires PowerShell 7.0+ for Azure Key Vault support
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$EnvFile,

    [Parameter(Mandatory = $false)]
    [switch]$Force
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Determine .env file path
if (-not $EnvFile) {
    $scriptDir = Split-Path -Parent $PSCommandPath
    $parentDir = Split-Path -Parent $scriptDir
    $EnvFile = Join-Path $parentDir ".env"
}

# Check if .env file exists
if (-not (Test-Path $EnvFile)) {
    Write-Host "❌ .env file not found at: $EnvFile" -ForegroundColor Red
    Write-Host "Create a .env file with your configuration variables."
    exit 1
}

Write-Host "🔐 Loading environment variables from .env file..." -ForegroundColor Green

$loadedCount = 0
$errorCount = 0

try {
    $lines = Get-Content $EnvFile -Encoding UTF8
    $lineNumber = 0

    foreach ($line in $lines) {
        $lineNumber++
        $line = $line.Trim()

        # Skip comments and empty lines
        if ([string]::IsNullOrEmpty($line) -or $line.StartsWith('#')) {
            continue
        }

        # Parse KEY=VALUE
        if ($line -match '^([^=]+)=(.*)$') {
            $key = $matches[1].Trim()
            $value = $matches[2].Trim()

            # Remove quotes if present
            if (($value.StartsWith('"') -and $value.EndsWith('"')) -or
                ($value.StartsWith("'") -and $value.EndsWith("'"))) {
                $value = $value.Substring(1, $value.Length - 2)
            }

            # Check if already set and not forcing
            if (-not $Force -and (Test-Path "env:$key")) {
                Write-Host "  ⏭️  $key (already set, use -Force to override)" -ForegroundColor Yellow
                continue
            }

            # Handle Azure Key Vault references
            if ($value -match '^@AzureKeyVault\((.+)\)$') {
                $kvReference = $matches[1]
                Write-Host "  🔑 Resolving Key Vault reference: $kvReference" -ForegroundColor Blue

                try {
                    # Check if Azure modules are available
                    if (-not (Get-Module -ListAvailable -Name Az.KeyVault)) {
                        throw "Azure PowerShell modules not available. Install with: Install-Module -Name Az -Scope CurrentUser"
                    }

                    Import-Module Az.KeyVault -ErrorAction Stop

                    # Extract vault URL and secret name
                    if ($kvReference -match '(.+)/secrets/(.+)') {
                        $vaultUrl = $matches[1]
                        $secretName = $matches[2]

                        # Get Azure context
                        $context = Get-AzContext
                        if (-not $context) {
                            throw "Not logged in to Azure. Run Connect-AzAccount first."
                        }

                        $secret = Get-AzKeyVaultSecret -VaultName ($vaultUrl -replace 'https://(.+)\.vault\.azure\.net', '$1') -Name $secretName
                        if ($secret -and $secret.SecretValueText) {
                            [Environment]::SetEnvironmentVariable($key, $secret.SecretValueText, "Process")
                            $loadedCount++
                            Write-Host "  ✅ $key (from Key Vault)" -ForegroundColor Green
                        } else {
                            $errorCount++
                            Write-Host "  ❌ $key`: Empty secret from Key Vault" -ForegroundColor Red
                        }
                    } else {
                        $errorCount++
                        Write-Host "  ❌ $key`: Invalid Key Vault reference format" -ForegroundColor Red
                    }
                } catch {
                    $errorCount++
                    Write-Host "  ❌ $key`: Key Vault resolution failed: $($_.Exception.Message)" -ForegroundColor Red
                }
            } else {
                # Regular environment variable
                try {
                    [Environment]::SetEnvironmentVariable($key, $value, "Process")
                    $loadedCount++
                    Write-Host "  ✅ $key" -ForegroundColor Green
                } catch {
                    $errorCount++
                    Write-Host "  ❌ $key`: $($_.Exception.Message)" -ForegroundColor Red
                }
            }
        } else {
            Write-Host "  ⚠️  Skipping invalid line $lineNumber`: $line" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "❌ Error reading .env file: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "Loaded $loadedCount environment variables" -ForegroundColor Green
if ($errorCount -gt 0) {
    Write-Host "Failed to load $errorCount variables" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Environment setup complete" -ForegroundColor Green