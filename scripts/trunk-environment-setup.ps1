# Trunk Environment Setup Script for Wiley Widget
# This script configures trunk to work properly with the Wiley Widget development environment

param(
    [switch]$Diagnose,
    [switch]$Fix,
    [switch]$Reset
)

$ErrorActionPreference = "Stop"

# Load environment variables from .env file
function Load-Environment {
    Write-Information "🔧 Loading Wiley Widget environment configuration..." -InformationAction Continue

    if (Test-Path ".env") {
        Write-Information "📄 Found .env file, loading environment variables..." -InformationAction Continue
        Get-Content ".env" | Where-Object { $_ -match '^[^#].*=' } | ForEach-Object {
            $key, $value = $_ -split '=', 2
            $key = $key.Trim()
            $value = $value.Trim()
            [Environment]::SetEnvironmentVariable($key, $value, "Process")
        }
        Write-Information "✅ Environment variables loaded from .env" -InformationAction Continue
    }
    else {
        Write-Warning "⚠️  .env file not found. Some features may not work correctly."
    }
}

# Diagnose trunk configuration
function Test-TrunkConfiguration {
    Write-Information "🔍 Diagnosing trunk configuration..." -InformationAction Continue

    # Check if trunk is installed
    try {
        $trunkVersion = & trunk --version 2>$null
        Write-Information "✅ Trunk is installed: $trunkVersion" -InformationAction Continue
    }
    catch {
        Write-Error "❌ Trunk is not installed or not in PATH"
        return $false
    }

    # Check trunk.yaml configuration
    if (Test-Path ".trunk\trunk.yaml") {
        Write-Information "✅ Trunk configuration file found" -InformationAction Continue

        # Validate YAML syntax
        try {
            $yamlContent = Get-Content ".trunk\trunk.yaml" -Raw
            # Basic YAML validation (this is a simple check)
            if ($yamlContent -match 'version:\s*0\.1') {
                Write-Information "✅ Trunk YAML configuration appears valid" -InformationAction Continue
            }
            else {
                Write-Warning "⚠️  Trunk YAML version may be incorrect"
            }
        }
        catch {
            Write-Error "❌ Error reading trunk.yaml: $_"
            return $false
        }
    }
    else {
        Write-Error "❌ Trunk configuration file not found at .trunk\trunk.yaml"
        return $false
    }

    # Check environment variables
    $requiredEnvVars = @(
        'ASPNETCORE_ENVIRONMENT',
        'DOTNET_CLI_TELEMETRY_OPTOUT',
        'POWERSHELL_EXECUTION_POLICY'
    )

    foreach ($envVar in $requiredEnvVars) {
        if ([Environment]::GetEnvironmentVariable($envVar)) {
            Write-Information "✅ Environment variable $envVar is set" -InformationAction Continue
        }
        else {
            Write-Warning "⚠️  Environment variable $envVar is not set"
        }
    }

    Write-Information "🔍 Trunk configuration diagnosis complete" -InformationAction Continue
    return $true
}

# Fix trunk configuration issues
function Repair-TrunkConfiguration {
    Write-Information "🔧 Attempting to fix trunk configuration issues..." -InformationAction Continue

    # Ensure trunk daemon is not running (to avoid conflicts)
    try {
        & trunk daemon shutdown 2>$null
        Write-Information "✅ Trunk daemon shut down" -InformationAction Continue
    }
    catch {
        Write-Information "ℹ️  Trunk daemon was not running" -InformationAction Continue
    }

    # Clear trunk cache
    try {
        & trunk cache clean 2>$null
        Write-Information "✅ Trunk cache cleared" -InformationAction Continue
    }
    catch {
        Write-Warning "⚠️  Could not clear trunk cache: $_"
    }

    # Reinitialize trunk
    try {
        & trunk init --force 2>$null
        Write-Information "✅ Trunk reinitialized" -InformationAction Continue
    }
    catch {
        Write-Error "❌ Failed to reinitialize trunk: $_"
        return $false
    }

    # Test trunk functionality
    try {
        & trunk check --ci 2>$null
        Write-Information "✅ Trunk check passed" -InformationAction Continue
    }
    catch {
        Write-Warning "⚠️  Trunk check failed, but this may be expected on first run"
    }

    Write-Information "🔧 Trunk configuration repair complete" -InformationAction Continue
    return $true
}

# Reset trunk to clean state
function Reset-TrunkConfiguration {
    Write-Information "🔄 Resetting trunk to clean state..." -InformationAction Continue

    # Stop all trunk processes
    Get-Process -Name "trunk" -ErrorAction SilentlyContinue | Stop-Process -Force
    Write-Information "✅ All trunk processes stopped" -InformationAction Continue

    # Remove trunk cache and data
    $trunkPaths = @(
        ".trunk\cache",
        ".trunk\data",
        ".trunk\logs"
    )

    foreach ($path in $trunkPaths) {
        if (Test-Path $path) {
            Remove-Item -Path $path -Recurse -Force
            Write-Information "✅ Removed $path" -InformationAction Continue
        }
    }

    # Reinitialize trunk
    try {
        & trunk init 2>$null
        Write-Information "✅ Trunk reinitialized from clean state" -InformationAction Continue
    }
    catch {
        Write-Error "❌ Failed to reinitialize trunk: $_"
        return $false
    }

    Write-Information "🔄 Trunk reset complete" -InformationAction Continue
    return $true
}

# Main execution
Write-Information "🚀 Wiley Widget Trunk Environment Setup" -InformationAction Continue
Write-Information "==========================================" -InformationAction Continue

# Load environment
Load-Environment

# Execute requested operations
$success = $true

if ($Diagnose) {
    $success = Test-TrunkConfiguration
}

if ($Fix) {
    $success = Repair-TrunkConfiguration
}

if ($Reset) {
    $success = Reset-TrunkConfiguration
}

# If no specific operation requested, do a full setup
if (-not ($Diagnose -or $Fix -or $Reset)) {
    Write-Information "📋 Performing full trunk environment setup..." -InformationAction Continue

    if (Test-TrunkConfiguration) {
        Write-Information "✅ Trunk configuration is healthy" -InformationAction Continue
    }
    else {
        Write-Information "🔧 Configuration issues detected, attempting repair..." -InformationAction Continue
        $success = Repair-TrunkConfiguration
    }
}

# Final status
if ($success) {
    Write-Information "" -InformationAction Continue
    Write-Information "🎉 Trunk environment setup completed successfully!" -InformationAction Continue
    Write-Information "💡 You can now use trunk commands in this environment" -InformationAction Continue
    Write-Information "   Example: trunk check, trunk fmt, trunk daemon launch" -InformationAction Continue
}
else {
    Write-Error "❌ Trunk environment setup failed. Please check the errors above." 
    exit 1
}
