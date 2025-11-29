param(
    [switch]$Force,
    [switch]$Verbose
)

<#
    scripts/tools/setup-dev-db.ps1

    Powershell script to create or update a development LocalDB instance (Windows).
    - Starts (localdb)\\mssqllocaldb if stopped
    - Creates database WileyWidget.Dev if missing
    - Runs EF Core database update against src/WileyWidget.Data

    Usage:
      pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\tools\setup-dev-db.ps1

    Notes:
      - Requires sqlcmd in PATH (part of SQL Server tools) and sqllocaldb (Default with VS/.NET tooling)
      - Runs dotnet ef database update; ensure dotnet-ef tool is installed globally or use the dotnet CLI workload
#>

function Write-Log { param($msg) Write-Host "[setup-dev-db] $msg" }

if (-not ($IsWindows)) {
    Write-Log "LocalDB is Windows-only. To create a dev DB on non-Windows CI, use a transient SQL Server Docker container (documented in README)."
    exit 1
}

# Ensure LocalDB instance is running
try {
    Write-Log "Ensuring LocalDB instance 'mssqllocaldb' is running..."
    sqllocaldb start mssqllocaldb | Out-Null
}
catch {
    Write-Log "Failed to start LocalDB 'mssqllocaldb' — ensure LocalDB is installed. Error: $_"
    exit 2
}

# Check if sqlcmd exists
$hasSqlCmd = $false
try {
    sqlcmd -? > $null 2>&1
    $hasSqlCmd = $LASTEXITCODE -eq 0 -or $LASTEXITCODE -eq $null
}
catch {
    $hasSqlCmd = $false
}

if (-not $hasSqlCmd) {
    Write-Log "sqlcmd not found in PATH. Please install 'SQL Server Command Line Tools' (sqlcmd) or use SQL Server Management Studio to create the DB."
    exit 3
}

# Check for existence of database
$checkQuery = "SELECT CASE WHEN DB_ID(N'WileyWidget.Dev') IS NULL THEN 0 ELSE 1 END"
$exists = sqlcmd -S "(localdb)\\mssqllocaldb" -E -Q $checkQuery -h -1 | Out-String
$exists = $exists.Trim()

if ($exists -eq "1" -and -not $Force) {
    Write-Log "Database WileyWidget.Dev already exists. Running EF migrations to ensure schema is up to date."
}
else {
    if ($exists -ne "1") {
        Write-Log "Database WileyWidget.Dev is missing. Creating database..."
        $createCmd = "CREATE DATABASE [WileyWidget.Dev]"
        sqlcmd -S "(localdb)\\mssqllocaldb" -E -Q $createCmd
        if ($LASTEXITCODE -ne 0) { Write-Log "Failed to create WileyWidget.Dev"; exit 4 }
        Write-Log "Created WileyWidget.Dev"
    }
}

# Run EF Core database update to apply migrations
Write-Log "Applying EF Core migrations to WileyWidget.Dev (src/WileyWidget.Data)"
$efToolCheck = dotnet ef -v > $null 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Log "dotnet-ef not available. Installing latest dotnet-ef as a global tool (requires Internet)."
    dotnet tool install --global dotnet-ef
    $env:PATH += ";$env:USERPROFILE\.dotnet\tools"
}

try {
    dotnet ef database update --project src/WileyWidget.Data --startup-project WileyWidget.WinForms
    if ($LASTEXITCODE -ne 0) { Write-Log "dotnet ef database update returned non-zero exit code"; exit 5 }
}
catch {
    Write-Log "dotnet ef database update failed: $_"
    exit 6
}

Write-Log "Dev database ready."
exit 0
