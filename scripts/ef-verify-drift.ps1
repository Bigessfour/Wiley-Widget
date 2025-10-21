param(
    [string]$Project,
    [string]$StartupProject,
    [switch]$FailOnDrift,
    [switch]$Quiet,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
if ($Quiet) { $InformationPreference = 'SilentlyContinue' } else { $InformationPreference = 'Continue' }

# Resolve defaults (migrations project and startup project)
try {
    $repoRoot = Resolve-Path -LiteralPath (Join-Path -Path $PSScriptRoot -ChildPath '..')
    if (-not $Project) { $Project = Join-Path -Path $repoRoot -ChildPath 'WileyWidget.Data\WileyWidget.Data.csproj' }
    if (-not $StartupProject) { $StartupProject = Join-Path -Path $repoRoot -ChildPath 'WileyWidget.csproj' }
}
catch {
    Write-Error "Failed to resolve project paths: $($_.Exception.Message)"
    exit 10
}

if ($DryRun) { Write-Output "[DRY RUN] reporting only"; $FailOnDrift = $false }

Write-Output "Checking EF Core migration drift..."
Write-Output "  Project:        $Project"
Write-Output "  StartupProject: $StartupProject"

try {
    $projectDir = Split-Path -Parent $Project
    Push-Location $projectDir

    $argsList = @('ef', 'migrations', 'list', '--startup-project', $StartupProject)
    $output = & dotnet @argsList 2>&1
    $exit = $LASTEXITCODE

    Pop-Location

    if ($exit -ne 0) {
        Write-Error "'dotnet ef migrations list' failed with exit code $exit"
        $output | ForEach-Object { Write-Error "  $_" }
        exit $exit
    }

    # Pending migrations are typically marked with '(Pending)'
    $pending = $output | Where-Object { $_ -match '(?i)\(Pending\)' }
    if ($null -eq $pending) { $pendingCount = 0 } else { $pendingCount = ($pending | Measure-Object).Count }

    if ($pendingCount -gt 0) {
        Write-Warning "Pending migrations detected ($pendingCount):"
        $pending | ForEach-Object { Write-Output "  $_" }
        if ($FailOnDrift) { Write-Warning "Failing due to drift."; exit 2 } else { exit 0 }
    }
    else {
        Write-Output "No pending migrations; database appears aligned with model."
        exit 0
    }
}
catch {
    Write-Error "Drift check failed: $($_.Exception.Message)"
    exit 11
}
