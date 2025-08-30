# Database Setup Script for WileyWidget
# This script helps set up SQL Server LocalDB for development

param(
    [switch]$CheckOnly,
    [switch]$Force
)

Write-Output "=== WileyWidget Database Setup ==="

# Check if SQL Server LocalDB is installed
function Test-SqlLocalDB {
    try {
        & sqllocaldb info 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Output "✅ SQL Server LocalDB is installed"
            return $true
        }
        else {
            Write-Output "❌ SQL Server LocalDB is not installed or not in PATH"
            return $false
        }
    }
    catch {
        Write-Output "❌ SQL Server LocalDB is not installed or not accessible"
        return $false
    }
}

# Check LocalDB instances
function Get-LocalDBInstance {
    try {
        $instances = & sqllocaldb info
        Write-Output "Available LocalDB instances:"
        $instances | ForEach-Object { Write-Output "  - $_" }
        return $instances
    }
    catch {
        Write-Output "❌ Could not retrieve LocalDB instances"
        return $null
    }
}

# Test database connectivity
function Test-DatabaseConnection {
    param([string]$ConnectionString)

    Write-Output "Testing database connection..."

    try {
        $connection = New-Object System.Data.SqlClient.SqlConnection
        $connection.ConnectionString = $ConnectionString
        $connection.Open()
        Write-Output "✅ Database connection successful"
        $connection.Close()
        return $true
    }
    catch {
        Write-Output "❌ Database connection failed: $($_.Exception.Message)"
        return $false
    }
}

# Main execution
if ($CheckOnly) {
    Write-Output "Running in check-only mode..."
}

# Check LocalDB installation
$localDBInstalled = Test-SqlLocalDB

if (-not $localDBInstalled) {
    Write-Output ""
    Write-Output "🔧 SQL Server LocalDB Installation Required"
    Write-Output ""
    Write-Output "Option 1 - Install via SQL Server Express (Recommended):"
    Write-Output "  1. Download from: https://www.microsoft.com/en-us/sql-server/sql-server-downloads"
    Write-Output "  2. Choose 'SQL Server Express' (free edition)"
    Write-Output "  3. Select 'Basic' installation"
    Write-Output "  4. Use default instance name (SQLEXPRESS)"
    Write-Output ""
    Write-Output "Option 2 - Install via Chocolatey:"
    Write-Output "  choco install sql-server-localdb -y"
    Write-Output ""
    Write-Output "After installation, run this script again to verify setup."
    exit 1
}

# Get LocalDB instances
$instances = Get-LocalDBInstances

# Check for MSSQLLocalDB instance
$mssqlLocalDBExists = $instances -contains "MSSQLLocalDB"

if (-not $mssqlLocalDBExists -or $Force) {
    if ($Force -and $mssqlLocalDBExists) {
        Write-Output ""
        Write-Output "🔄 Force mode: Recreating MSSQLLocalDB instance..."
        try {
            & sqllocaldb stop "MSSQLLocalDB" 2>$null
            & sqllocaldb delete "MSSQLLocalDB" 2>$null
            Write-Output "🗑️  Old instance removed"
        }
        catch {
            Write-Output "⚠️  Could not remove old instance (may not exist): $($_.Exception.Message)"
        }
    }

    Write-Output ""
    Write-Output "⚠️  MSSQLLocalDB instance not found"
    Write-Output "Creating MSSQLLocalDB instance..."

    try {
        & sqllocaldb create "MSSQLLocalDB"
        Write-Output "✅ MSSQLLocalDB instance created successfully"
    }
    catch {
        Write-Output "❌ Failed to create MSSQLLocalDB instance: $($_.Exception.Message)"
        exit 1
    }
}
else {
    Write-Output "✅ MSSQLLocalDB instance exists"
}

# Start the LocalDB instance if not running
Write-Output ""
Write-Output "Starting MSSQLLocalDB instance..."
try {
    $startOutput = & sqllocaldb start "MSSQLLocalDB" 2>&1
    if ($startOutput -match "LocalDB instance.*started") {
        Write-Output "✅ MSSQLLocalDB instance started successfully"
    }
    else {
        Write-Output "ℹ️  MSSQLLocalDB instance may already be running"
    }
}
catch {
    Write-Output "❌ Failed to start MSSQLLocalDB instance: $($_.Exception.Message)"
}

# Test database connection
Write-Output ""
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;"

if (Test-DatabaseConnection -ConnectionString $connectionString) {
    Write-Output ""
    Write-Output "🎉 Database setup completed successfully!"
    Write-Output ""
    Write-Output "Next steps:"
    Write-Output "  1. Run the application: dotnet run --project WileyWidget/WileyWidget.csproj"
    Write-Output "  2. The database will be created automatically on first run"
    Write-Output "  3. Check logs at: ./logs"
}
else {
    Write-Output ""
    Write-Output "❌ Database connection test failed"
    Write-Output ""
    Write-Output "Troubleshooting:"
    Write-Output "  1. Ensure you're running as Administrator"
    Write-Output "  2. Check Windows Firewall settings"
    Write-Output "  3. Verify LocalDB installation: sqllocaldb info"
    Write-Output "  4. Try manual connection: sqlcmd -S ""(localdb)\MSSQLLocalDB"" -Q ""SELECT @@VERSION"""
    exit 1
}
