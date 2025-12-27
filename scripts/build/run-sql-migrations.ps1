param(
    [string]$Connection = "Server=localhost\SQLEXPRESS;Database=WileyWidgetDev;Integrated Security=True;TrustServerCertificate=True;",
    [string]$ScriptsDir = "scripts/ci",
    [string]$Migrations = "20251013153901,20251028124500"
)

$project = "tools/SqlMigrationRunner"
Write-Host "Building runner..."
dotnet build $project -c Release

$exe = Join-Path $project "bin/Release/net9.0/SqlMigrationRunner.dll"
if (-not (Test-Path $exe)) { throw "Runner not found at $exe" }

Write-Host "Running runner for migrations: $Migrations"
dotnet $exe -- --connection="$Connection" --scriptsDir="$ScriptsDir" --migrations="$Migrations"
