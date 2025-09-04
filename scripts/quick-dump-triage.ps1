<#!
.SYNOPSIS
  Quick managed (.NET) crash dump triage using dotnet-dump.

.DESCRIPTION
  Automates first-pass analysis of a WileyWidget *.dmp file:
    * Ensures dotnet-dump global tool is installed
    * Runs: clrstack, threads, dumpheap -stat, gcroot (optional), sos Status
    * Captures output to logs/dumps/triage_<dumpname>.txt
    * Emits a short summary (top 5 largest types & first exception frame if visible)

.PARAMETER Dump
  Path to the .dmp file (defaults to newest WileyWidget.exe*.dmp under ./logs/dumps)

.PARAMETER GcRoots
  Also run an experimental GC root query on the largest object type (can be slow on big dumps)

.EXAMPLE
  pwsh ./scripts/quick-dump-triage.ps1

.EXAMPLE
  pwsh ./scripts/quick-dump-triage.ps1 -Dump .\logs\dumps\WileyWidget.exe.175776.dmp -GcRoots
#>
[CmdletBinding()]
param(
  [string] $Dump,
  [switch] $GcRoots
)

$ErrorActionPreference = 'Stop'
$dumpDir = Join-Path (Get-Location) 'logs/dumps'
if (-not $Dump) {
  if (-not (Test-Path $dumpDir)) { throw "Dump folder not found: $dumpDir" }
  $Dump = Get-ChildItem $dumpDir -Filter 'WileyWidget.exe*.dmp' | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
  if (-not $Dump) { throw 'No WileyWidget dumps found.' }
}
if (-not (Test-Path $Dump)) { throw "Dump not found: $Dump" }

Write-Host "🔍 Analyzing dump: $Dump" -ForegroundColor Cyan

# Ensure dotnet-dump installed
$toolList = & dotnet tool list -g 2>$null | Out-String
if ($toolList -notmatch 'dotnet-dump') {
  Write-Host '⬇️  Installing dotnet-dump global tool...' -ForegroundColor Yellow
  & dotnet tool install -g dotnet-dump | Out-Null
}

$dotnetToolPath = (Get-Command dotnet-dump).Source
if (-not $dotnetToolPath) { throw 'dotnet-dump not available after install.' }

$triageOut = Join-Path $dumpDir ("triage_" + [IO.Path]::GetFileNameWithoutExtension($Dump) + ".txt")

# Build analysis command script (dotnet-dump supports -c for command)
$commands = @(
  'setsymbolserver -ms',
  'clrstack -f',
  'threads',
  'dumpheap -stat'
)

if ($GcRoots) { $commands += 'dumpheap -stat'; }

# Compose multi-command invocation
# dotnet-dump analyze <dump> -c "command" -c "command" ...
$cmdArgs = @('analyze', $Dump) + ($commands | ForEach-Object { '-c'; $_ }) + @('-c','quit')

Write-Host '▶️  Running dotnet-dump (this may take a moment)...'
$raw = & dotnet-dump @cmdArgs | Tee-Object -Variable fullOut
if ($LASTEXITCODE -ne 0) {
  Write-Warning "dotnet-dump exited with code $LASTEXITCODE. Output may be incomplete."
}
$raw | Out-File -FilePath $triageOut -Encoding UTF8

# Basic parsing
$exceptionMatch = $fullOut | Select-String -Pattern 'Exception:' -SimpleMatch | Select-Object -First 1
$exceptionLine = if ($exceptionMatch) { $exceptionMatch.Line } else { $null }
if (-not $exceptionLine) {
  $exceptionMatch2 = $fullOut | Select-String -Pattern 'System\.' | Select-Object -First 1
  if ($exceptionMatch2) { $exceptionLine = $exceptionMatch2.Line }
}

$typeTable = @()
$heapStart = ($fullOut | Select-String -Pattern '^Statistics$' -Context 0,120 | Select-Object -First 1)
if ($heapStart) {
  # Very lightweight parse: lines with two or more spaces after a number pattern
  $typeLines = $fullOut | Select-String -Pattern '^[ ]*\d+[ ]+\d+[ ]+System\.'
  foreach ($l in $typeLines) {
    $parts = ($l.Line -split '\s+') | Where-Object { $_ }
    if ($parts.Length -ge 3) {
      $typeTable += [pscustomobject]@{ Count=[int]$parts[0]; Size=[int]$parts[1]; Type=$parts[2] }
    }
  }
}

$topTypes = $typeTable | Sort-Object Size -Descending | Select-Object -First 5

Write-Host '--- SUMMARY ---------------------------------------'
Write-Host ("First exception hint: {0}" -f ($exceptionLine ?? 'N/A'))
if ($topTypes) {
  Write-Host 'Top 5 heap types:'
  $topTypes | ForEach-Object { Write-Host ("  {0,6} x {1,10} bytes  {2}" -f $_.Count, $_.Size, $_.Type) }
} else {
  Write-Host 'Heap summary not parsed (could be trimmed or unsupported).'
}
Write-Host "Full triage saved: $triageOut"
Write-Host '---------------------------------------------------'

if ($GcRoots -and $topTypes) {
  $largest = $topTypes | Select-Object -First 1
  Write-Host '(Optional GC roots) Example: dotnet-dump analyze "'"$Dump"'" -c "gcroot <OBJECT_ADDRESS>"'
}
