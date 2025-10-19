#!/usr/bin/env pwsh
# Quick fix: Reinstall Cloudflared service with proper config parameter
# Run as Administrator

$cloudflared = "C:\Program Files (x86)\cloudflared\cloudflared.exe"
$config = "C:\ProgramData\cloudflared\config.yml"

Write-Output "Stopping and removing old service..."
sc.exe stop Cloudflared 2>&1 | Out-Null
Start-Sleep -Seconds 2
& $cloudflared service uninstall 2>&1 | Out-Null

Write-Output "Installing service with config parameter..."
& $cloudflared service install --config $config

Write-Output "Setting to automatic start..."
sc.exe config Cloudflared start= auto | Out-Null

Write-Output "Starting service..."
sc.exe start Cloudflared

Write-Output "`nWaiting 10 seconds for tunnel to connect..."
Start-Sleep -Seconds 10

Write-Output "`nChecking tunnel status..."
& $cloudflared tunnel info ddd24f98-673d-43cb-b8a8-21a2329fffec

Write-Output "`nTesting public endpoint..."
try {
    $response = Invoke-WebRequest -Uri "https://app.townofwiley.gov/health" -UseBasicParsing -TimeoutSec 10
    Write-Output "✓ SUCCESS! Status: $($response.StatusCode), Response: $($response.Content)"
} catch {
    Write-Warning "✗ Failed: $($_.Exception.Message)"
}
