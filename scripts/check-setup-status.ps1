# WileyWidget Azure Setup Status and Next Steps

Write-Information "🎯 WileyWidget Azure Integration Status Report" -InformationAction Continue
Write-Information "=============================================" -InformationAction Continue

# Configuration
$ProjectRoot = "c:\Users\biges\Desktop\Wiley_Widget"
$ScriptsPath = Join-Path $ProjectRoot "scripts"
$DocsPath = Join-Path $ProjectRoot "docs"
$EnvFile = Join-Path $ProjectRoot ".env"

# Function to check file existence
function Test-FileExist {
    param([string]$Path, [string]$Description)
    if (Test-Path $Path) {
        Write-Information "✅ $Description - Found" -InformationAction Continue
        return $true
    }
    else {
        Write-Information "❌ $Description - Missing" -InformationAction Continue
        return $false
    }
}

# Function to check Azure CLI status
function Test-AzureCLIStatus {
    try {
        $account = az account show 2>$null | ConvertFrom-Json
        if ($account) {
            Write-Information "✅ Azure CLI - Connected" -InformationAction Continue
            Write-Information "   • Subscription: $($account.name)" -InformationAction Continue
            Write-Information "   • User: $($account.user.name)" -InformationAction Continue
            return $true
        }
    }
    catch {
        Write-Information "❌ Azure CLI - Not connected" -InformationAction Continue
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
            Write-Information "✅ Environment Configuration - Configured" -InformationAction Continue
            Write-Information "   • Found $($azureVars.Count) Azure environment variables" -InformationAction Continue
            return $true
        }
    }

    Write-Information "❌ Environment Configuration - Not configured" -InformationAction Continue
    return $false
}

# Function to check MCP server extension
function Test-MCPServerExtension {
    $extension = code --list-extensions 2>$null | Where-Object { $_ -eq "ms-azuretools.vscode-azure-mcp-server" }
    if ($extension) {
        Write-Information "✅ Azure MCP Server Extension - Installed" -InformationAction Continue
        return $true
    }
    else {
        Write-Information "❌ Azure MCP Server Extension - Not installed" -InformationAction Continue
        return $false
    }
}

# Status checks
Write-Information "`n📋 Component Status:" -InformationAction Continue
Write-Information "-------------------" -InformationAction Continue

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
Write-Information "`n☁️  Azure Components:" -InformationAction Continue
Write-Information "-------------------" -InformationAction Continue

$azureCLIStatus = Test-AzureCLIStatus
$envConfigStatus = Test-EnvironmentConfig
$mcpStatus = Test-MCPServerExtension

# Overall status
Write-Information "`n🎯 Overall Status:" -InformationAction Continue
Write-Information "-----------------" -InformationAction Continue

$overallStatus = $scriptsStatus -and $docsStatus -and $azureCLIStatus -and $envConfigStatus -and $mcpStatus

if ($overallStatus) {
    Write-Information "✅ COMPLETE: All components are properly configured!" -InformationAction Continue
}
else {
    Write-Information "⚠️  PARTIAL: Some components need attention" -InformationAction Continue
}

# Next steps
Write-Information "`n🚀 Next Steps:" -InformationAction Continue
Write-Information "-------------" -InformationAction Continue

if (-not $azureCLIStatus) {
    Write-Information "1. 🔐 Login to Azure: az login" -InformationAction Continue
}

if (-not $envConfigStatus) {
    Write-Information "2. ⚙️  Run Azure setup: .\scripts\setup-azure.ps1" -InformationAction Continue
}

if (-not $mcpStatus) {
    Write-Information "3. 🔌 Install MCP Server: code --install-extension ms-azuretools.vscode-azure-mcp-server" -InformationAction Continue
}

Write-Information "4. 🧪 Test connectivity: .\scripts\test-database-connection.ps1" -InformationAction Continue
Write-Information "5. 🏗️  Build application: dotnet build WileyWidget.csproj" -InformationAction Continue
Write-Information "6. ▶️  Run application: dotnet run --project WileyWidget.csproj" -InformationAction Continue

# Quick commands
Write-Information "`n💡 Quick Commands:" -InformationAction Continue
Write-Information "-----------------" -InformationAction Continue

Write-Information "• Test Azure CLI: az account show" -InformationAction Continue
Write-Information "• List resources: az resource list --output table" -InformationAction Continue
Write-Information "• Check database: .\scripts\test-database-connection.ps1" -InformationAction Continue
Write-Information "• View docs: Get-Content .\docs\azure-setup.md" -InformationAction Continue

Write-Information "`n✨ Ready to build amazing things with WileyWidget and Azure!" -InformationAction Continue
