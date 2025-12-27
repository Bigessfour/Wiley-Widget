param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Development', 'Staging', 'Production')]
    [string]$Environment,

    [switch]$DryRun,
    [switch]$Force,
    [switch]$BackupFirst
)

$ErrorActionPreference = 'Stop'

# Configuration per environment
$configs = @{
    'Development' = @{
        ConnectionStringKey = 'ConnectionStrings:DefaultConnection'
        AutoApply = $true
        RequireConfirmation = $false
    }
    'Staging' = @{
        ConnectionStringKey = 'ConnectionStrings:DefaultConnection'
        AutoApply = $false
        RequireConfirmation = $true
    }
    'Production' = @{
        ConnectionStringKey = 'ConnectionStrings:DefaultConnection'
        AutoApply = $false
        RequireConfirmation = $true
    }
}

$config = $configs[$Environment]

Write-Host "=== Wiley Widget Migration Deployment ===" -ForegroundColor Cyan
Write-Host "Environment: $Environment" -ForegroundColor Yellow
Write-Host "Dry Run: $($DryRun)" -ForegroundColor Yellow
Write-Host "Force: $($Force)" -ForegroundColor Yellow
Write-Host ""

# Resolve paths
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$dataProject = Join-Path $repoRoot 'src\WileyWidget.Data\WileyWidget.Data.csproj'
$startupProject = Join-Path $repoRoot 'src\WileyWidget.WinForms\WileyWidget.WinForms.csproj'

Write-Host "Data Project: $dataProject" -ForegroundColor Gray
Write-Host "Startup Project: $startupProject" -ForegroundColor Gray
Write-Host ""

# Check for pending migrations
Write-Host "Checking for pending migrations..." -ForegroundColor Cyan
try {
    $migrationsOutput = & dotnet ef migrations list --project $dataProject --startup-project $startupProject 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to list migrations: $migrationsOutput"
    }

    $pendingMigrations = $migrationsOutput | Where-Object { $_ -match '\(Pending\)' }
    $pendingCount = ($pendingMigrations | Measure-Object).Count

    if ($pendingCount -eq 0) {
        Write-Host "✅ No pending migrations found. Database is up to date." -ForegroundColor Green
        exit 0
    }

    Write-Host "⚠️  Found $pendingCount pending migration(s):" -ForegroundColor Yellow
    $pendingMigrations | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    Write-Host ""
}
catch {
    Write-Error "Failed to check migrations: $($_.Exception.Message)"
    exit 1
}

# Environment-specific confirmation
if ($config.RequireConfirmation -and -not $Force) {
    Write-Host "⚠️  PRODUCTION/STAGING DEPLOYMENT REQUIRES CONFIRMATION" -ForegroundColor Red
    Write-Host ""
    Write-Host "This will apply migrations to the $Environment environment." -ForegroundColor Yellow
    Write-Host "Please ensure:" -ForegroundColor Yellow
    Write-Host "  - Database backup has been taken" -ForegroundColor Yellow
    Write-Host "  - Rollback plan is in place" -ForegroundColor Yellow
    Write-Host "  - Stakeholders have been notified" -ForegroundColor Yellow
    Write-Host ""

    $confirmation = Read-Host "Type 'YES' to proceed with migration deployment"
    if ($confirmation -ne 'YES') {
        Write-Host "Migration deployment cancelled." -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

# Backup if requested
if ($BackupFirst) {
    Write-Host "Creating database backup..." -ForegroundColor Cyan
    # Note: Backup logic would depend on database type (SQL Server vs PostgreSQL)
    # This is a placeholder for actual backup implementation
    Write-Host "⚠️  Backup functionality not implemented yet" -ForegroundColor Yellow
    Write-Host ""
}

# Apply migrations
if ($DryRun) {
    Write-Host "🔍 DRY RUN: Would apply $pendingCount migration(s)" -ForegroundColor Cyan
    Write-Host "Command: dotnet ef database update --project '$dataProject' --startup-project '$startupProject'" -ForegroundColor Gray
}
else {
    Write-Host "Applying $pendingCount migration(s)..." -ForegroundColor Cyan

    try {
        $updateOutput = & dotnet ef database update --project $dataProject --startup-project $startupProject 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Migration failed: $updateOutput"
        }

        Write-Host "✅ Migrations applied successfully!" -ForegroundColor Green
        Write-Host "Output:" -ForegroundColor Gray
        $updateOutput | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    }
    catch {
        Write-Error "Migration deployment failed: $($_.Exception.Message)"
        Write-Host ""
        Write-Host "🔄 ROLLBACK INSTRUCTIONS:" -ForegroundColor Red
        Write-Host "  dotnet ef database update <previous-migration-name> --project '$dataProject' --startup-project '$startupProject'" -ForegroundColor Red
        Write-Host "  Or restore from backup if available" -ForegroundColor Red
        exit 1
    }
}

# Post-deployment verification
if (-not $DryRun) {
    Write-Host ""
    Write-Host "Verifying deployment..." -ForegroundColor Cyan

    try {
        $verifyOutput = & dotnet ef migrations list --project $dataProject --startup-project $startupProject 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Verification failed: $verifyOutput"
        }

        $remainingPending = $verifyOutput | Where-Object { $_ -match '\(Pending\)' }
        $remainingCount = ($remainingPending | Measure-Object).Count

        if ($remainingCount -eq 0) {
            Write-Host "✅ Verification complete: No pending migrations remain." -ForegroundColor Green
        }
        else {
            Write-Warning "⚠️  Verification found $remainingCount pending migration(s) after deployment:"
            $remainingPending | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
        }
    }
    catch {
        Write-Warning "Verification failed: $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "=== Migration Deployment Complete ===" -ForegroundColor Green</content>
<parameter name="filePath">c:/Users/biges/Desktop/Wiley-Widget/scripts/maintenance/deploy-migrations.ps1