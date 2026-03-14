[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$NoRestore,

    [switch]$NoBuild,

    [switch]$IncludeUiTests,

    [string]$CoverageRoot = 'coverage',

    [string]$ReportDirectory = 'CoverageReport'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir '..\..')
Set-Location $repoRoot

$coverageRootPath = Join-Path $repoRoot $CoverageRoot
$reportDirectoryPath = Join-Path $repoRoot $ReportDirectory

foreach ($path in @($coverageRootPath, $reportDirectoryPath)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $path | Out-Null
}

$projects = [System.Collections.Generic.List[object]]::new()
$projects.Add([pscustomobject]@{
        Name   = 'LayerProof'
        Path   = 'tests/WileyWidget.LayerProof.Tests/WileyWidget.LayerProof.Tests.csproj'
        Filter = $null
    })
$projects.Add([pscustomobject]@{
        Name   = 'LayoutRegression'
        Path   = 'tests/WileyWidget.LayoutRegression.Tests/WileyWidget.LayoutRegression.Tests.csproj'
        Filter = $null
    })
$projects.Add([pscustomobject]@{
        Name   = 'WinForms'
        Path   = 'tests/WileyWidget.WinForms.Tests/WileyWidget.WinForms.Tests.csproj'
        Filter = 'FullyQualifiedName~WileyWidget.WinForms.Tests.Unit.Services|FullyQualifiedName~WileyWidget.WinForms.Tests.Unit.Services.AI|FullyQualifiedName~WileyWidget.WinForms.Tests.Unit.ViewModels|FullyQualifiedName~WileyWidget.WinForms.Tests.Integration.Services|FullyQualifiedName~WileyWidget.WinForms.Tests.Integration.Services.AI|FullyQualifiedName~WileyWidget.WinForms.Tests.Integration.DependencyInjection'
    })

if ($IncludeUiTests.IsPresent) {
    $projects.Add([pscustomobject]@{
            Name   = 'UiTests'
            Path   = 'tests/WileyWidget.UiTests/WileyWidget.UiTests.csproj'
            Filter = $null
        })
}

Write-Host "Restoring local tools..."
& dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if ($NoBuild.IsPresent) {
    Write-Warning 'Ignoring -NoBuild because coverage instrumentation requires a build with CopyLocalLockFileAssemblies=true.'
}

foreach ($project in $projects) {
    $resultsDirectory = Join-Path $coverageRootPath $project.Name
    New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null

    $arguments = [System.Collections.Generic.List[string]]::new()
    $arguments.Add('test')
    $arguments.Add($project.Path)
    $arguments.Add('--configuration')
    $arguments.Add($Configuration)

    if ($NoRestore.IsPresent) {
        $arguments.Add('--no-restore')
    }

    $arguments.Add('--verbosity')
    $arguments.Add('minimal')

    $coverageOutput = Join-Path $resultsDirectory 'coverage'
    $arguments.Add('/p:CollectCoverage=true')
    $arguments.Add('/p:CoverletOutputFormat=cobertura')
    $arguments.Add("/p:CoverletOutput=$coverageOutput")
    $arguments.Add('/p:IncludeTestAssembly=false')
    $arguments.Add('/p:CopyLocalLockFileAssemblies=true')
    $arguments.Add('/p:Include=[WileyWidget.*]*')

    if (-not [string]::IsNullOrWhiteSpace($project.Filter)) {
        $arguments.Add('--filter')
        $arguments.Add($project.Filter)
    }

    Write-Host "Running coverage for $($project.Name)..."
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$reportFiles = Get-ChildItem -Path $coverageRootPath -Recurse -Filter 'coverage.cobertura.xml' |
Select-Object -ExpandProperty FullName

if (-not $reportFiles) {
    throw 'No coverage.cobertura.xml files were generated.'
}

$reportArguments = [System.Collections.Generic.List[string]]::new()
$reportArguments.Add('tool')
$reportArguments.Add('run')
$reportArguments.Add('reportgenerator')
$reportArguments.Add("-reports:$($reportFiles -join ';')")
$reportArguments.Add("-targetdir:$reportDirectoryPath")
$reportArguments.Add('-reporttypes:Html;HtmlSummary;TextSummary;Cobertura')

Write-Host 'Merging coverage reports...'
& dotnet @reportArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$summaryFile = Join-Path $reportDirectoryPath 'Summary.txt'
$mergedCoberturaFile = Join-Path $reportDirectoryPath 'Cobertura.xml'
$htmlIndexFile = Join-Path $reportDirectoryPath 'index.html'

if (Test-Path -LiteralPath $summaryFile) {
    Write-Host ''
    Get-Content -LiteralPath $summaryFile
}

Write-Host ''
Write-Host "Merged Cobertura report: $mergedCoberturaFile"
Write-Host "HTML report: $htmlIndexFile"
