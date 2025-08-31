# Azure Setup and Configuration Script for WileyWidget
# Run this script to set up your development environment

param(
    [switch]$TestConnection,
    [switch]$CreateResources,
    [switch]$DeployDatabase
)

Write-Information "🔧 WileyWidget Azure Setup Script" -InformationAction Continue
Write-Information "=================================" -InformationAction Continue

# Check if .env file exists
if (!(Test-Path ".env")) {
    Write-Information "❌ .env file not found!" -InformationAction Continue
    Write-Information "Please copy .env.example to .env and fill in your Azure values" -InformationAction Continue
    exit 1
}

# Check for administrator privileges
Write-Information "🔍 Checking for administrator privileges..." -InformationAction Continue
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Information "  ❌ Administrator privileges required to set machine environment variables" -InformationAction Continue
    Write-Information "  Please run this script as administrator or use 'Process' scope" -InformationAction Continue
    exit 1
}
Write-Information "  ✓ Running as administrator" -InformationAction Continue

# Load environment variables
Write-Information "📖 Loading environment configuration..." -InformationAction Continue
Get-Content ".env" | ForEach-Object {
    if ($_ -match '^([^=]+)=(.*)$') {
        $key = $matches[1]
        $value = $matches[2]
        [Environment]::SetEnvironmentVariable($key, $value, "Machine")
        Write-Information "  ✓ $key set in machine environment" -InformationAction Continue
    }
}

# Check Azure CLI login
Write-Information "`n🔐 Checking Azure CLI authentication..." -InformationAction Continue
try {
    $account = az account show | ConvertFrom-Json
    Write-Information "  ✓ Signed in as: $($account.user.name)" -InformationAction Continue
    Write-Information "  ✓ Subscription: $($account.name)" -InformationAction Continue
} catch {
    Write-Information "  ❌ Not signed in to Azure CLI" -InformationAction Continue
    Write-Information "  Run: az login" -InformationAction Continue
    exit 1
}

if ($TestConnection) {
    Write-Information "`n🧪 Testing Azure SQL Connection..." -InformationAction Continue

    $connectionString = "Server=tcp:$($env:AZURE_SQL_SERVER),1433;Database=$($env:AZURE_SQL_DATABASE);User ID=$($env:AZURE_SQL_USER);Password=$($env:AZURE_SQL_PASSWORD);Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

    try {
        # Test connection using .NET SQL client
        Add-Type -AssemblyName System.Data
        $connection = New-Object System.Data.SqlClient.SqlConnection
        $connection.ConnectionString = $connectionString
        $connection.Open()
        Write-Information "  ✓ Successfully connected to Azure SQL Database" -InformationAction Continue
        $connection.Close()
    } catch {
        Write-Information "  ❌ Failed to connect to Azure SQL Database" -InformationAction Continue
        Write-Information "  Error: $($_.Exception.Message)" -InformationAction Continue
    }
}

if ($CreateResources) {
    Write-Information "`n🏗️  Creating Azure Resources..." -InformationAction Continue

    # Create Resource Group
    Write-Information "  Creating resource group..." -InformationAction Continue
    az group create --name "BusBuddy-RG" --location "westus2" --output none

    # Create SQL Server
    Write-Information "  Creating Azure SQL Server..." -InformationAction Continue
    az sql server create --name $env:AZURE_SQL_SERVER.Split('.')[0] --resource-group "BusBuddy-RG" --location "westus2" --admin-user $env:AZURE_SQL_USER --admin-password $env:AZURE_SQL_PASSWORD --output none

    # Create SQL Database
    Write-Information "  Creating Azure SQL Database..." -InformationAction Continue
    az sql db create --resource-group "BusBuddy-RG" --server $env:AZURE_SQL_SERVER.Split('.')[0] --name $env:AZURE_SQL_DATABASE --service-objective S0 --output none

    # Configure firewall
    Write-Information "  Configuring firewall..." -InformationAction Continue
    az sql server firewall-rule create --resource-group "BusBuddy-RG" --server $env:AZURE_SQL_SERVER.Split('.')[0] --name "AllowAllWindowsAzureIps" --start-ip-address "0.0.0.0" --end-ip-address "0.0.0.0" --output none

    Write-Information "  ✓ Azure resources created successfully!" -InformationAction Continue
}

if ($DeployDatabase) {
    Write-Information "`n🗄️  Deploying Database Schema..." -InformationAction Continue

    # Run EF Core migrations
    Write-Information "  Running Entity Framework migrations..." -InformationAction Continue
    dotnet ef database update --project WileyWidget.csproj

    Write-Information "  ✓ Database schema deployed!" -InformationAction Continue
}

Write-Information "`n🎉 Azure setup complete!" -InformationAction Continue
Write-Information "Use the following commands to manage your Azure resources:" -InformationAction Continue
Write-Information "  • Test connection: .\azure-setup.ps1 -TestConnection" -InformationAction Continue
Write-Information "  • Create resources: .\azure-setup.ps1 -CreateResources" -InformationAction Continue
Write-Information "  • Deploy database: .\azure-setup.ps1 -DeployDatabase" -InformationAction Continue
