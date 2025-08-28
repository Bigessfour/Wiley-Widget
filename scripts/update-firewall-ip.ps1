# Dynamic IP Firewall Update Script
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory=$true)]
    [string]$SqlServer,

    [Parameter(Mandatory=$false)]
    [string]$NewIP,

    [Parameter(Mandatory=$false)]
    [switch]$AutoDetectIP,

    [Parameter(Mandatory=$false)]
    [int]$KeepLastNDays = 7
)

Write-Host "🔄 WileyWidget Dynamic IP Firewall Update" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Function to get current public IP
function Get-CurrentPublicIP {
    try {
        $ip = Invoke-RestMethod -Uri "https://api.ipify.org"
        return $ip
    }
    catch {
        Write-Host "❌ Failed to get current IP: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

# Determine IP to use
if ($NewIP) {
    $targetIP = $NewIP
    Write-Host "📍 Using provided IP: $targetIP" -ForegroundColor Yellow
} elseif ($AutoDetectIP) {
    Write-Host "🔍 Auto-detecting current public IP..." -ForegroundColor Yellow
    $targetIP = Get-CurrentPublicIP
    if ($targetIP) {
        Write-Host "📍 Detected IP: $targetIP" -ForegroundColor Green
    } else {
        Write-Host "❌ Could not detect current IP" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "❌ Please provide -NewIP or use -AutoDetectIP" -ForegroundColor Red
    exit 1
}

# Generate rule name
$ruleName = "CurrentIP-$(Get-Date -Format 'yyyyMMdd-HHmm')"
$cutoffDate = (Get-Date).AddDays(-$KeepLastNDays)

Write-Host "🧹 Cleaning up old CurrentIP rules (keeping last $KeepLastNDays days)..." -ForegroundColor Yellow

# Get existing CurrentIP rules
try {
    $existingRules = az sql server firewall-rule list `
        --resource-group $ResourceGroup `
        --server $SqlServer `
        --query "[?contains(name, 'CurrentIP-')].{Name:name, StartIP:startIpAddress}" `
        -o json | ConvertFrom-Json
} catch {
    Write-Host "⚠️  Could not retrieve existing rules" -ForegroundColor Yellow
    $existingRules = @()
}

# Remove old rules
$removedCount = 0
foreach ($rule in $existingRules) {
    try {
        # Extract date from rule name (format: CurrentIP-yyyyMMdd-HHmm)
        if ($rule.Name -match "CurrentIP-(\d{8})") {
            $ruleDate = [DateTime]::ParseExact($matches[1], "yyyyMMdd", $null)
            if ($ruleDate -lt $cutoffDate) {
                az sql server firewall-rule delete `
                    --resource-group $ResourceGroup `
                    --server $SqlServer `
                    --name $rule.Name `
                    --yes 2>$null

                if ($LASTEXITCODE -eq 0) {
                    Write-Host "  • Removed old rule: $($rule.Name) ($($rule.StartIP))" -ForegroundColor Gray
                    $removedCount++
                }
            }
        }
    } catch {
        Write-Host "  • Could not remove rule: $($rule.Name)" -ForegroundColor Yellow
    }
}

if ($removedCount -gt 0) {
    Write-Host "✅ Cleaned up $removedCount old rules" -ForegroundColor Green
} else {
    Write-Host "ℹ️  No old rules to clean up" -ForegroundColor Blue
}

# Check if current IP is already allowed
$currentRules = az sql server firewall-rule list `
    --resource-group $ResourceGroup `
    --server $SqlServer `
    --query "[?startIpAddress=='$targetIP' && endIpAddress=='$targetIP']" `
    -o json 2>$null | ConvertFrom-Json

if ($currentRules -and $currentRules.Count -gt 0) {
    Write-Host "ℹ️  IP $targetIP is already allowed (Rule: $($currentRules[0].name))" -ForegroundColor Blue
} else {
    # Add new rule
    Write-Host "➕ Adding new firewall rule for IP: $targetIP" -ForegroundColor Yellow

    try {
        az sql server firewall-rule create `
            --resource-group $ResourceGroup `
            --server $SqlServer `
            --name $ruleName `
            --start-ip-address $targetIP `
            --end-ip-address $targetIP 2>$null

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Successfully added firewall rule: $ruleName" -ForegroundColor Green
        } else {
            Write-Host "❌ Failed to add firewall rule" -ForegroundColor Red
            exit 1
        }
    } catch {
        Write-Host "❌ Error adding firewall rule: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# Display current firewall rules
Write-Host "`n📋 Current Firewall Rules:" -ForegroundColor Cyan
try {
    az sql server firewall-rule list `
        --resource-group $ResourceGroup `
        --server $SqlServer `
        --output table
} catch {
    Write-Host "⚠️  Could not list firewall rules" -ForegroundColor Yellow
}

# Test connectivity
Write-Host "`n🧪 Testing Database Connectivity..." -ForegroundColor Yellow
$connectionString = "Server=tcp:$SqlServer.database.windows.net,1433;Database=WileyWidgetDb;User ID=dummy;Password=dummy;Encrypt=True;TrustServerCertificate=False;Connection Timeout=10;"

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = $connectionString
    $connection.Open()
    Write-Host "✅ Database connectivity test passed" -ForegroundColor Green
    $connection.Close()
} catch {
    Write-Host "⚠️  Connectivity test failed (expected with dummy credentials): $($_.Exception.Message)" -ForegroundColor Yellow
}

# Log the update
$logEntry = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | IP: $targetIP | Rule: $ruleName | Cleaned: $removedCount rules"
$logFile = "firewall-updates.log"

if (Test-Path $logFile) {
    Add-Content $logFile $logEntry
} else {
    $logEntry | Out-File $logFile -Encoding UTF8
}

Write-Host "`n✅ Firewall update completed successfully!" -ForegroundColor Green
Write-Host "📝 Summary:" -ForegroundColor Cyan
Write-Host "   • Target IP: $targetIP" -ForegroundColor White
Write-Host "   • Rule Name: $ruleName" -ForegroundColor White
Write-Host "   • Old Rules Cleaned: $removedCount" -ForegroundColor White
Write-Host "   • Log Updated: $logFile" -ForegroundColor White

Write-Host "`n💡 Usage Examples:" -ForegroundColor Yellow
Write-Host "   • Auto-detect IP: .\scripts\update-firewall-ip.ps1 -ResourceGroup 'rg' -SqlServer 'server' -AutoDetectIP" -ForegroundColor White
Write-Host "   • Specific IP: .\scripts\update-firewall-ip.ps1 -ResourceGroup 'rg' -SqlServer 'server' -NewIP '1.2.3.4'" -ForegroundColor White
Write-Host "   • Keep more days: .\scripts\update-firewall-ip.ps1 -ResourceGroup 'rg' -SqlServer 'server' -AutoDetectIP -KeepLastNDays 14" -ForegroundColor White
