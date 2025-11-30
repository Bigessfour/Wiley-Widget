<#
Setup localdb instance and apply EF Core migrations for WileyWidget.

Usage examples:
  # Create/start default (MSSQLLocalDB) instance and migrate
  pwsh ./scripts/maintenance/setup-localdb-and-migrate.ps1

  # Use a custom instance or database name
  pwsh ./scripts/maintenance/setup-localdb-and-migrate.ps1 -InstanceName "MSSQLLocalDB" -DatabaseName "WileyWidget"

#>

param(
    [string]$InstanceName = 'MSSQLLocalDB',
    [string]$DatabaseName = 'WileyWidget',
    [string]$MigrationProject = 'src/WileyWidget.Data',
    [string]$StartupProject = 'src/WileyWidget.Data'
)

Write-Host "Preparing LocalDB instance '$InstanceName' and database '$DatabaseName'..."

$sqllocaldb = Get-Command sqllocaldb -ErrorAction SilentlyContinue
if (-not $sqllocaldb) {
    Write-Warning 'sqllocaldb.exe not found on PATH. LocalDB is part of Visual Studio / SQL Server Express installs. Install LocalDB or provide a remote SQL Server connection string in appsettings.json.'
    exit 1
}

# Ensure instance exists and is running
$instances = & sqllocaldb i | Out-String
if ($instances -notmatch $InstanceName) {
    Write-Host "Creating LocalDB instance '$InstanceName'..."
    & sqllocaldb create $InstanceName | Write-Host
}

Write-Host "Starting LocalDB instance '$InstanceName' (safe to call if already running)..."
& sqllocaldb start $InstanceName | Write-Host

# Verify server connectivity using connection string pattern
$connString = "Server=(localdb)\\$InstanceName;Database=$DatabaseName;Trusted_Connection=True;MultipleActiveResultSets=True"

Write-Host "Using connection string: $connString"

# Apply EF Core migrations - this requires the ef tools and working restore/build
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'dotnet not found. Ensure .NET SDK is installed and on PATH.'
    exit 2
}

Write-Host 'Applying EF Core migrations (may require dotnet-ef and valid project configuration)...'

# If user provided EF_MIGRATION_CONNECTION env var, use it for the migration command and log it
if ($env:EF_MIGRATION_CONNECTION) {
    Write-Host 'Using EF_MIGRATION_CONNECTION from environment for migration run (overrides default connection).'
    $connOverride = $env:EF_MIGRATION_CONNECTION
}

try {
    $efArgs = @('database', 'update', '-p', $MigrationProject, '-s', $StartupProject, '--verbose')
    if ($connOverride) { $efArgs += @('--connection', $connOverride) }

    Write-Host "Running: dotnet ef $($efArgs -join ' ')"
    dotnet ef @efArgs
    Write-Host 'Migrations applied successfully.'
}
catch {
    Write-Error "Failed to apply migrations: $($_.Exception.Message)"
    Write-Host "Run `dotnet ef migrations list -p $MigrationProject -s $StartupProject --verbose` to inspect migrations or create one via `dotnet ef migrations add <Name> -p $MigrationProject -s $StartupProject`.'"
    exit 3
}

Write-Host 'LocalDB setup and migration finished. If the application still fails on startup, confirm the connection string in appsettings.json and that SQL Server accepts connections.'
