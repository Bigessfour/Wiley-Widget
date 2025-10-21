#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fix Cloudflared Windows service to load config.yml properly
.DESCRIPTION
    The service is running but not loading ingress rules from config.yml.
    This script recreates the service with correct parameters.
#>

#Requires -RunAsAdministrator

param(
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'

# Configuration
$ServiceName = "Cloudflared"
$CloudflaredExe = "C:\Program Files (x86)\cloudflared\cloudflared.exe"
$ConfigPath = "C:\ProgramData\cloudflared\config.yml"
$TunnelName = "wileywidget"

Write-Output "`n=== Fixing Cloudflared Service Configuration ===`n"

# Step 1: Verify files exist
Write-Output "[1/6] Verifying files..."
if (-not (Test-Path $CloudflaredExe)) {
    Write-Error "cloudflared.exe not found at: $CloudflaredExe"
}
Write-Output "  ✓ cloudflared.exe found"

if (-not (Test-Path $ConfigPath)) {
    Write-Error "config.yml not found at: $ConfigPath"
}
Write-Output "  ✓ config.yml found"

# Step 2: Display current config
Write-Output "`n[2/6] Current configuration:"
$config = Get-Content $ConfigPath -Raw
Write-Output $config

# Step 3: Stop and remove existing service
Write-Output "`n[3/6] Removing existing service..."
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    Write-Output "  → Stopping service..."
    if (-not $WhatIf) {
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2

        Write-Output "  → Uninstalling service..."
        & $CloudflaredExe service uninstall
        Start-Sleep -Seconds 2
    }
    else {
        Write-Output "  [WHATIF] Would stop and uninstall service"
    }
    Write-Output "  ✓ Service removed"
}
else {
    Write-Output "  ℹ No existing service found"
}

# Step 4: Install service with correct parameters
Write-Output "`n[4/6] Installing service with config path..."
$installCommand = "& `"$CloudflaredExe`" service install --config `"$ConfigPath`""

if (-not $WhatIf) {
    Write-Output "  → Running: $installCommand"
    & $CloudflaredExe service install --config $ConfigPath
    Start-Sleep -Seconds 2
    Write-Output "  ✓ Service installed"
}
else {
    Write-Output "  [WHATIF] Would run: $installCommand"
}

# Step 5: Verify service configuration
Write-Output "`n[5/6] Verifying service configuration..."
if (-not $WhatIf) {
    $service = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'"
    if ($service) {
        Write-Output "  Service Name: $($service.Name)"
        Write-Output "  Display Name: $($service.DisplayName)"
        Write-Output "  Start Mode: $($service.StartMode)"
        Write-Output "  State: $($service.State)"
        Write-Output "  Path: $($service.PathName)"

        if ($service.PathName -like "*--config*") {
            Write-Output "  ✓ Config path parameter found"
        }
        else {
            Write-Warning "  ⚠ Config path parameter NOT found in service command"
        }
    }
}

# Step 6: Start service and verify tunnel
Write-Output "`n[6/6] Starting service..."
if (-not $WhatIf) {
    Start-Service -Name $ServiceName
    Write-Output "  → Waiting for tunnel to connect..."
    Start-Sleep -Seconds 8

    # Check tunnel status
    $tunnelInfo = & $CloudflaredExe tunnel info ddd24f98-673d-43cb-b8a8-21a2329fffec 2>&1

    if ($tunnelInfo -like "*active connection*") {
        Write-Output "  ✓ Tunnel has active connections!"
    }
    else {
        Write-Warning "  ⚠ No active connections yet. Service output:"
        Write-Output $tunnelInfo
    }

    # Test public endpoint
    Write-Output "`n  → Testing public endpoint..."
    $ProgressPreference = 'SilentlyContinue'
    try {
        $response = Invoke-WebRequest -Uri "https://app.townofwiley.gov/health" -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
        Write-Output "  ✓ PUBLIC HEALTH CHECK PASSED!"
        Write-Output "    Status: $($response.StatusCode)"
        Write-Output "    Response: $($response.Content)"
    }
    catch {
        Write-Warning "  ✗ Public health check failed: $($_.Exception.Message)"
    }
}
else {
    Write-Output "  [WHATIF] Would start service and verify connections"
}

Write-Output "`n=== Summary ===`n"
Write-Output "The service has been reconfigured to load config.yml from:"
Write-Output "  $ConfigPath"
Write-Output ""
Write-Output "Next steps:"
Write-Output "  1. Verify: sc qc Cloudflared"
Write-Output "  2. Test: https://app.townofwiley.gov/health"
Write-Output "  3. Monitor: Get-EventLog -LogName System -Source 'Service Control Manager' -Newest 10"
Write-Output ""

if ($WhatIf) {
    Write-Output "[WHATIF MODE] No changes were made. Run without -WhatIf to apply fixes."
}
