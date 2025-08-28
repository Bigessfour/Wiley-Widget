# Liberal Firewall Policy Setup for Dynamic IP Addresses

## Overview

This script sets up liberal firewall policies for WileyWidget Azure SQL Database to accommodate dynamic IP addresses while maintaining security best practices.

## Policy Configuration

### Firewall Rules Strategy

- **Allow Azure Services**: Enable access from all Azure services
- **Broad IP Ranges**: Allow access from common ISP ranges
- **Dynamic IP Support**: Configure for frequent IP changes
- **Security Monitoring**: Enable logging and alerts

---

## Automated Setup Script

```powershell
# Liberal Firewall Setup for Dynamic IP Addresses
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
    [string]$CurrentIP
)

Write-Host "🔥 Setting up Liberal Firewall Policies for Dynamic IP" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

# Allow Azure Services (Most Important for Dynamic IPs)
if ($AllowAllAzureServices) {
    Write-Host "🌐 Allowing access from all Azure services..." -ForegroundColor Yellow
    az sql server firewall-rule create `
        --resource-group $ResourceGroup `
        --server $SqlServer `
        --name "AllowAllAzureServices" `
        --start-ip-address "0.0.0.0" `
        --end-ip-address "0.0.0.0"
    Write-Host "✅ Azure services access enabled" -ForegroundColor Green
}

# Add current IP if provided
if ($CurrentIP) {
    Write-Host "📍 Adding current IP: $CurrentIP" -ForegroundColor Yellow
    az sql server firewall-rule create `
        --resource-group $ResourceGroup `
        --server $SqlServer `
        --name "CurrentIP-$(Get-Date -Format 'yyyyMMdd')" `
        --start-ip-address $CurrentIP `
        --end-ip-address $CurrentIP
    Write-Host "✅ Current IP added to firewall" -ForegroundColor Green
}

# Setup common ISP ranges for dynamic IPs
if ($SetupCommonISPRanges) {
    Write-Host "🏢 Setting up common ISP ranges for dynamic IPs..." -ForegroundColor Yellow

    $commonRanges = @(
        @{Name="Comcast-Xfinity"; Start="24.0.0.0"; End="24.255.255.255"},
        @{Name="Spectrum-Charter"; Start="71.0.0.0"; End="71.255.255.255"},
        @{Name="Cox-Communications"; Start="68.0.0.0"; End="68.255.255.255"},
        @{Name="Verizon-FIOS"; Start="71.160.0.0"; End="71.191.255.255"},
        @{Name="AT&T-Uverse"; Start="99.0.0.0"; End="99.255.255.255"},
        @{Name="CenturyLink"; Start="65.128.0.0"; End="65.255.255.255"},
        @{Name="Frontier"; Start="74.40.0.0"; End="74.47.255.255"}
    )

    foreach ($range in $commonRanges) {
        Write-Host "  • Adding $($range.Name) range..." -ForegroundColor Gray
        az sql server firewall-rule create `
            --resource-group $ResourceGroup `
            --server $SqlServer `
            --name "ISP-$($range.Name)" `
            --start-ip-address $range.Start `
            --end-ip-address $range.End
    }
    Write-Host "✅ Common ISP ranges configured" -ForegroundColor Green
}

# Enable diagnostic settings for firewall monitoring
Write-Host "📊 Setting up firewall monitoring..." -ForegroundColor Yellow
az monitor diagnostic-settings create `
    --name "sql-firewall-monitoring" `
    --resource "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$ResourceGroup/providers/Microsoft.Sql/servers/$SqlServer" `
    --logs '[{"category": "SQLSecurityAuditEvents", "enabled": true}]' `
    --metrics '[{"category": "AllMetrics", "enabled": true}]' `
    --workspace "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/DefaultResourceGroup/providers/Microsoft.OperationalInsights/workspaces/DefaultWorkspace"

Write-Host "✅ Firewall monitoring enabled" -ForegroundColor Green

# Display current firewall rules
Write-Host "`n📋 Current Firewall Rules:" -ForegroundColor Cyan
az sql server firewall-rule list `
    --resource-group $ResourceGroup `
    --server $SqlServer `
    --output table

Write-Host "`n🎉 Liberal firewall policies configured successfully!" -ForegroundColor Green
Write-Host "📝 Policy Summary:" -ForegroundColor Cyan
Write-Host "   • Azure Services: Allowed" -ForegroundColor White
Write-Host "   • Common ISP Ranges: Configured" -ForegroundColor White
Write-Host "   • Monitoring: Enabled" -ForegroundColor White
Write-Host "   • Dynamic IP Support: Ready" -ForegroundColor White
```

## Manual Firewall Configuration

### 1. Allow Azure Services

```powershell
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "AllowAllAzureServices" --start-ip-address "0.0.0.0" --end-ip-address "0.0.0.0"
```

### 2. Add Common ISP Ranges

```powershell
# Comcast/Xfinity
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "Comcast-Xfinity" --start-ip-address "24.0.0.0" --end-ip-address "24.255.255.255"

# Spectrum/Charter
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "Spectrum-Charter" --start-ip-address "71.0.0.0" --end-ip-address "71.255.255.255"

# Verizon FIOS
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "Verizon-FIOS" --start-ip-address "71.160.0.0" --end-ip-address "71.191.255.255"

# AT&T Uverse
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "ATT-Uverse" --start-ip-address "99.0.0.0" --end-ip-address "99.255.255.255"
```

### 3. Add Current IP Address

```powershell
# Get current public IP
$currentIP = Invoke-RestMethod -Uri "https://api.ipify.org"

# Add to firewall
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "CurrentIP-$(Get-Date -Format 'yyyyMMdd')" --start-ip-address $currentIP --end-ip-address $currentIP
```

## Dynamic IP Management

### Automated IP Update Script

```powershell
# Dynamic IP Update Script
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory=$true)]
    [string]$SqlServer
)

# Get current public IP
$currentIP = Invoke-RestMethod -Uri "https://api.ipify.org"
$ruleName = "CurrentIP-$(Get-Date -Format 'yyyyMMdd')"

Write-Host "🔄 Updating firewall for dynamic IP: $currentIP" -ForegroundColor Yellow

# Remove old current IP rules (older than today)
$existingRules = az sql server firewall-rule list --resource-group $ResourceGroup --server $SqlServer --query "[?contains(name, 'CurrentIP-')].name" -o tsv

foreach ($rule in $existingRules) {
    if ($rule -ne $ruleName) {
        az sql server firewall-rule delete --resource-group $ResourceGroup --server $SqlServer --name $rule --yes
    }
}

# Add current IP
az sql server firewall-rule create --resource-group $ResourceGroup --server $SqlServer --name $ruleName --start-ip-address $currentIP --end-ip-address $currentIP

Write-Host "✅ Firewall updated for current IP: $currentIP" -ForegroundColor Green
```

## Security Considerations

### Risk Mitigation

- **Monitoring**: Enable diagnostic logging for all firewall changes
- **Alerts**: Set up alerts for suspicious access patterns
- **Regular Review**: Audit firewall rules monthly
- **VPN Option**: Consider VPN for highly sensitive operations

### Best Practices

- **Principle of Least Privilege**: Only allow necessary access
- **Regular Updates**: Update IP ranges as needed
- **Documentation**: Keep firewall policies documented
- **Testing**: Regularly test connectivity from different locations

## Monitoring & Alerts

### Enable Firewall Auditing

```powershell
az monitor diagnostic-settings create --name "sql-firewall-audit" --resource "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Sql/servers/server" --logs '[{"category": "SQLSecurityAuditEvents", "enabled": true}]' --workspace "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.OperationalInsights/workspaces/workspace"
```

### Setup Alerts

```powershell
# Alert for failed login attempts
az monitor metrics alert create --name "SQL-Failed-Logins" --resource "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Sql/servers/server" --condition "count of failed_connections > 10" --action "/subscriptions/sub/resourceGroups/rg/providers/microsoft.insights/actionGroups/actionGroup"
```

## Documentation & Compliance

### Firewall Policy Documentation

```powershell
# Export current firewall rules
az sql server firewall-rule list --resource-group "wileywidget-rg" --server "wileywidget-sql" --output json > firewall-rules-$(Get-Date -Format 'yyyyMMdd').json
```

### Compliance Checklist

- [ ] Azure Services access enabled
- [ ] Common ISP ranges configured
- [ ] Dynamic IP support implemented
- [ ] Monitoring and alerting enabled
- [ ] Documentation maintained
- [ ] Regular security reviews scheduled

---

## Last updated: August 28, 2025
