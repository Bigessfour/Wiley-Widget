# Load Syncfusion Licensing assembly from build output
$dllPath = Resolve-Path "src/WileyWidget.WinForms/bin/Debug/net10.0-windows10.0.26100.0/Syncfusion.Licensing.dll"

if (-not (Test-Path $dllPath)) {
    Write-Error "Syncfusion.Licensing.dll not found. Please build the project first."
    exit 1
}

Add-Type -Path $dllPath

# Key to test (from appsettings.json)
$key = "Ngo9BigBOggjHTQxAR8/V1JGaF5cXGpCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWH1ceXVSR2JcV0NyWkFWYEs="

Write-Host "Testing Key: $key"
Write-Host "DLL Path: $dllPath"

# Register
try {
    [Syncfusion.Licensing.SyncfusionLicenseProvider]::RegisterLicense($key)
    Write-Host "RegisterLicense() called successfully." -ForegroundColor Green
} catch {
    Write-Error "RegisterLicense() failed: $_"
    exit 1
}

# Validate for WinForms
$isValid = [Syncfusion.Licensing.SyncfusionLicenseProvider]::ValidateLicense([Syncfusion.Licensing.Platform]::WindowsForms)

if ($isValid) {
    Write-Host "✅ VALID for WindowsForms" -ForegroundColor Green
} else {
    Write-Host "❌ INVALID for WindowsForms" -ForegroundColor Red
}



