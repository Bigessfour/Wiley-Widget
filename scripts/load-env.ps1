# Secure Environment Variable Loader for WileyWidget
# This script loads environment variables from .env file securely

param(
    [switch]$Load,
    [switch]$Unload,
    [switch]$Status,
    [switch]$TestConnections
)

$envFile = Join-Path $PSScriptRoot ".." ".env"

function Load-EnvironmentVariables {
    if (-not (Test-Path $envFile)) {
        Write-Host "❌ .env file not found at: $envFile" -ForegroundColor Red
        Write-Host "Create a .env file with your configuration variables." -ForegroundColor Yellow
        return $false
    }

    Write-Host "🔐 Loading environment variables from .env file..." -ForegroundColor Cyan

    $loadedCount = 0
    $errorCount = 0

    Get-Content $envFile | ForEach-Object {
        $line = $_.Trim()

        # Skip comments and empty lines
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
            return
        }

        # Parse KEY=VALUE
        if ($line -match "^([^=]+)=(.*)$") {
            $key = $matches[1].Trim()
            $value = $matches[2].Trim()

            # Remove quotes if present
            if ($value.StartsWith('"') -and $value.EndsWith('"')) {
                $value = $value.Substring(1, $value.Length - 2)
            } elseif ($value.StartsWith("'") -and $value.EndsWith("'")) {
                $value = $value.Substring(1, $value.Length - 2)
            }

            try {
                [System.Environment]::SetEnvironmentVariable($key, $value, "Process")
                $loadedCount++
                Write-Host "  ✅ $key" -ForegroundColor Green
            } catch {
                Write-Host "  ❌ Failed to set $key : $($_.Exception.Message)" -ForegroundColor Red
                $errorCount++
            }
        }
    }

    Write-Host ""
    Write-Host "📊 Environment variables loaded: $loadedCount" -ForegroundColor Green
    if ($errorCount -gt 0) {
        Write-Host "❌ Errors: $errorCount" -ForegroundColor Red
    }

    return $loadedCount -gt 0
}

function Unload-EnvironmentVariables {
    Write-Host "🧹 Unloading environment variables..." -ForegroundColor Cyan

    if (-not (Test-Path $envFile)) {
        Write-Host "❌ .env file not found" -ForegroundColor Red
        return
    }

    $unloadedCount = 0

    Get-Content $envFile | ForEach-Object {
        $line = $_.Trim()

        # Skip comments and empty lines
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
            return
        }

        # Parse KEY=VALUE
        if ($line -match "^([^=]+)=(.*)$") {
            $key = $matches[1].Trim()

            try {
                [System.Environment]::SetEnvironmentVariable($key, $null, "Process")
                $unloadedCount++
                Write-Host "  ✅ $key unloaded" -ForegroundColor Green
            } catch {
                Write-Host "  ❌ Failed to unload $key : $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }

    Write-Host ""
    Write-Host "📊 Environment variables unloaded: $unloadedCount" -ForegroundColor Green
}

function Show-EnvironmentStatus {
    Write-Host "📋 Environment Variables Status" -ForegroundColor Cyan
    Write-Host "=" * 50

    # Check .env file
    if (Test-Path $envFile) {
        Write-Host "✅ .env file found" -ForegroundColor Green
    } else {
        Write-Host "❌ .env file not found" -ForegroundColor Red
        Write-Host "   Create .env file with your configuration" -ForegroundColor Yellow
        return
    }

    # Check key environment variables
    $keyVars = @(
        "AZURE_SQL_SERVER",
        "AZURE_SQL_DATABASE",
        "AZURE_SQL_USER",
        "SYNCFUSION_LICENSE_KEY",
        "QBO_CLIENT_ID"
    )

    Write-Host ""
    Write-Host "🔍 Key Variables:" -ForegroundColor Yellow

    foreach ($var in $keyVars) {
        $value = [System.Environment]::GetEnvironmentVariable($var, "Process")
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            if ($value -like "*CHANGE_THIS*" -or $value -like "*YOUR_*") {
                Write-Host "  ⚠️  $var = [PLACEHOLDER VALUE]" -ForegroundColor Yellow
            } else {
                Write-Host "  ✅ $var = [SET]" -ForegroundColor Green
            }
        } else {
            Write-Host "  ❌ $var = [NOT SET]" -ForegroundColor Red
        }
    }

    # Check Azure connection string
    $azureConn = [System.Environment]::GetEnvironmentVariable("CONNECTIONSTRINGS__AZURECONNECTION", "Process")
    if (-not [string]::IsNullOrWhiteSpace($azureConn)) {
        Write-Host "  ✅ CONNECTIONSTRINGS__AZURECONNECTION = [SET]" -ForegroundColor Green
    } else {
        Write-Host "  ❌ CONNECTIONSTRINGS__AZURECONNECTION = [NOT SET]" -ForegroundColor Red
    }
}

function Test-Connections {
    Write-Host "🔗 Testing Connections" -ForegroundColor Cyan
    Write-Host "=" * 50

    # Test Azure SQL Connection
    $server = [System.Environment]::GetEnvironmentVariable("AZURE_SQL_SERVER", "Process")
    $database = [System.Environment]::GetEnvironmentVariable("AZURE_SQL_DATABASE", "Process")
    $user = [System.Environment]::GetEnvironmentVariable("AZURE_SQL_USER", "Process")
    $password = [System.Environment]::GetEnvironmentVariable("AZURE_SQL_PASSWORD", "Process")

    if ($server -and $database -and $user -and $password) {
        Write-Host "Testing Azure SQL connection..." -ForegroundColor Yellow

        try {
            $connectionString = "Server=tcp:$server,1433;Initial Catalog=$database;Persist Security Info=False;User ID=$user;Password=$password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

            $connection = New-Object System.Data.SqlClient.SqlConnection
            $connection.ConnectionString = $connectionString
            $connection.Open()

            # Test query
            $command = $connection.CreateCommand()
            $command.CommandText = "SELECT @@VERSION as Version"
            $reader = $command.ExecuteReader()
            if ($reader.Read()) {
                $version = $reader["Version"]
                Write-Host "  ✅ Azure SQL connection successful" -ForegroundColor Green
                Write-Host "     SQL Server version: $($version.ToString().Substring(0, 50))..." -ForegroundColor Gray
            }

            $connection.Close()
        } catch {
            Write-Host "  ❌ Azure SQL connection failed: $($_.Exception.Message)" -ForegroundColor Red
        }
    } else {
        Write-Host "❌ Azure SQL configuration incomplete" -ForegroundColor Red
        Write-Host "   Missing: $(($server, $database, $user, $password | Where-Object { -not $_ }) -join ', ')" -ForegroundColor Yellow
    }

    # Test Syncfusion License
    $syncfusionKey = [System.Environment]::GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "Process")
    if (-not [string]::IsNullOrWhiteSpace($syncfusionKey) -and $syncfusionKey -notlike "*YOUR_*") {
        Write-Host "  ✅ Syncfusion license key configured" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Syncfusion license key not configured" -ForegroundColor Red
    }
}

# Main execution
# Determines which action to perform based on provided parameters and executes the corresponding function.
$action = $null
if ($Load) { $action = "Load" }
elseif ($Unload) { $action = "Unload" }
elseif ($Status) { $action = "Status" }
elseif ($TestConnections) { $action = "TestConnections" }

if ($action -eq "Load") {
    Load-EnvironmentVariables
} elseif ($action -eq "Unload") {
    Unload-EnvironmentVariables
} elseif ($action -eq "Status") {
    Show-EnvironmentStatus
} elseif ($action -eq "TestConnections") {
    # Load variables first, then test
    if (Load-EnvironmentVariables) {
        Write-Host ""
        Test-Connections
    }
}
else {
    Write-Host "WileyWidget Environment Manager" -ForegroundColor Cyan
    Write-Host "Usage:" -ForegroundColor Yellow
    Write-Host "  .\load-env.ps1 -Load          # Load environment variables" -ForegroundColor White
    Write-Host "  .\load-env.ps1 -Unload        # Unload environment variables" -ForegroundColor White
    Write-Host "  .\load-env.ps1 -Status        # Show current status" -ForegroundColor White
    Write-Host "  .\load-env.ps1 -TestConnections # Test all connections" -ForegroundColor White
    Write-Host ""
    Write-Host "Example:" -ForegroundColor Yellow
    Write-Host "  .\load-env.ps1 -Load -TestConnections" -ForegroundColor White
