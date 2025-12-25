param(
    [string]$ReportsPath = './TestResults/**/coverage.cobertura.xml',
    [string]$TargetDir = './tmp/coverage-report'
)

Write-Host "Generating coverage report from: $ReportsPath -> $TargetDir"

# Install reportgenerator global tool if missing
$tool = 'dotnet-reportgenerator-globaltool'
$toolPath = Join-Path $env:USERPROFILE ".dotnet\tools\reportgenerator.exe"
if (-not (Test-Path $toolPath)) {
    Write-Host "Installing ReportGenerator global tool..."
    dotnet tool install --global $tool --version 5.* | Write-Host
}

# Ensure target dir exists
if (Test-Path $TargetDir) { Remove-Item -Recurse -Force $TargetDir }
New-Item -ItemType Directory -Path $TargetDir | Out-Null

# Run reportgenerator
reportgenerator -reports:$ReportsPath -targetdir:$TargetDir -reporttypes:Html

Write-Host "Coverage report generated at: $TargetDir\index.html"