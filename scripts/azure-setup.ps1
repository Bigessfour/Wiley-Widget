# Azure Setup and Configuration Script for WileyWidget
# Run this script to set up your Azure development environment

param(
    [switch]$TestConnection,
    [switch]$CreateResources,
    [switch]$DeployDatabase
)

Write-Host "🔧 WileyWidget Azure Setup Script" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

# Check if .env file exists
if (!(Test-Path ".env")) {
    Write-Host "❌ .env file not found!" -ForegroundColor Red
    Write-Host "Please copy .env.example to .env and fill in your Azure values" -ForegroundColor Yellow
    exit 1
}

# Load environment variables
Write-Host "📖 Loading environment configuration..." -ForegroundColor Green
Get-Content ".env" | ForEach-Object {
    if ($_ -match '^([^=]+)=(.*)$') {
        $key = $matches[1]
        $value = $matches[2]
        [Environment]::SetEnvironmentVariable($key, $value, "Process")
        Write-Host "  ✓ $key" -ForegroundColor Green
    }
}

# Check Azure CLI login
Write-Host "`n🔐 Checking Azure CLI authentication..." -ForegroundColor Green
try {
    $account = az account show | ConvertFrom-Json
    Write-Host "  ✓ Signed in as: $($account.user.name)" -ForegroundColor Green
    Write-Host "  ✓ Subscription: $($account.name)" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Not signed in to Azure CLI" -ForegroundColor Red
    Write-Host "  Run: az login" -ForegroundColor Yellow
    exit 1
}

if ($TestConnection) {
    Write-Host "`n🧪 Testing Azure SQL Connection..." -ForegroundColor Green

    $connectionString = "Server=tcp:$($env:AZURE_SQL_SERVER),1433;Database=$($env:AZURE_SQL_DATABASE);User ID=$($env:AZURE_SQL_USER);Password=$($env:AZURE_SQL_PASSWORD);Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

    try {
        # Test connection using .NET SQL client
        Add-Type -AssemblyName System.Data
        $connection = New-Object System.Data.SqlClient.SqlConnection
        $connection.ConnectionString = $connectionString
        $connection.Open()
        Write-Host "  ✓ Successfully connected to Azure SQL Database" -ForegroundColor Green
        $connection.Close()
    } catch {
        Write-Host "  ❌ Failed to connect to Azure SQL Database" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

if ($CreateResources) {
    Write-Host "`n🏗️  Creating Azure Resources..." -ForegroundColor Green

    # Create Resource Group
    Write-Host "  Creating resource group..." -ForegroundColor Yellow
    az group create --name "WileyWidget-RG" --location "East US" --output none

    # Create SQL Server
    Write-Host "  Creating Azure SQL Server..." -ForegroundColor Yellow
    az sql server create --name $env:AZURE_SQL_SERVER.Split('.')[0] --resource-group "WileyWidget-RG" --location "East US" --admin-user $env:AZURE_SQL_USER --admin-password $env:AZURE_SQL_PASSWORD --output none

    # Create SQL Database
    Write-Host "  Creating Azure SQL Database..." -ForegroundColor Yellow
    az sql db create --resource-group "WileyWidget-RG" --server $env:AZURE_SQL_SERVER.Split('.')[0] --name $env:AZURE_SQL_DATABASE --service-objective S0 --output none

    # Configure firewall
    Write-Host "  Configuring firewall..." -ForegroundColor Yellow
    az sql server firewall-rule create --resource-group "WileyWidget-RG" --server $env:AZURE_SQL_SERVER.Split('.')[0] --name "AllowAllWindowsAzureIps" --start-ip-address "0.0.0.0" --end-ip-address "0.0.0.0" --output none

    Write-Host "  ✓ Azure resources created successfully!" -ForegroundColor Green
}

if ($DeployDatabase) {
    Write-Host "`n🗄️  Deploying Database Schema..." -ForegroundColor Green

    # Run EF Core migrations
    Write-Host "  Running Entity Framework migrations..." -ForegroundColor Yellow
    dotnet ef database update --project WileyWidget.csproj

    Write-Host "  ✓ Database schema deployed!" -ForegroundColor Green
}

Write-Host "`n🎉 Azure setup complete!" -ForegroundColor Cyan
Write-Host "Use the following commands to manage your Azure resources:" -ForegroundColor White
Write-Host "  • Test connection: .\azure-setup.ps1 -TestConnection" -ForegroundColor Gray
Write-Host "  • Create resources: .\azure-setup.ps1 -CreateResources" -ForegroundColor Gray
Write-Host "  • Deploy database: .\azure-setup.ps1 -DeployDatabase" -ForegroundColor Gray
