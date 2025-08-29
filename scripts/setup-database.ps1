# Database Setup Script for WileyWidget
# This script helps set up SQL Server LocalDB for development

param(
    [switch]$CheckOnly,
    [switch]$Force
)

Write-Host "=== WileyWidget Database Setup ===" -ForegroundColor Cyan

# Check if SQL Server LocalDB is installed
function Test-SqlLocalDB {
    try {
        $output = & sqllocaldb info 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ SQL Server LocalDB is installed" -ForegroundColor Green
            return $true
        }
        else {
            Write-Host "❌ SQL Server LocalDB is not installed or not in PATH" -ForegroundColor Red
            return $false
        }
    }
    catch {
        Write-Host "❌ SQL Server LocalDB is not installed or not accessible" -ForegroundColor Red
        return $false
    }
}

# Check LocalDB instances
function Get-LocalDBInstance {
    try {
        $instances = & sqllocaldb info
        Write-Host "Available LocalDB instances:" -ForegroundColor Yellow
        $instances | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
        return $instances
    }
    catch {
        Write-Host "❌ Could not retrieve LocalDB instances" -ForegroundColor Red
        return $null
    }
}

# Test database connectivity
function Test-DatabaseConnection {
    param([string]$ConnectionString)

    Write-Host "Testing database connection..." -ForegroundColor Yellow

    try {
        $connection = New-Object System.Data.SqlClient.SqlConnection
        $connection.ConnectionString = $ConnectionString
        $connection.Open()
        Write-Host "✅ Database connection successful" -ForegroundColor Green
        $connection.Close()
        return $true
    }
    catch {
        Write-Host "❌ Database connection failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Main execution
if ($CheckOnly) {
    Write-Host "Running in check-only mode..." -ForegroundColor Yellow
}

# Check LocalDB installation
$localDBInstalled = Test-SqlLocalDB

if (-not $localDBInstalled) {
    Write-Host ""
    Write-Host "🔧 SQL Server LocalDB Installation Required" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Option 1 - Install via SQL Server Express (Recommended):" -ForegroundColor Cyan
    Write-Host "  1. Download from: https://www.microsoft.com/en-us/sql-server/sql-server-downloads" -ForegroundColor White
    Write-Host "  2. Choose 'SQL Server Express' (free edition)" -ForegroundColor White
    Write-Host "  3. Select 'Basic' installation" -ForegroundColor White
    Write-Host "  4. Use default instance name (SQLEXPRESS)" -ForegroundColor White
    Write-Host ""
    Write-Host "Option 2 - Install via Chocolatey:" -ForegroundColor Cyan
    Write-Host "  choco install sql-server-localdb -y" -ForegroundColor White
    Write-Host ""
    Write-Host "After installation, run this script again to verify setup." -ForegroundColor Yellow
    exit 1
}

# Get LocalDB instances
$instances = Get-LocalDBInstances

# Check for MSSQLLocalDB instance
$mssqlLocalDBExists = $instances -contains "MSSQLLocalDB"

if (-not $mssqlLocalDBExists) {
    Write-Host ""
    Write-Host "⚠️  MSSQLLocalDB instance not found" -ForegroundColor Yellow
    Write-Host "Creating MSSQLLocalDB instance..." -ForegroundColor Cyan

    try {
        & sqllocaldb create "MSSQLLocalDB"
        Write-Host "✅ MSSQLLocalDB instance created successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ Failed to create MSSQLLocalDB instance: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "✅ MSSQLLocalDB instance exists" -ForegroundColor Green
}

# Start the LocalDB instance if not running
Write-Host ""
Write-Host "Starting MSSQLLocalDB instance..." -ForegroundColor Cyan
try {
    $startOutput = & sqllocaldb start "MSSQLLocalDB" 2>&1
    if ($startOutput -match "LocalDB instance.*started") {
        Write-Host "✅ MSSQLLocalDB instance started successfully" -ForegroundColor Green
    }
    else {
        Write-Host "ℹ️  MSSQLLocalDB instance may already be running" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "❌ Failed to start MSSQLLocalDB instance: $($_.Exception.Message)" -ForegroundColor Red
}

# Test database connection
Write-Host ""
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;"

if (Test-DatabaseConnection -ConnectionString $connectionString) {
    Write-Host ""
    Write-Host "🎉 Database setup completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Run the application: dotnet run --project WileyWidget/WileyWidget.csproj" -ForegroundColor White
    Write-Host "  2. The database will be created automatically on first run" -ForegroundColor White
    Write-Host "  3. Check logs at: %APPDATA%\WileyWidget\logs" -ForegroundColor White
}
else {
    Write-Host ""
    Write-Host "❌ Database connection test failed" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Ensure you're running as Administrator" -ForegroundColor White
    Write-Host "  2. Check Windows Firewall settings" -ForegroundColor White
    Write-Host "  3. Verify LocalDB installation: sqllocaldb info" -ForegroundColor White
    Write-Host "  4. Try manual connection: sqlcmd -S ""(localdb)\MSSQLLocalDB"" -Q ""SELECT @@VERSION""" -ForegroundColor White
    exit 1
}
