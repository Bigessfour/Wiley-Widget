# Liberal Firewall Setup Script for Dynamic IP Addresses
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory=$true)]
    [string]$SqlServer,

    [Parameter(Mandatory=$false)]
    [switch]$AllowAllAzureServices,

    [Parameter(Mandatory=$false)]
    [switch]$SetupCommonISPRanges,

    [Parameter(Mandatory=$false)]
    [string]$CurrentIP,

    [Parameter(Mandatory=$false)]
    [switch]$EnableMonitoring
)

Write-Host "🔥 WileyWidget Liberal Firewall Setup for Dynamic IPs" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

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

# Allow Azure Services (Most Important for Dynamic IPs)
if ($AllowAllAzureServices) {
    Write-Host "🌐 Allowing access from all Azure services..." -ForegroundColor Yellow
    try {
        az sql server firewall-rule create `
            --resource-group $ResourceGroup `
            --server $SqlServer `
            --name "AllowAllAzureServices" `
            --start-ip-address "0.0.0.0" `
            --end-ip-address "0.0.0.0" 2>$null

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Azure services access enabled" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Azure services rule may already exist" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "⚠️  Azure services rule may already exist or access denied" -ForegroundColor Yellow
    }
}

# Add current IP if provided or auto-detect
if ($CurrentIP) {
    $ipToAdd = $CurrentIP
} else {
    Write-Host "🔍 Auto-detecting current public IP..." -ForegroundColor Yellow
    $ipToAdd = Get-CurrentPublicIP
}

if ($ipToAdd) {
    $ruleName = "CurrentIP-$(Get-Date -Format 'yyyyMMdd-HHmm')"
    Write-Host "📍 Adding current IP: $ipToAdd (Rule: $ruleName)" -ForegroundColor Yellow

    try {
        az sql server firewall-rule create `
            --resource-group $ResourceGroup `
            --server $SqlServer `
            --name $ruleName `
            --start-ip-address $ipToAdd `
            --end-ip-address $ipToAdd 2>$null

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Current IP added to firewall" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Current IP rule may already exist" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "⚠️  Could not add current IP rule" -ForegroundColor Yellow
    }
}

# Setup common ISP ranges for dynamic IPs
if ($SetupCommonISPRanges) {
    Write-Host "🏢 Setting up common ISP ranges for dynamic IPs..." -ForegroundColor Yellow

    $commonRanges = @(
        @{Name="Comcast-Xfinity"; Start="24.0.0.0"; End="24.255.255.255"},
        @{Name="Spectrum-Charter"; Start="71.0.0.0"; End="71.255.255.255"},
        @{Name="Cox-Communications"; Start="68.0.0.0"; End="68.255.255.255"},
        @{Name="Verizon-FIOS"; Start="71.160.0.0"; End="71.191.255.255"},
        @{Name="ATT-Uverse"; Start="99.0.0.0"; End="99.255.255.255"},
        @{Name="CenturyLink"; Start="65.128.0.0"; End="65.255.255.255"},
        @{Name="Frontier"; Start="74.40.0.0"; End="74.47.255.255"}
    )

    foreach ($range in $commonRanges) {
        Write-Host "  • Adding $($range.Name) range..." -ForegroundColor Gray
        try {
            az sql server firewall-rule create `
                --resource-group $ResourceGroup `
                --server $SqlServer `
                --name "ISP-$($range.Name)" `
                --start-ip-address $range.Start `
                --end-ip-address $range.End 2>$null

            if ($LASTEXITCODE -eq 0) {
                Write-Host "    ✅ Added" -ForegroundColor Green
            } else {
                Write-Host "    ⚠️  May already exist" -ForegroundColor Yellow
            }
        }
        catch {
            Write-Host "    ⚠️  Could not add $($range.Name)" -ForegroundColor Yellow
        }
    }
    Write-Host "✅ Common ISP ranges configuration completed" -ForegroundColor Green
}

# Enable diagnostic settings for firewall monitoring
if ($EnableMonitoring) {
    Write-Host "📊 Setting up firewall monitoring..." -ForegroundColor Yellow
    try {
        az monitor diagnostic-settings create `
            --name "sql-firewall-monitoring" `
            --resource "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$ResourceGroup/providers/Microsoft.Sql/servers/$SqlServer" `
            --logs '[{"category": "SQLSecurityAuditEvents", "enabled": true}]' `
            --metrics '[{"category": "AllMetrics", "enabled": true}]' `
            --workspace "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/DefaultResourceGroup/providers/Microsoft.OperationalInsights/workspaces/DefaultWorkspace" 2>$null

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Firewall monitoring enabled" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Could not enable monitoring (may already exist)" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "⚠️  Could not enable monitoring" -ForegroundColor Yellow
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

# Create firewall policy documentation
Write-Host "`n📝 Generating Firewall Policy Documentation..." -ForegroundColor Yellow

$policyDoc = @"
# WileyWidget Firewall Policy Documentation

Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

## Server Information
- Resource Group: $ResourceGroup
- SQL Server: $SqlServer
- Subscription: $(az account show --query name -o tsv)

## Firewall Rules Summary
- Azure Services: $(if ($AllowAllAzureServices) { "Enabled" } else { "Not Configured" })
- Common ISP Ranges: $(if ($SetupCommonISPRanges) { "Enabled" } else { "Not Configured" })
- Current IP: $(if ($ipToAdd) { $ipToAdd } else { "Not Detected" })
- Monitoring: $(if ($EnableMonitoring) { "Enabled" } else { "Not Configured" })

## Security Notes
- This configuration allows broad access for dynamic IP support
- Regular monitoring and log review is recommended
- Consider VPN for highly sensitive operations
- Review and update rules regularly

## Maintenance Commands
# Update current IP
.\scripts\update-firewall-ip.ps1 -ResourceGroup "$ResourceGroup" -SqlServer "$SqlServer"

# List current rules
az sql server firewall-rule list --resource-group "$ResourceGroup" --server "$SqlServer" --output table

# Remove old rules
az sql server firewall-rule delete --resource-group "$ResourceGroup" --server "$SqlServer" --name "OldRuleName" --yes
"@

$policyDoc | Out-File -FilePath "firewall-policy-$(Get-Date -Format 'yyyyMMdd').md" -Encoding UTF8
Write-Host "✅ Firewall policy documentation saved" -ForegroundColor Green

Write-Host "`n🎉 Liberal firewall policies configured successfully!" -ForegroundColor Green
Write-Host "📝 Policy Summary:" -ForegroundColor Cyan
Write-Host "   • Azure Services: $(if ($AllowAllAzureServices) { "Allowed" } else { "Not Configured" })" -ForegroundColor White
Write-Host "   • Common ISP Ranges: $(if ($SetupCommonISPRanges) { "Configured" } else { "Not Configured" })" -ForegroundColor White
Write-Host "   • Current IP: $(if ($ipToAdd) { "Added ($ipToAdd)" } else { "Not Detected" })" -ForegroundColor White
Write-Host "   • Monitoring: $(if ($EnableMonitoring) { "Enabled" } else { "Not Configured" })" -ForegroundColor White
Write-Host "   • Documentation: Generated" -ForegroundColor White

Write-Host "`n💡 Next Steps:" -ForegroundColor Yellow
Write-Host "   • Test database connectivity: .\scripts\test-database-connection.ps1" -ForegroundColor White
Write-Host "   • Update IP when it changes: .\scripts\update-firewall-ip.ps1" -ForegroundColor White
Write-Host "   • Monitor firewall logs regularly" -ForegroundColor White
