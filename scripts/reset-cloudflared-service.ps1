#!/usr/bin/env pwsh
#Requires -RunAsAdministrator
# Complete service cleanup and reinstall with proper config

$ErrorActionPreference = 'Continue'

Write-Output "=== Cloudflared Service Complete Reset ==="

# Step 1: Kill all cloudflared processes
Write-Output "`n[1/5] Killing cloudflared processes..."
Get-Process cloudflared -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Output "  Killing PID $($_.Id)..."
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 3

# Step 2: Delete the service if it exists
Write-Output "`n[2/5] Removing service..."
$service = Get-Service Cloudflared -ErrorAction SilentlyContinue
if ($service) {
    sc.exe delete Cloudflared
    Start-Sleep -Seconds 2
    Write-Output "  ✓ Service deleted"
} else {
    Write-Output "  No service found"
}

# Step 3: Verify config file
Write-Output "`n[3/5] Verifying configuration..."
$configPath = "C:\ProgramData\cloudflared\config.yml"
if (Test-Path $configPath) {
    Write-Output "  ✓ Config file exists"
    Get-Content $configPath | Write-Output
} else {
    Write-Error "Config file not found: $configPath"
    exit 1
}

# Step 4: Install service with config parameter
Write-Output "`n[4/5] Installing service..."
$cloudflared = "C:\Program Files (x86)\cloudflared\cloudflared.exe"
& $cloudflared service install --config $configPath

Start-Sleep -Seconds 2

# Verify installation
$newService = Get-Service Cloudflared -ErrorAction SilentlyContinue
if ($newService) {
    Write-Output "  ✓ Service created"
    Write-Output "  Start Type: $($newService.StartType)"
} else {
    Write-Error "Service installation failed"
    exit 1
}

# Step 5: Configure and start
Write-Output "`n[5/5] Starting service..."
sc.exe config Cloudflared start= auto
Start-Sleep -Seconds 1
sc.exe start Cloudflared

Write-Output "`nWaiting 10 seconds for tunnel connections..."
Start-Sleep -Seconds 10

# Verify
Write-Output "`nService Status:"
Get-Service Cloudflared | Format-List Name,Status,StartType

Write-Output "`nTunnel Status:"
& $cloudflared tunnel info ddd24f98-673d-43cb-b8a8-21a2329fffec

Write-Output "`nTesting Public Endpoint:"
try {
    $response = Invoke-WebRequest -Uri "https://app.townofwiley.gov/health" -UseBasicParsing -TimeoutSec 10
    Write-Output "✓ SUCCESS! Status: $($response.StatusCode)"
    Write-Output "  Response: $($response.Content)"
} catch {
    Write-Warning "✗ Failed: $($_.Exception.Message)"
    Write-Output "`nTroubleshooting:"
    Write-Output "  1. Check service: Get-Service Cloudflared"
    Write-Output "  2. View logs: Get-EventLog -LogName Application -Source cloudflared -Newest 10"
    Write-Output "  3. Manual test: & '$cloudflared' tunnel run wileywidget"
}

Write-Output "`n=== Complete ==="
