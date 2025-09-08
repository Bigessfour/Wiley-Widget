# Profile Syncfusion License Initialization Script
# Runs during PowerShell profile loading to ensure Syncfusion license is properly configured

Write-Information "🔧 Initializing Syncfusion license configuration..." -InformationAction Continue

# Get script directory and project root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

# Check if Python is available
$pythonCmd = Get-Command python -ErrorAction SilentlyContinue
if (-not $pythonCmd) {
    $pythonCmd = Get-Command python3 -ErrorAction SilentlyContinue
}

if (-not $pythonCmd) {
    Write-Warning "Python not found. Cannot run Syncfusion environment propagation script."
    Write-Information "To install Python: https://www.python.org/downloads/" -InformationAction Continue
    return
}

# Path to the Syncfusion environment propagation script
$syncfusionScript = Join-Path $scriptDir "syncfusion_env_propagate.py"

if (-not (Test-Path $syncfusionScript)) {
    Write-Warning "Syncfusion environment propagation script not found: $syncfusionScript"
    return
}

# Run the Python script to propagate Syncfusion license key
Write-Information "📄 Running Syncfusion environment propagation..." -InformationAction Continue

try {
    # Change to project root directory
    Push-Location $projectRoot

    # Run the Python script
    $result = & $pythonCmd.Path $syncfusionScript 2>&1

    # Check exit code
    if ($LASTEXITCODE -eq 0) {
        Write-Information "✅ Syncfusion license configuration updated successfully" -InformationAction Continue
    }
    elseif ($LASTEXITCODE -eq 2) {
        Write-Warning "⚠️ Syncfusion license key not found in machine/user environment"
        Write-Information "Set SYNCFUSION_LICENSE_KEY environment variable or run Azure Key Vault resolution" -InformationAction Continue
    }
    elseif ($LASTEXITCODE -eq 3) {
        Write-Warning "⚠️ Syncfusion license key appears to be a placeholder"
        Write-Information "Update with a valid Syncfusion license key" -InformationAction Continue
    }
    else {
        Write-Warning "❌ Syncfusion environment propagation failed (Exit code: $LASTEXITCODE)"
        Write-Information "Output: $result" -InformationAction Continue
    }
}
catch {
    Write-Warning "❌ Error running Syncfusion environment propagation: $($_.Exception.Message)"
}
finally {
    # Restore original location
    Pop-Location
}

Write-Information "🔧 Syncfusion license initialization complete" -InformationAction Continue
