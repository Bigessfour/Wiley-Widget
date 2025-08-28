# Azure Setup Script for WileyWidget
# This script configures Azure CLI and MCP server for database connectivity

param(
    [Parameter(Mandatory=$false)]
    [string]$AzureSubscriptionId,

    [Parameter(Mandatory=$false)]
    [string]$AzureResourceGroup,

    [Parameter(Mandatory=$false)]
    [string]$AzureLocation = "East US",

    [Parameter(Mandatory=$false)]
    [switch]$SkipLogin,

    [Parameter(Mandatory=$false)]
    [switch]$InstallMCP
)

# Configuration
$ScriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptPath
$EnvFile = Join-Path $ProjectRoot ".env"

Write-Host "🔧 WileyWidget Azure Setup Script" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

# Function to check Azure CLI
function Test-AzureCLI {
    try {
        $version = az --version 2>$null | Select-Object -First 1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Azure CLI is installed and working" -ForegroundColor Green
            return $true
        }
    }
    catch {
        Write-Host "❌ Azure CLI is not available" -ForegroundColor Red
        return $false
    }
}

# Function to login to Azure
function Connect-AzureAccount {
    if ($SkipLogin) {
        Write-Host "⏭️  Skipping Azure login as requested" -ForegroundColor Yellow
        return
    }

    Write-Host "🔐 Logging into Azure..." -ForegroundColor Yellow
    try {
        az login --use-device-code
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Successfully logged into Azure" -ForegroundColor Green
        } else {
            Write-Host "❌ Azure login failed" -ForegroundColor Red
            exit 1
        }
    }
    catch {
        Write-Host "❌ Azure login failed: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# Function to set Azure subscription
function Set-AzureSubscription {
    param([string]$SubscriptionId)

    if ([string]::IsNullOrEmpty($SubscriptionId)) {
        Write-Host "📋 Available subscriptions:" -ForegroundColor Yellow
        az account list --output table
        $SubscriptionId = Read-Host "Enter subscription ID"
    }

    Write-Host "🔄 Setting Azure subscription to: $SubscriptionId" -ForegroundColor Yellow
    az account set --subscription $SubscriptionId
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Azure subscription set successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ Failed to set Azure subscription" -ForegroundColor Red
        exit 1
    }
}

# Function to create resource group
function New-AzureResourceGroup {
    param([string]$ResourceGroup, [string]$Location)

    if ([string]::IsNullOrEmpty($ResourceGroup)) {
        $ResourceGroup = Read-Host "Enter resource group name (default: wileywidget-rg)"
        if ([string]::IsNullOrEmpty($ResourceGroup)) {
            $ResourceGroup = "wileywidget-rg"
        }
    }

    Write-Host "🏗️  Creating resource group: $ResourceGroup in $Location" -ForegroundColor Yellow
    az group create --name $ResourceGroup --location $Location
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Resource group created successfully" -ForegroundColor Green
        return $ResourceGroup
    } else {
        Write-Host "❌ Failed to create resource group" -ForegroundColor Red
        exit 1
    }
}

# Function to create Azure SQL Server
function New-AzureSQLServer {
    param([string]$ResourceGroup, [string]$Location)

    $ServerName = "wileywidget-sql-" + (Get-Random -Minimum 1000 -Maximum 9999)
    $AdminUser = "wileyadmin"
    $AdminPassword = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 16 | ForEach-Object {[char]$_})

    Write-Host "🗄️  Creating Azure SQL Server: $ServerName" -ForegroundColor Yellow
    az sql server create `
        --name $ServerName `
        --resource-group $ResourceGroup `
        --location $Location `
        --admin-user $AdminUser `
        --admin-password $AdminPassword

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Azure SQL Server created successfully" -ForegroundColor Green

        # Create firewall rule for Azure services
        Write-Host "🔥 Configuring firewall rules..." -ForegroundColor Yellow
        az sql server firewall-rule create `
            --resource-group $ResourceGroup `
            --server $ServerName `
            --name "AllowAzureServices" `
            --start-ip-address "0.0.0.0" `
            --end-ip-address "0.0.0.0"

        # Create database
        $DatabaseName = "WileyWidgetDB"
        Write-Host "📊 Creating database: $DatabaseName" -ForegroundColor Yellow
        az sql db create `
            --resource-group $ResourceGroup `
            --server $ServerName `
            --name $DatabaseName `
            --service-objective "Basic"

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Database created successfully" -ForegroundColor Green

            # Generate connection string
            $ConnectionString = "Server=tcp:$ServerName.database.windows.net,1433;Initial Catalog=$DatabaseName;Persist Security Info=False;User ID=$AdminUser;Password=$AdminPassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

            return @{
                ServerName = $ServerName
                DatabaseName = $DatabaseName
                AdminUser = $AdminUser
                AdminPassword = $AdminPassword
                ConnectionString = $ConnectionString
            }
        }
    }

    Write-Host "❌ Failed to create Azure SQL resources" -ForegroundColor Red
    return $null
}

# Function to update environment file
function Update-EnvironmentFile {
    param([hashtable]$AzureConfig)

    Write-Host "📝 Updating environment configuration..." -ForegroundColor Yellow

    $envContent = @"
# Azure Configuration
AZURE_SUBSCRIPTION_ID=$($AzureConfig.SubscriptionId)
AZURE_RESOURCE_GROUP=$($AzureConfig.ResourceGroup)
AZURE_SQL_SERVER=$($AzureConfig.ServerName)
AZURE_SQL_DATABASE=$($AzureConfig.DatabaseName)
AZURE_SQL_ADMIN_USER=$($AzureConfig.AdminUser)
AZURE_SQL_ADMIN_PASSWORD=$($AzureConfig.AdminPassword)
AZURE_SQL_CONNECTION_STRING=$($AzureConfig.ConnectionString)
"@

    if (Test-Path $EnvFile) {
        $existingContent = Get-Content $EnvFile -Raw
        if ($existingContent -notmatch "# Azure Configuration") {
            Add-Content $EnvFile "`n$envContent"
        } else {
            Write-Host "⚠️  Azure configuration already exists in .env file" -ForegroundColor Yellow
        }
    } else {
        $envContent | Out-File $EnvFile -Encoding UTF8
    }

    Write-Host "✅ Environment configuration updated" -ForegroundColor Green
}

# Function to install Azure MCP Server extension
function Install-AzureMCPServer {
    Write-Host "🔌 Installing Azure MCP Server extension..." -ForegroundColor Yellow

    # Check if extension is already installed
    $extension = code --list-extensions | Where-Object { $_ -eq "ms-azuretools.vscode-azure-mcp-server" }
    if ($extension) {
        Write-Host "✅ Azure MCP Server extension is already installed" -ForegroundColor Green
        return
    }

    # Install the extension
    code --install-extension ms-azuretools.vscode-azure-mcp-server
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Azure MCP Server extension installed successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ Failed to install Azure MCP Server extension" -ForegroundColor Red
    }
}

# Main execution
try {
    # Check Azure CLI
    if (-not (Test-AzureCLI)) {
        Write-Host "❌ Azure CLI is required but not available. Please install Azure CLI first." -ForegroundColor Red
        Write-Host "📥 Download from: https://aka.ms/installazurecliwindows" -ForegroundColor Yellow
        exit 1
    }

    # Login to Azure
    Connect-AzureAccount

    # Set subscription
    Set-AzureSubscription -SubscriptionId $AzureSubscriptionId

    # Create resource group
    $resourceGroup = New-AzureResourceGroup -ResourceGroup $AzureResourceGroup -Location $AzureLocation

    # Create Azure SQL resources
    $azureConfig = New-AzureSQLServer -ResourceGroup $resourceGroup -Location $AzureLocation

    if ($azureConfig) {
        # Update environment file
        $azureConfig.SubscriptionId = $AzureSubscriptionId
        $azureConfig.ResourceGroup = $resourceGroup
        Update-EnvironmentFile -AzureConfig $azureConfig

        Write-Host "`n🎉 Azure setup completed successfully!" -ForegroundColor Green
        Write-Host "📋 Summary:" -ForegroundColor Cyan
        Write-Host "   • Resource Group: $resourceGroup" -ForegroundColor White
        Write-Host "   • SQL Server: $($azureConfig.ServerName)" -ForegroundColor White
        Write-Host "   • Database: $($azureConfig.DatabaseName)" -ForegroundColor White
        Write-Host "   • Connection String saved to .env file" -ForegroundColor White

        # Install MCP Server if requested
        if ($InstallMCP) {
            Install-AzureMCPServer
        }
    } else {
        Write-Host "❌ Azure setup failed" -ForegroundColor Red
        exit 1
    }

} catch {
    Write-Host "❌ An error occurred: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`n✨ Setup complete! You can now use Azure resources in your WileyWidget application." -ForegroundColor Green
