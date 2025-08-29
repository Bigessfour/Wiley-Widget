# WileyWidget Azure Setup Status and Next Steps

Write-Host "🎯 WileyWidget Azure Integration Status Report" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# Configuration
$ProjectRoot = "c:\Users\biges\Desktop\Wiley_Widget"
$ScriptsPath = Join-Path $ProjectRoot "scripts"
$DocsPath = Join-Path $ProjectRoot "docs"
$EnvFile = Join-Path $ProjectRoot ".env"

# Function to check file existence
function Test-FileExist {
    param([string]$Path, [string]$Description)
    if (Test-Path $Path) {
        Write-Host "✅ $Description - Found" -ForegroundColor Green
        return $true
    }
    else {
        Write-Host "❌ $Description - Missing" -ForegroundColor Red
        return $false
    }
}

# Function to check Azure CLI status
function Test-AzureCLIStatus {
    try {
        $account = az account show 2>$null | ConvertFrom-Json
        if ($account) {
            Write-Host "✅ Azure CLI - Connected" -ForegroundColor Green
            Write-Host "   • Subscription: $($account.name)" -ForegroundColor White
            Write-Host "   • User: $($account.user.name)" -ForegroundColor White
            return $true
        }
    }
    catch {
        Write-Host "❌ Azure CLI - Not connected" -ForegroundColor Red
        return $false
    }
    return $false
}

# Function to check environment configuration
function Test-EnvironmentConfig {
    if (Test-Path $EnvFile) {
        $envContent = Get-Content $EnvFile -Raw
        $azureVars = $envContent -split "`n" | Where-Object { $_ -match '^AZURE_.*=' }

        if ($azureVars.Count -gt 0) {
            Write-Host "✅ Environment Configuration - Configured" -ForegroundColor Green
            Write-Host "   • Found $($azureVars.Count) Azure environment variables" -ForegroundColor White
            return $true
        }
    }

    Write-Host "❌ Environment Configuration - Not configured" -ForegroundColor Red
    return $false
}

# Function to check MCP server extension
function Test-MCPServerExtension {
    $extension = code --list-extensions 2>$null | Where-Object { $_ -eq "ms-azuretools.vscode-azure-mcp-server" }
    if ($extension) {
        Write-Host "✅ Azure MCP Server Extension - Installed" -ForegroundColor Green
        return $true
    }
    else {
        Write-Host "❌ Azure MCP Server Extension - Not installed" -ForegroundColor Red
        return $false
    }
}

# Status checks
Write-Host "`n📋 Component Status:" -ForegroundColor Yellow
Write-Host "-------------------" -ForegroundColor Yellow

# Check setup scripts
$setupScripts = @(
    @{ Path = Join-Path $ScriptsPath "setup-azure.ps1"; Description = "Azure Setup Script" },
    @{ Path = Join-Path $ScriptsPath "test-database-connection.ps1"; Description = "Database Test Script" },
    @{ Path = Join-Path $ScriptsPath "setup-database.ps1"; Description = "LocalDB Setup Script" },
    @{ Path = Join-Path $ScriptsPath "setup-license.ps1"; Description = "License Setup Script" }
)

$scriptsStatus = $true
foreach ($script in $setupScripts) {
    if (-not (Test-FileExists -Path $script.Path -Description $script.Description)) {
        $scriptsStatus = $false
    }
}

# Check documentation
$docs = @(
    @{ Path = Join-Path $DocsPath "azure-setup.md"; Description = "Azure Setup Guide" },
    @{ Path = Join-Path $DocsPath "database-setup.md"; Description = "Database Setup Guide" },
    @{ Path = Join-Path $DocsPath "syncfusion-license-setup.md"; Description = "License Setup Guide" }
)

$docsStatus = $true
foreach ($doc in $docs) {
    if (-not (Test-FileExists -Path $doc.Path -Description $doc.Description)) {
        $docsStatus = $false
    }
}

# Check Azure components
Write-Host "`n☁️  Azure Components:" -ForegroundColor Yellow
Write-Host "-------------------" -ForegroundColor Yellow

$azureCLIStatus = Test-AzureCLIStatus
$envConfigStatus = Test-EnvironmentConfig
$mcpStatus = Test-MCPServerExtension

# Overall status
Write-Host "`n🎯 Overall Status:" -ForegroundColor Yellow
Write-Host "-----------------" -ForegroundColor Yellow

$overallStatus = $scriptsStatus -and $docsStatus -and $azureCLIStatus -and $envConfigStatus -and $mcpStatus

if ($overallStatus) {
    Write-Host "✅ COMPLETE: All components are properly configured!" -ForegroundColor Green
}
else {
    Write-Host "⚠️  PARTIAL: Some components need attention" -ForegroundColor Yellow
}

# Next steps
Write-Host "`n🚀 Next Steps:" -ForegroundColor Yellow
Write-Host "-------------" -ForegroundColor Yellow

if (-not $azureCLIStatus) {
    Write-Host "1. 🔐 Login to Azure: az login" -ForegroundColor White
}

if (-not $envConfigStatus) {
    Write-Host "2. ⚙️  Run Azure setup: .\scripts\setup-azure.ps1" -ForegroundColor White
}

if (-not $mcpStatus) {
    Write-Host "3. 🔌 Install MCP Server: code --install-extension ms-azuretools.vscode-azure-mcp-server" -ForegroundColor White
}

Write-Host "4. 🧪 Test connectivity: .\scripts\test-database-connection.ps1" -ForegroundColor White
Write-Host "5. 🏗️  Build application: dotnet build WileyWidget.csproj" -ForegroundColor White
Write-Host "6. ▶️  Run application: dotnet run --project WileyWidget.csproj" -ForegroundColor White

# Quick commands
Write-Host "`n💡 Quick Commands:" -ForegroundColor Yellow
Write-Host "-----------------" -ForegroundColor Yellow

Write-Host "• Test Azure CLI: az account show" -ForegroundColor White
Write-Host "• List resources: az resource list --output table" -ForegroundColor White
Write-Host "• Check database: .\scripts\test-database-connection.ps1" -ForegroundColor White
Write-Host "• View docs: Get-Content .\docs\azure-setup.md" -ForegroundColor White

Write-Host "`n✨ Ready to build amazing things with WileyWidget and Azure!" -ForegroundColor Green
