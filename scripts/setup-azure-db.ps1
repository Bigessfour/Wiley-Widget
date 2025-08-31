# Azure SQL Database Connection Setup for WileyWidget
# This script helps you configure your Azure SQL database connection

param(
    [switch]$GetConnectionDetails,
    [switch]$TestConnection,
    [switch]$UpdateEnvFile,
    [string]$ServerName,
    [string]$DatabaseName,
    [string]$Username,
    [SecureString]$Password
)

Write-Host "🔗 WileyWidget Azure SQL Database Setup" -ForegroundColor Cyan
Write-Host "=" * 50

function Show-AzureConnectionGuide {
    Write-Host ""
    Write-Host "📋 How to Get Your Azure SQL Connection Details:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. Go to your Azure Portal: https://portal.azure.com" -ForegroundColor White
    Write-Host "2. Navigate to your SQL Database (WileyWidgetDB)" -ForegroundColor White
    Write-Host "3. Go to 'Connection strings' in the left menu" -ForegroundColor White
    Write-Host "4. Copy the ADO.NET connection string" -ForegroundColor White
    Write-Host "5. Extract the following values:" -ForegroundColor White
    Write-Host "   - Server: tcp:your-server.database.windows.net,1433" -ForegroundColor Gray
    Write-Host "   - Database: YourDatabaseName" -ForegroundColor Gray
    Write-Host "   - User ID: your_username" -ForegroundColor Gray
    Write-Host "   - Password: your_password" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Example Connection String:" -ForegroundColor Cyan
    Write-Host "Server=tcp:wileywidget-server.database.windows.net,1433;Initial Catalog=WileyWidgetDB;Persist Security Info=False;User ID=wileywidget_admin;Password=W1l3yW1dg3t@2025!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;" -ForegroundColor Gray
    Write-Host ""
}

function Parse-ConnectionString {
    param([string]$ConnectionString)

    Write-Host "🔍 Parsing connection string..." -ForegroundColor Yellow

    # Extract server
    if ($ConnectionString -match "Server=tcp:([^,]+)") {
        $server = $matches[1]
        Write-Host "  ✅ Server: $server" -ForegroundColor Green
    }

    # Extract database
    if ($ConnectionString -match "Initial Catalog=([^;]+)") {
        $database = $matches[1]
        Write-Host "  ✅ Database: $database" -ForegroundColor Green
    }

    # Extract username
    if ($ConnectionString -match "User ID=([^;]+)") {
        $user = $matches[1]
        Write-Host "  ✅ Username: $user" -ForegroundColor Green
    }

    # Extract password
    if ($ConnectionString -match "Password=([^;]+)") {
        $pass = $matches[1]
        Write-Host "  ✅ Password: [HIDDEN]" -ForegroundColor Green
    }

    return @{
        Server   = $server
        Database = $database
        Username = $user
        Password = $pass
    }
}

function Update-EnvFile {
    param([hashtable]$ConnectionDetails)

    $envFile = Join-Path $PSScriptRoot ".." ".env"

    if (-not (Test-Path $envFile)) {
        Write-Host "❌ .env file not found at: $envFile" -ForegroundColor Red
        return $false
    }

    Write-Host "📝 Updating .env file..." -ForegroundColor Yellow

    $content = Get-Content $envFile -Raw

    # Update each value
    $content = $content -replace "(?m)^AZURE_SQL_SERVER=.*$", "AZURE_SQL_SERVER=$($ConnectionDetails.Server)"
    $content = $content -replace "(?m)^AZURE_SQL_DATABASE=.*$", "AZURE_SQL_DATABASE=$($ConnectionDetails.Database)"
    $content = $content -replace "(?m)^AZURE_SQL_USER=.*$", "AZURE_SQL_USER=$($ConnectionDetails.Username)"
    $content = $content -replace "(?m)^AZURE_SQL_PASSWORD=.*$", "AZURE_SQL_PASSWORD=$($ConnectionDetails.Password)"

    $content | Set-Content $envFile -Force

    Write-Host "✅ .env file updated successfully" -ForegroundColor Green
    return $true
}

function Test-AzureConnection {
    param([hashtable]$ConnectionDetails)

    Write-Host "🔗 Testing Azure SQL connection..." -ForegroundColor Yellow

    try {
        $connectionString = "Server=tcp:$($ConnectionDetails.Server),1433;Initial Catalog=$($ConnectionDetails.Database);Persist Security Info=False;User ID=$($ConnectionDetails.Username);Password=$($ConnectionDetails.Password);MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

        $connection = New-Object System.Data.SqlClient.SqlConnection
        $connection.ConnectionString = $connectionString
        $connection.Open()

        # Test query
        $command = $connection.CreateCommand()
        $command.CommandText = "SELECT @@VERSION as Version, DB_NAME() as DatabaseName"
        $reader = $command.ExecuteReader()

        if ($reader.Read()) {
            $version = $reader["Version"]
            $dbName = $reader["DatabaseName"]
            Write-Host "  ✅ Connection successful!" -ForegroundColor Green
            Write-Host "     Database: $dbName" -ForegroundColor Gray
            Write-Host "     SQL Server: $($version.ToString().Substring(0, 50))..." -ForegroundColor Gray
        }

        $connection.Close()
        return $true
    }
    catch {
        Write-Host "  ❌ Connection failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Main execution
$action = $null
if ($GetConnectionDetails) { $action = "GetConnectionDetails" }
elseif ($TestConnection) { $action = "TestConnection" }
elseif ($UpdateEnvFile) { $action = "UpdateEnvFile" }

switch ($action) {
    "GetConnectionDetails" {
        Show-AzureConnectionGuide
    }
    "TestConnection" {
        if ($ServerName -and $DatabaseName -and $Username -and $Password) {
            $details = @{
                Server   = $ServerName
                Database = $DatabaseName
                Username = $Username
                Password = $Password
            }
            Test-AzureConnection -ConnectionDetails $details
        }
        else {
            Write-Host "❌ Please provide all connection parameters:" -ForegroundColor Red
            Write-Host "   -ServerName, -DatabaseName, -Username, -Password" -ForegroundColor Yellow
        }
    }
    "UpdateEnvFile" {
        if ($ServerName -and $DatabaseName -and $Username -and $Password) {
            $details = @{
                Server   = $ServerName
                Database = $DatabaseName
                Username = $Username
                Password = $Password
            }
            if (Update-EnvFile -ConnectionDetails $details) {
                Write-Host ""
                Write-Host "🎉 Configuration updated! Next steps:" -ForegroundColor Green
                Write-Host "  1. Load environment: .\scripts\load-env.ps1 -Load" -ForegroundColor White
                Write-Host "  2. Test connection: .\scripts\load-env.ps1 -TestConnections" -ForegroundColor White
                Write-Host "  3. Run application: dotnet run --project WileyWidget/WileyWidget.csproj" -ForegroundColor White
            }
        }
        else {
            Write-Host "❌ Please provide all connection parameters:" -ForegroundColor Red
            Write-Host "   -ServerName, -DatabaseName, -Username, -Password" -ForegroundColor Yellow
        }
    }
    default {
        Write-Host "Azure SQL Database Setup for WileyWidget" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Usage:" -ForegroundColor Yellow
        Write-Host "  .\setup-azure-db.ps1 -GetConnectionDetails    # Show how to get connection details" -ForegroundColor White
        Write-Host "  .\setup-azure-db.ps1 -TestConnection -ServerName 'server' -DatabaseName 'db' -Username 'user' -Password 'pass'  # Test connection" -ForegroundColor White
        Write-Host "  .\setup-azure-db.ps1 -UpdateEnvFile -ServerName 'server' -DatabaseName 'db' -Username 'user' -Password 'pass'   # Update .env file" -ForegroundColor White
        Write-Host ""
        Write-Host "Example workflow:" -ForegroundColor Cyan
        Write-Host "  1. .\setup-azure-db.ps1 -GetConnectionDetails" -ForegroundColor White
        Write-Host "  2. Get your connection details from Azure Portal" -ForegroundColor White
        Write-Host "  3. .\setup-azure-db.ps1 -UpdateEnvFile -ServerName 'busbuddy-server.database.windows.net' -DatabaseName 'BusBuddy' -Username 'busbuddy_admin' -Password 'YourPassword123!'" -ForegroundColor White
        Write-Host "  4. .\scripts\load-env.ps1 -Load" -ForegroundColor White
        Write-Host "  5. .\scripts\load-env.ps1 -TestConnections" -ForegroundColor White
    }
}
