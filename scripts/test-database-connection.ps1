# Azure Database Connectivity Test Script
# Tests Azure SQL Database connection and basic operations

param(
    [Parameter(Mandatory = $false)]
    [switch]$UseLocalDB,

    [Parameter(Mandatory = $false)]
    [string]$ConnectionString,

    [Parameter(Mandatory = $false)]
    [switch]$CreateTestData
)

# Configuration
$ScriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptPath
$EnvFile = Join-Path $ProjectRoot ".env"

Write-Host "🧪 WileyWidget Azure Database Connectivity Test" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

# Function to load environment variables
function Get-EnvironmentVariable {
    if (Test-Path $EnvFile) {
        $envVars = @{}
        Get-Content $EnvFile | Where-Object { $_ -match '^[^#].*=.*' } | ForEach-Object {
            $key, $value = $_ -split '=', 2
            $envVars[$key.Trim()] = $value.Trim()
        }
        return $envVars
    }
    return $null
}

# Function to test Azure SQL connection
function Test-AzureSQLConnection {
    param([string]$ConnectionString)

    Write-Host "🔍 Testing Azure SQL Database connection..." -ForegroundColor Yellow

    try {
        $connection = New-Object System.Data.SqlClient.SqlConnection
        $connection.ConnectionString = $ConnectionString
        $connection.Open()

        if ($connection.State -eq 'Open') {
            Write-Host "✅ Successfully connected to Azure SQL Database" -ForegroundColor Green

            # Get database info
            $command = $connection.CreateCommand()
            $command.CommandText = "SELECT @@VERSION as Version, DB_NAME() as DatabaseName, CURRENT_USER as CurrentUser"
            $reader = $command.ExecuteReader()

            if ($reader.Read()) {
                Write-Host "📊 Database Information:" -ForegroundColor Cyan
                Write-Host "   • Server Version: $($reader["Version"])" -ForegroundColor White
                Write-Host "   • Database: $($reader["DatabaseName"])" -ForegroundColor White
                Write-Host "   • User: $($reader["CurrentUser"])" -ForegroundColor White
            }
            $reader.Close()

            $connection.Close()
            return $true
        }
    }
    catch {
        Write-Host "❌ Connection failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }

    return $false
}

# Function to test LocalDB connection
function Test-LocalDBConnection {
    Write-Host "🔍 Testing LocalDB connection..." -ForegroundColor Yellow

    $localDBConnectionString = "Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Database=WileyWidgetDB;"

    try {
        $connection = New-Object System.Data.SqlClient.SqlConnection
        $connection.ConnectionString = $localDBConnectionString
        $connection.Open()

        if ($connection.State -eq 'Open') {
            Write-Host "✅ Successfully connected to LocalDB" -ForegroundColor Green

            # Get database info
            $command = $connection.CreateCommand()
            $command.CommandText = "SELECT @@VERSION as Version, DB_NAME() as DatabaseName"
            $reader = $command.ExecuteReader()

            if ($reader.Read()) {
                Write-Host "📊 Database Information:" -ForegroundColor Cyan
                Write-Host "   • Server Version: $($reader["Version"])" -ForegroundColor White
                Write-Host "   • Database: $($reader["DatabaseName"])" -ForegroundColor White
            }
            $reader.Close()

            $connection.Close()
            return $true
        }
    }
    catch {
        Write-Host "❌ LocalDB connection failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "💡 Make sure LocalDB is installed and running" -ForegroundColor Yellow
        return $false
    }

    return $false
}

# Function to create test tables and data
function New-TestDatabaseSchema {
    param([string]$ConnectionString)

    Write-Host "🏗️  Creating test database schema..." -ForegroundColor Yellow

    try {
        $connection = New-Object System.Data.SqlClient.SqlConnection
        $connection.ConnectionString = $ConnectionString
        $connection.Open()

        $commands = @(
            @"
CREATE TABLE IF NOT EXISTS Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    Email NVARCHAR(100) NOT NULL UNIQUE,
    CreatedDate DATETIME2 DEFAULT GETUTCDATE(),
    IsActive BIT DEFAULT 1
)
"@,
            @"
CREATE TABLE IF NOT EXISTS Widgets (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500),
    Price DECIMAL(10,2),
    CreatedDate DATETIME2 DEFAULT GETUTCDATE(),
    UserId INT,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
)
"@,
            @"
INSERT INTO Users (Username, Email) VALUES
('testuser1', 'test1@example.com'),
('testuser2', 'test2@example.com')
"@,
            @"
INSERT INTO Widgets (Name, Description, Price, UserId) VALUES
('Test Widget 1', 'A test widget for demonstration', 29.99, 1),
('Test Widget 2', 'Another test widget', 49.99, 2)
"@
        )

        foreach ($sql in $commands) {
            $command = $connection.CreateCommand()
            $command.CommandText = $sql
            $command.ExecuteNonQuery() | Out-Null
        }

        Write-Host "✅ Test database schema created successfully" -ForegroundColor Green
        $connection.Close()
        return $true

    }
    catch {
        Write-Host "❌ Failed to create test schema: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Function to test database operations
function Test-DatabaseOperation {
    param([string]$ConnectionString)

    Write-Host "🔄 Testing database operations..." -ForegroundColor Yellow

    try {
        $connection = New-Object System.Data.SqlClient.SqlConnection
        $connection.ConnectionString = $ConnectionString
        $connection.Open()

        # Test SELECT
        $command = $connection.CreateCommand()
        $command.CommandText = "SELECT COUNT(*) as UserCount FROM Users"
        $userCount = $command.ExecuteScalar()
        Write-Host "   • Found $userCount users in database" -ForegroundColor White

        # Test INSERT
        $command.CommandText = "INSERT INTO Users (Username, Email) VALUES ('testuser3', 'test3@example.com')"
        $rowsAffected = $command.ExecuteNonQuery()
        Write-Host "   • Inserted $rowsAffected new user" -ForegroundColor White

        # Test UPDATE
        $command.CommandText = "UPDATE Users SET IsActive = 0 WHERE Username = 'testuser3'"
        $rowsAffected = $command.ExecuteNonQuery()
        Write-Host "   • Updated $rowsAffected user" -ForegroundColor White

        # Test DELETE
        $command.CommandText = "DELETE FROM Users WHERE Username = 'testuser3'"
        $rowsAffected = $command.ExecuteNonQuery()
        Write-Host "   • Deleted $rowsAffected user" -ForegroundColor White

        $connection.Close()
        Write-Host "✅ All database operations completed successfully" -ForegroundColor Green
        return $true

    }
    catch {
        Write-Host "❌ Database operations failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Function to test Azure CLI connectivity
function Test-AzureCLIConnectivity {
    Write-Host "🔧 Testing Azure CLI connectivity..." -ForegroundColor Yellow

    try {
        $account = az account show 2>$null | ConvertFrom-Json
        if ($account) {
            Write-Host "✅ Azure CLI is connected" -ForegroundColor Green
            Write-Host "   • Subscription: $($account.name)" -ForegroundColor White
            Write-Host "   • User: $($account.user.name)" -ForegroundColor White
            return $true
        }
    }
    catch {
        Write-Host "❌ Azure CLI is not connected" -ForegroundColor Red
        Write-Host "💡 Run 'az login' to connect to Azure" -ForegroundColor Yellow
        return $false
    }

    return $false
}

# Main execution
try {
    $envVars = Get-EnvironmentVariables

    if ($UseLocalDB) {
        # Test LocalDB connection
        if (Test-LocalDBConnection) {
            Write-Host "`n🎉 LocalDB connectivity test completed successfully!" -ForegroundColor Green
        }
        else {
            Write-Host "`n❌ LocalDB connectivity test failed" -ForegroundColor Red
            exit 1
        }
    }
    else {
        # Test Azure connectivity
        $connectionString = $ConnectionString

        if ([string]::IsNullOrEmpty($connectionString) -and $envVars) {
            $connectionString = $envVars["AZURE_SQL_CONNECTION_STRING"]
        }

        if ([string]::IsNullOrEmpty($connectionString)) {
            Write-Host "❌ No connection string provided. Use -ConnectionString parameter or set AZURE_SQL_CONNECTION_STRING in .env file" -ForegroundColor Red
            exit 1
        }

        # Test Azure SQL connection
        if (Test-AzureSQLConnection -ConnectionString $connectionString) {

            # Create test data if requested
            if ($CreateTestData) {
                New-TestDatabaseSchema -ConnectionString $connectionString
            }

            # Test database operations
            Test-DatabaseOperations -ConnectionString $connectionString

            Write-Host "`n🎉 Azure SQL Database connectivity test completed successfully!" -ForegroundColor Green
        }
        else {
            Write-Host "`n❌ Azure SQL Database connectivity test failed" -ForegroundColor Red
            exit 1
        }
    }

    # Test Azure CLI connectivity
    Test-AzureCLIConnectivity

}
catch {
    Write-Host "❌ An error occurred: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`n✨ Database connectivity testing complete!" -ForegroundColor Green
