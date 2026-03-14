param(
    [Parameter(Mandatory = $true)]
    [string]$Project,

    [Parameter(Mandatory = $true)]
    [string]$Filter,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir '..\..')
Set-Location $repoRoot

$projectPath = if (Test-Path -LiteralPath $Project) {
    Resolve-Path -LiteralPath $Project
} else {
    Resolve-Path -LiteralPath (Join-Path $repoRoot $Project)
}

$resultsDirectory = Join-Path $repoRoot 'TestResults'
if (-not (Test-Path -LiteralPath $resultsDirectory)) {
    New-Item -ItemType Directory -Path $resultsDirectory | Out-Null
}

$arguments = [System.Collections.Generic.List[string]]::new()
$arguments.Add('test')
$arguments.Add($projectPath)
$arguments.Add('--configuration')
$arguments.Add($Configuration)
if ($NoRestore.IsPresent) {
    $arguments.Add('--no-restore')
}
$arguments.Add('--filter')
$arguments.Add($Filter)
$arguments.Add('--logger')
$arguments.Add('trx')
$arguments.Add('--results-directory')
$arguments.Add($resultsDirectory)
$arguments.Add('-v')
$arguments.Add('minimal')

Write-Host "Project: $projectPath"
Write-Host "Filter : $Filter"

& dotnet @arguments
exit $LASTEXITCODE
