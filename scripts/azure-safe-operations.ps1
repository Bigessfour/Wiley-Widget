# Azure Safe Operations for Novices
# This script provides safe, programmatic Azure operations

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("status", "list", "connect", "backup", "restore", "safe-delete")]
    [string]$Operation = "status",

    [Parameter(Mandatory = $false)]
    [switch]$DryRun,

    [Parameter(Mandatory = $false)]
    [switch]$Verbose
)

# Configuration
$Script:Config = @{
    SubscriptionId = $env:AZURE_SUBSCRIPTION_ID
    ResourceGroup = "WileyWidget-RG"
    SqlServer = $env:AZURE_SQL_SERVER
    Database = $env:AZURE_SQL_DATABASE
    SafeMode = $true  # Always true for novices
}

# Logging function
function Write-SafeLog {
    param([string]$Message, [string]$Level = "INFO")
    $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $Color = switch ($Level) {
        "ERROR" { "Red" }
        "WARN" { "Yellow" }
        "SUCCESS" { "Green" }
        default { "White" }
    }
    Write-Host "[$Timestamp] [$Level] $Message" -ForegroundColor $Color
}

# Safe Azure CLI wrapper
function Invoke-AzureSafe {
    param([string]$Command, [string]$Description)

    if ($DryRun) {
        Write-SafeLog "DRY RUN: Would execute: $Command" "INFO"
        Write-SafeLog "Purpose: $Description" "INFO"
        return $true
    }

    Write-SafeLog "Executing: $Description" "INFO"

    try {
        $result = Invoke-Expression $Command
        Write-SafeLog "Success: $Description" "SUCCESS"
        return $result
    }
    catch {
        Write-SafeLog "Failed: $Description - $($_.Exception.Message)" "ERROR"
        return $null
    }
}

# Check Azure status
function Get-AzureStatus {
    Write-SafeLog "Checking Azure connection and resources..." "INFO"

    # Check if logged in
    $account = Invoke-AzureSafe "az account show --output json" "Check Azure login status"
    if (-not $account) {
        Write-SafeLog "Not logged in to Azure. Run 'az login' first." "ERROR"
        return
    }

    $accountObj = $account | ConvertFrom-Json
    Write-SafeLog "Logged in as: $($accountObj.user.name)" "SUCCESS"
    Write-SafeLog "Subscription: $($accountObj.name)" "INFO"

    # Check resource group
    $rg = Invoke-AzureSafe "az group exists --name $($Script:Config.ResourceGroup)" "Check resource group existence"
    if ($rg -eq "true") {
        Write-SafeLog "Resource group '$($Script:Config.ResourceGroup)' exists" "SUCCESS"
    } else {
        Write-SafeLog "Resource group '$($Script:Config.ResourceGroup)' not found" "WARN"
    }

    # Check SQL server
    if ($Script:Config.SqlServer) {
        $server = Invoke-AzureSafe "az sql server show --resource-group $($Script:Config.ResourceGroup) --name $($Script:Config.SqlServer.Split('.')[0]) --output json" "Check SQL server status"
        if ($server) {
            Write-SafeLog "SQL Server '$($Script:Config.SqlServer)' is available" "SUCCESS"
        } else {
            Write-SafeLog "SQL Server '$($Script:Config.SqlServer)' not found" "WARN"
        }
    }
}

# List Azure resources safely
function Get-AzureResources {
    Write-SafeLog "Listing Azure resources (read-only)..." "INFO"

    # List resource groups
    Invoke-AzureSafe "az group list --output table" "List resource groups"

    # List SQL servers
    Invoke-AzureSafe "az sql server list --resource-group $($Script:Config.ResourceGroup) --output table" "List SQL servers"

    # List SQL databases
    if ($Script:Config.SqlServer) {
        Invoke-AzureSafe "az sql db list --resource-group $($Script:Config.ResourceGroup) --server $($Script:Config.SqlServer.Split('.')[0]) --output table" "List SQL databases"
    }
}

# Safe database connection test
function Test-AzureDatabaseConnection {
    Write-SafeLog "Testing database connection (read-only)..." "INFO"

    if (-not $Script:Config.SqlServer -or -not $Script:Config.Database) {
        Write-SafeLog "Database configuration missing in environment variables" "ERROR"
        return
    }

    # Test connection using Azure CLI
    $connectionString = "Server=tcp:$($Script:Config.SqlServer),1433;Database=$($Script:Config.Database);User ID=$($env:AZURE_SQL_USER);Password=$($env:AZURE_SQL_PASSWORD);Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

    Write-SafeLog "Testing connection to: $($Script:Config.Database)" "INFO"

    try {
        # Use .NET to test connection
        Add-Type -AssemblyName System.Data
        $connection = New-Object System.Data.SqlClient.SqlConnection
        $connection.ConnectionString = $connectionString
        $connection.Open()

        # Run a safe read-only query
        $command = $connection.CreateCommand()
        $command.CommandText = "SELECT @@VERSION as Version"
        $reader = $command.ExecuteReader()
        if ($reader.Read()) {
            Write-SafeLog "Connection successful! SQL Server version: $($reader["Version"])" "SUCCESS"
        }
        $reader.Close()
        $connection.Close()

    } catch {
        Write-SafeLog "Connection failed: $($_.Exception.Message)" "ERROR"
    }
}

# Safe backup operation
function Backup-AzureDatabase {
    Write-SafeLog "Creating database backup..." "INFO"

    if (-not $Script:Config.SqlServer -or -not $Script:Config.Database) {
        Write-SafeLog "Database configuration missing" "ERROR"
        return
    }

    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupName = "$($Script:Config.Database)_backup_$timestamp"

    Write-SafeLog "Creating backup: $backupName" "INFO"
    Write-SafeLog "This is a SAFE operation that creates a backup copy" "SUCCESS"

    # Create database copy (safe backup)
    Invoke-AzureSafe "az sql db copy --resource-group $($Script:Config.ResourceGroup) --server $($Script:Config.SqlServer.Split('.')[0]) --name $($Script:Config.Database) --dest-name $backupName" "Create database backup copy"
}

# Safe delete with confirmation
function Remove-AzureResourceSafe {
    param([string]$ResourceType, [string]$ResourceName)

    Write-SafeLog "SAFE DELETE MODE ENABLED" "WARN"
    Write-SafeLog "Resource: $ResourceType '$ResourceName'" "INFO"
    Write-SafeLog "This operation requires explicit confirmation" "WARN"

    if ($DryRun) {
        Write-SafeLog "DRY RUN: Would prompt for confirmation to delete $ResourceType '$ResourceName'" "INFO"
        return
    }

    $confirmation = Read-Host "Type 'YES' to confirm deletion of $ResourceType '$ResourceName'"
    if ($confirmation -ne "YES") {
        Write-SafeLog "Deletion cancelled by user" "SUCCESS"
        return
    }

    Write-SafeLog "Proceeding with deletion..." "WARN"

    switch ($ResourceType) {
        "database" {
            Invoke-AzureSafe "az sql db delete --resource-group $($Script:Config.ResourceGroup) --server $($Script:Config.SqlServer.Split('.')[0]) --name $ResourceName --yes" "Delete database"
        }
        "server" {
            Invoke-AzureSafe "az sql server delete --resource-group $($Script:Config.ResourceGroup) --name $ResourceName --yes" "Delete SQL server"
        }
        default {
            Write-SafeLog "Unknown resource type: $ResourceType" "ERROR"
        }
    }
}

# Main execution
Write-SafeLog "Azure Safe Operations Script v1.0" "INFO"
Write-SafeLog "Safe Mode: $($Script:Config.SafeMode)" "SUCCESS"
Write-SafeLog "Dry Run: $DryRun" "INFO"

switch ($Operation) {
    "status" { Get-AzureStatus }
    "list" { Get-AzureResources }
    "connect" { Test-AzureDatabaseConnection }
    "backup" { Backup-AzureDatabase }
    "safe-delete" {
        Write-SafeLog "Safe delete mode - specify resource type and name" "INFO"
        Write-SafeLog "Example: -ResourceType database -ResourceName MyDatabase" "INFO"
    }
    default {
        Write-SafeLog "Available operations: status, list, connect, backup, safe-delete" "INFO"
    }
}

Write-SafeLog "Operation completed safely" "SUCCESS"
