<#!
.SYNOPSIS
  Computes approximate CRAP (Change Risk Anti-Patterns) scores for methods in a C# source file.
.DESCRIPTION
  1. Optionally runs `dotnet test` with Coverlet to gather coverage if a coverage JSON file isn't supplied.
  2. Approximates cyclomatic complexity per method using a lightweight keyword heuristic (branch keywords + 1).
  3. Joins complexity with coverage (sequence point hits) to compute CRAP:
       CRAP = comp^2 * (1 - coverage%)^3 + comp
     where coverage% = (covered / total) * 100.
  4. Outputs a sorted table and emits a JSON report for further automation.
.NOTES
  This is an approximation. For production-grade metrics integrate Roslyn analyzers or tools like NDepend / ReportGenerator with detailed complexity data.
.PARAMETER SourceFile
  Path to the C# file to analyze.
.PARAMETER SolutionOrProject
  Path to .sln or .csproj used when (optionally) invoking tests.
.PARAMETER CoverageJson
  Existing Coverlet coverage JSON (if omitted, tests are executed to produce one).
.PARAMETER OutputJson
  Path for emitted CRAP report JSON (default: build/crap-report.json).
.PARAMETER RunTests
  Switch to force running tests even if CoverageJson provided.
.EXAMPLE
  pwsh ./scripts/compute-crap.ps1 -SourceFile "Wiley Widget/Views/MainWindow.xaml.cs" -SolutionOrProject WileyWidget.sln
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)] [string]$SourceFile,
    [Parameter(Mandatory=$true)] [string]$SolutionOrProject,
    [string]$CoverageJson,
    [string]$OutputJson = "build/crap-report.json",
    [switch]$RunTests,
    [int]$MinLinesPerMethod = 2
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Info($msg){ Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Write-Warn($msg){ Write-Host "[WARN] $msg" -ForegroundColor Yellow }
function Write-Err($msg){ Write-Host "[ERR ] $msg" -ForegroundColor Red }

if(-not (Test-Path $SourceFile)){ throw "SourceFile not found: $SourceFile" }
if(-not (Test-Path $SolutionOrProject)){ throw "Solution/Project not found: $SolutionOrProject" }

$coverageTemp = $null
if(-not $CoverageJson -or $RunTests){
    Write-Info "Running tests with coverage (Coverlet)..."
    $coverageDir = Join-Path (Resolve-Path .) 'build/coverage-temp'
    New-Item -ItemType Directory -Path $coverageDir -Force | Out-Null
    $coverageTemp = Join-Path $coverageDir 'coverage.json'
    dotnet test $SolutionOrProject /p:CollectCoverage=true /p:CoverletOutput=$coverageDir/ /p:CoverletOutputFormat=json | Out-Null
    $CoverageJson = $coverageTemp
}

if(-not (Test-Path $CoverageJson)){ Write-Warn "Coverage JSON not found. CRAP scores will assume 0% coverage." }
else { Write-Info "Using coverage file: $CoverageJson" }

$coverageData = $null
if(Test-Path $CoverageJson){
    try { $coverageData = Get-Content $CoverageJson -Raw | ConvertFrom-Json } catch { Write-Warn "Failed to parse coverage JSON: $_" }
}

# Build coverage index: method => (covered, total)
$coverageIndex = @{}
if($coverageData){
    foreach($mod in $coverageData.Modules.PSObject.Properties.Value){
        foreach($doc in $mod.Documents.PSObject.Properties){
            $docPath = $doc.Name
            if($docPath -and ((Split-Path $SourceFile -Leaf) -eq (Split-Path $docPath -Leaf))){
                foreach($cls in $mod.Classes){
                    foreach($meth in $cls.Methods){
                        $total = 0; $covered = 0
                        foreach($sp in $meth.SequencePoints){
                            $total++
                            if($sp.Hits -gt 0){ $covered++ }
                        }
                        $key = $meth.Name
                        if(-not $coverageIndex.ContainsKey($key)){
                            $coverageIndex[$key] = [PSCustomObject]@{ Covered=$covered; Total=$total }
                        }
                    }
                }
            }
        }
    }
}

# Read source and split into rudimentary methods
$sourceLines = Get-Content $SourceFile

# Regex heuristics for method starts (public/private/protected/internal + return + name( ... ) { )
$methodPattern = '^(\s*)(public|private|protected|internal)(\s+static)?\s+[^;=]+?([A-Za-z0-9_`]+)\s*\([^;]*\)\s*(\{|=>)'

$methods = @()
$current = $null
$braceDepth = 0
for($i=0; $i -lt $sourceLines.Count; $i++){
    $line = $sourceLines[$i]
    if($line -match $methodPattern){
        if($current){ $methods += $current }
        $braceDepth = 0
        $current = [PSCustomObject]@{
            Name = ($Matches[4])
            Start = $i
            Lines = @($line)
            Completed = $false
        }
        if($line -match '\{'){ $braceDepth = ($line -split '\{').Count - ($line -split '\}').Count }
        continue
    }
    if($current){
        $current.Lines += $line
        if($line -match '\{'){ $braceDepth++ }
        if($line -match '\}'){ $braceDepth-- }
        if($braceDepth -le 0 -and $line.Trim().EndsWith('}')){
            $current.Completed = $true
            $methods += $current
            $current = $null
        }
    }
}
if($current){ $methods += $current }

# Complexity heuristic: count branching / decision keywords
$branchKeywords = '(?<![A-Za-z0-9_])(if|for|foreach|while|case|catch|when|&&|\|\||\?)(?![A-Za-z0-9_])'

function Get-Complexity([string[]]$lines){
    if(-not $lines){ return 1 }
    $joined = ($lines -join "\n")
    $matches = [regex]::Matches($joined, $branchKeywords, 'IgnoreCase')
    $complexity = 1 + $matches.Count
    return $complexity
}

$result = @()
foreach($m in $methods){
    $lineCount = $m.Lines.Count
    if($lineCount -lt $MinLinesPerMethod){ continue }
    $complexity = Get-Complexity $m.Lines
    $covRec = $coverageIndex[$m.Name]
    $covered = 0; $total = 0; $pct = 0
    if($covRec){
        $covered = $covRec.Covered; $total = $covRec.Total
        if($total -gt 0){ $pct = [double]$covered / $total * 100 }
    }
    $covFrac = $pct / 100.0
    $crap = [math]::Pow($complexity,2) * [math]::Pow((1 - $covFrac),3) + $complexity
    $result += [PSCustomObject]@{
        Method      = $m.Name
        StartLine   = $m.Start + 1
        Lines       = $lineCount
        Complexity  = $complexity
        Covered     = $covered
        Total       = $total
        CoveragePct = [math]::Round($pct,2)
        CRAP        = [math]::Round($crap,2)
    }
}

$result = $result | Sort-Object -Property CRAP -Descending

Write-Host "\nCRAP SCORE REPORT (Approximate)" -ForegroundColor Green
$result | Format-Table -AutoSize

# Emit JSON
$reportObj = [PSCustomObject]@{
    File = (Resolve-Path $SourceFile).Path
    Generated = (Get-Date).ToString('o')
    Methods = $result
}
New-Item -ItemType Directory -Force -Path (Split-Path $OutputJson) | Out-Null
$reportObj | ConvertTo-Json -Depth 6 | Set-Content $OutputJson -Encoding UTF8
Write-Info "Report written: $OutputJson"

# Basic summary risk classification
$high = ($result | Where-Object { $_.CRAP -ge 30 }).Count
$mod  = ($result | Where-Object { $_.CRAP -ge 15 -and $_.CRAP -lt 30 }).Count
$low  = ($result | Where-Object { $_.CRAP -lt 15 }).Count
Write-Host "High: $high  Moderate: $mod  Low: $low" -ForegroundColor Magenta

if($high -gt 0){ exit 0 } else { exit 0 }
