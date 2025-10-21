# Simple database check script
param(
    [string]$Server = ".\SQLEXPRESS",
    [string]$Database = "WileyWidgetDev"
)

$ErrorActionPreference = "Stop"

try {
    $connectionString = "Server=$Server;Database=$Database;Trusted_Connection=True;Connection Timeout=30;"
    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = $connectionString
    $connection.Open()

    # Check database info
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT name, state_desc, recovery_model_desc FROM sys.databases WHERE name = '$Database'"
    $reader = $command.ExecuteReader()
    if ($reader.Read()) {
        [PSCustomObject]@{
            Name          = $reader['name']
            State         = $reader['state_desc']
            RecoveryModel = $reader['recovery_model_desc']
            Resource      = 'Database'
        } | Write-Output
    }
    $reader.Close()

    # Check tables
    $command.CommandText = "SELECT COUNT(*) as TableCount FROM sys.tables"
    $reader = $command.ExecuteReader()
    if ($reader.Read()) {
        [PSCustomObject]@{
            Resource = 'Tables'
            Count    = $reader['TableCount']
        } | Write-Output
    }
    $reader.Close()

    # Check if stats exist
    $command.CommandText = "SELECT COUNT(*) as StatsCount FROM sys.stats WHERE auto_created = 0"
    $reader = $command.ExecuteReader()
    if ($reader.Read()) {
        [PSCustomObject]@{
            Resource = 'ManualStatistics'
            Count    = $reader['StatsCount']
        } | Write-Output
    }
    $reader.Close()

    # Check indexes
    $command.CommandText = "SELECT COUNT(*) as IndexCount FROM sys.indexes WHERE type > 0"
    $reader = $command.ExecuteReader()
    if ($reader.Read()) {
        [PSCustomObject]@{
            Resource = 'Indexes'
            Count    = $reader['IndexCount']
        } | Write-Output
    }
    $reader.Close()

    $connection.Close()
    Write-Information "Database check completed successfully" -InformationAction Continue
}
catch {
    Write-Error $_.Exception.Message
}
