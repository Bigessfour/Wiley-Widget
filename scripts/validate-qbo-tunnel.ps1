#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Complete QuickBooks tunnel validation and startup script
.DESCRIPTION
    Validates and starts all components needed for QuickBooks webhook integration:
    1. Cloudflare tunnel service
    2. Local webhooks server
    3. End-to-end connectivity test
    4. Signature validation test
#>

param(
    [switch]$StartServices,
    [switch]$TestOnly,
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Configuration
$PublicUrl = "https://app.townofwiley.gov"
$LocalUrl = "http://localhost:5207"
$ServiceName = "cloudflared-wileywidget"
$WebhooksProject = "WileyWidget.Webhooks\WileyWidget.Webhooks.csproj"

Write-Output "`n=== QuickBooks Tunnel Validation ==="

# Step 1: Check Cloudflare service
Write-Output "`n[1/6] Checking Cloudflare tunnel service..."
try {
    $service = Get-Service -Name $ServiceName -ErrorAction Stop
    Write-Output "  ✓ Service '$ServiceName' found"
    Write-Output "    Status: $($service.Status)"
    Write-Output "    StartType: $($service.StartType)"

    if ($service.Status -ne 'Running') {
        if ($StartServices) {
            Write-Output "  → Starting service..."
            Start-Service -Name $ServiceName
            Start-Sleep -Seconds 3
            Write-Output "  ✓ Service started"
        }
        else {
            Write-Warning "  ⚠ Service not running. Use -StartServices to start it."
        }
    }
}
catch {
    Write-Warning "  ✗ Service check failed: $($_.Exception.Message)"
}

# Step 2: Check webhooks server process
Write-Output "`n[2/6] Checking webhooks server..."
$webhooksProcess = Get-Process -Name "WileyWidget.Webhooks" -ErrorAction SilentlyContinue

if ($webhooksProcess) {
    Write-Output "  ✓ Webhooks server running (PID: $($webhooksProcess.Id))"
    Write-Output "    Started: $($webhooksProcess.StartTime)"
}
else {
    Write-Output "  ✗ Webhooks server not running"

    if ($StartServices) {
        Write-Output "  → Starting webhooks server..."
        $projectPath = Join-Path $PSScriptRoot ".." $WebhooksProject

        if (Test-Path $projectPath) {
            # Start in background
            Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", $projectPath, "--no-build" -WindowStyle Hidden
            Write-Output "  → Waiting for server to start..."
            Start-Sleep -Seconds 5

            # Verify it started
            $webhooksProcess = Get-Process -Name "WileyWidget.Webhooks" -ErrorAction SilentlyContinue
            if ($webhooksProcess) {
                Write-Output "  ✓ Webhooks server started (PID: $($webhooksProcess.Id))"
            }
            else {
                Write-Warning "  ⚠ Server may still be starting..."
            }
        }
        else {
            Write-Warning "  ✗ Project not found: $projectPath"
        }
    }
    else {
        Write-Warning "  ⚠ Use -StartServices to start it"
    }
}

if ($TestOnly -and -not $webhooksProcess) {
    Write-Warning "`nCannot run tests - webhooks server not running"
    exit 1
}

# Step 3: Test local health endpoint
Write-Output "`n[3/6] Testing local endpoint ($LocalUrl/health)..."
try {
    $response = Invoke-WebRequest -Uri "$LocalUrl/health" -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
    Write-Output "  ✓ Local health check passed"
    Write-Output "    Status: $($response.StatusCode)"
    Write-Output "    Response: $($response.Content)"
}
catch {
    Write-Warning "  ✗ Local health check failed: $($_.Exception.Message)"
    if ($_.Exception.Message -like "*refused*" -or $_.Exception.Message -like "*connect*") {
        Write-Warning "    Server may not be running or listening on port 5207"
    }
}

# Step 4: Test public health endpoint
Write-Output "`n[4/6] Testing public endpoint ($PublicUrl/health)..."
try {
    $response = Invoke-WebRequest -Uri "$PublicUrl/health" -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
    Write-Output "  ✓ Public health check passed"
    Write-Output "    Status: $($response.StatusCode)"
    Write-Output "    Response: $($response.Content)"
}
catch {
    Write-Warning "  ✗ Public health check failed: $($_.Exception.Message)"
    if ($_.Exception.Message -like "*Could not resolve*") {
        Write-Warning "    DNS may not be configured yet"
    }
    elseif ($_.Exception.Message -like "*1033*") {
        Write-Warning "    Tunnel may not be routing correctly"
    }
}

# Step 5: Test webhook signature validation
Write-Output "`n[5/6] Testing webhook signature validation..."
$verifier = [Environment]::GetEnvironmentVariable('QBO_WEBHOOKS_VERIFIER', 'User')

if ($verifier) {
    Write-Output "  ✓ Verifier configured"

    # Create test payload
    $testPayload = @{
        eventNotifications = @(
            @{
                realmId         = "test-realm"
                dataChangeEvent = @{
                    entities = @(
                        @{
                            name      = "Customer"
                            id        = "123"
                            operation = "Create"
                        }
                    )
                }
            }
        )
    } | ConvertTo-Json -Depth 10

    # Calculate signature
    $hmac = [System.Security.Cryptography.HMACSHA256]::new([System.Text.Encoding]::UTF8.GetBytes($verifier))
    $hash = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($testPayload))
    $signature = [Convert]::ToBase64String($hash)

    Write-Output "  → Sending test webhook with signature..."

    try {
        $headers = @{
            'Content-Type'     = 'application/json'
            'intuit-signature' = $signature
        }

        $response = Invoke-WebRequest -Uri "$PublicUrl/qbo/webhooks" -Method Post -Body $testPayload -Headers $headers -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
        Write-Output "  ✓ Webhook accepted"
        Write-Output "    Status: $($response.StatusCode)"
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 401) {
            Write-Warning "  ⚠ Signature validation working (401 expected for test data)"
        }
        elseif ($statusCode -eq 200) {
            Write-Output "  ✓ Webhook processed successfully"
        }
        else {
            Write-Warning "  ✗ Webhook failed: $($_.Exception.Message)"
        }
    }
}
else {
    Write-Warning "  ⚠ QBO_WEBHOOKS_VERIFIER not set"
    Write-Output "    Set it with: `$env:QBO_WEBHOOKS_VERIFIER = 'your-verifier-token'"
}

# Step 6: Summary
Write-Output "`n[6/6] Summary"
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
Write-Output "  Cloudflared Service: $(if ($service -and $service.Status -eq 'Running') { '✓ Running' } else { '✗ Not Running' })"
Write-Output "  Webhooks Server: $(if (Get-Process -Name 'WileyWidget.Webhooks' -ErrorAction SilentlyContinue) { '✓ Running' } else { '✗ Not Running' })"
Write-Output "  Local Endpoint: $LocalUrl/health"
Write-Output "  Public Endpoint: $PublicUrl/health"
Write-Output "  Webhooks Endpoint: $PublicUrl/qbo/webhooks"

Write-Output "`n=== QuickBooks Integration URLs ==="
Write-Output "  Host Domain: townofwiley.gov"
Write-Output "  Redirect URI: http://localhost:8080/callback"
Write-Output "  Launch URL: https://app.townofwiley.gov/app/launch"
Write-Output "  Disconnect URL: https://app.townofwiley.gov/app/disconnect"
Write-Output "  Privacy Policy: https://app.townofwiley.gov/privacy"
Write-Output "  EULA: https://app.townofwiley.gov/eula"
Write-Output "  Webhooks Endpoint: https://app.townofwiley.gov/qbo/webhooks"

Write-Output "`nNext steps:"
Write-Output "  1. If services aren't running: .\validate-qbo-tunnel.ps1 -StartServices"
Write-Output "  2. Configure QuickBooks app settings with URLs above"
Write-Output "  3. Test OAuth: Run app and click 'Connect to QuickBooks'"
Write-Output "  4. Test webhooks: Use Intuit's webhook test tool"
Write-Output ""
