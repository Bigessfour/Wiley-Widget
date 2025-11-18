<#
.SYNOPSIS
  Safe PowerShell script runner and analyzer wrapper.

.DESCRIPTION
  Parses scripts for syntax errors, runs PSScriptAnalyzer (if available),
  rejects usage of Write-Host, and optionally performs a dry run or analyzer-only check.
  Enforces PowerShell version >= 7.5.2.

.PARAMETER ScriptPath
  Path to a script file (.ps1) or a folder containing scripts to analyze.

.PARAMETER DryRun
  If specified, do not execute the script even if analysis passes. Just report.

.PARAMETER AnalyzerOnly
  Run only analysis checks and exit with code 0 if clean, non-zero if issues.

Examples:
  .\ps-runner.ps1 -ScriptPath .\scripts -AnalyzerOnly
  .\ps-runner.ps1 -ScriptPath .\scripts\deploy.ps1 -DryRun
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$ScriptPath,
    [switch]$DryRun,
    [switch]$AnalyzerOnly
)

function Write-Log { param($msg) Write-Host "[ps-runner] $msg" }

# Enforce PowerShell version
$required = [version]"7.5.2"
if ($PSVersionTable.PSVersion -lt $required) {
    Write-Error "PowerShell $($required) or newer is required. Current: $($PSVersionTable.PSVersion)"
    exit 10
}

try {
    $resolved = Resolve-Path -Path $ScriptPath -ErrorAction Stop
} catch {
    Write-Error "Script path '$ScriptPath' not found."
    exit 11
}

$items = @()
if ((Get-Item $resolved).PSIsContainer) {
    $items = Get-ChildItem -Path $resolved -Include '*.ps1' -Recurse | Sort-Object FullName
} else {
    $items = @(Get-Item -Path $resolved)
}

if (-not $items -or $items.Count -eq 0) {
    Write-Error "No .ps1 files found at path: $ScriptPath"
    exit 12
}

$hasParseErrors = $false
$parseReport = @()
foreach ($f in $items) {
    $content = Get-Content -Raw -Path $f.FullName -ErrorAction Stop
    $errors = New-Object System.Collections.ObjectModel.Collection[System.Management.Automation.Language.ParseError]
    [System.Management.Automation.Language.Parser]::ParseInput($content, [ref]$null, [ref]$errors)
    if ($errors.Count -gt 0) {
        $hasParseErrors = $true
        $parseReport += [PSCustomObject]@{ File = $f.FullName; Errors = $errors }
    }
}

if ($hasParseErrors) {
    Write-Error "PARSING ERRORS FOUND:"
    foreach ($r in $parseReport) {
        Write-Error "File: $($r.File)"
        foreach ($e in $r.Errors) { Write-Error "  Line $($e.Extent.StartLineNumber): $($e.Message)" }
    }
    exit 13
}

# Run PSScriptAnalyzer if available
if (Get-Module -ListAvailable -Name PSScriptAnalyzer) {
    Import-Module PSScriptAnalyzer -Force
    $issues = @()
    foreach ($f in $items) {
        $res = Invoke-ScriptAnalyzer -Path $f.FullName -Recurse:$false -ErrorAction SilentlyContinue
        if ($res) {
            foreach ($i in $res) {
                $issues += [PSCustomObject]@{
                    File = $f.FullName
                    RuleName = $i.RuleName
                    Severity = $i.Severity
                    Line = $i.Line
                    Message = $i.Message
                }
            }
        }
    }
    if ($issues.Count -gt 0) {
        Write-Host "PSScriptAnalyzer issues found (warnings/errors):" -ForegroundColor Yellow
        $issues | Sort-Object File, Line | Format-List
        Write-Host "Please fix issues or adjust .vscode/PSScriptAnalyzerSettings.psd1 if a rule is intentionally different." -ForegroundColor Yellow
        exit 14
    }
} else {
    Write-Warning "PSScriptAnalyzer module not found. Install it with: Install-Module -Name PSScriptAnalyzer -Scope CurrentUser -Force"
    exit 15
}

# Check for forbidden patterns (e.g., Write-Host)
$forbidden = @('Write-Host')
$forbidHits = @()
foreach ($f in $items) {
    foreach ($pat in $forbidden) {
        $hit = Select-String -Path $f.FullName -Pattern "\b$pat\b" -SimpleMatch -ErrorAction SilentlyContinue
        if ($hit) { $forbidHits += [PSCustomObject]@{ File = $f.FullName; Pattern = $pat; Line = $hit.LineNumber } }
    }
}
if ($forbidHits.Count -gt 0) {
    Write-Host "Forbidden patterns found (e.g. Write-Host):" -ForegroundColor Red
    $forbidHits | Format-Table -AutoSize
    exit 16
}

Write-Host "Analysis PASSED for $($items.Count) file(s)." -ForegroundColor Green

if ($AnalyzerOnly -or $DryRun) {
    if ($DryRun) { Write-Host "Dry run requested — not executing scripts." }
    exit 0
}

# If there is exactly one file, execute it. Otherwise, don't auto-execute a folder.
if ($items.Count -eq 1) {
    $fileToRun = $items[0].FullName
    Write-Host "Executing script: $fileToRun" -ForegroundColor Cyan
    $pwshPath = (Get-Command pwsh -ErrorAction SilentlyContinue).Path
    if (-not $pwshPath) { Write-Error "pwsh not found on PATH."; exit 20 }
    & $pwshPath -NoProfile -ExecutionPolicy Bypass -File $fileToRun
    exit $LASTEXITCODE
} else {
    Write-Host "Multiple files were analyzed. To execute one, re-run with -ScriptPath pointing to the single script file." -ForegroundColor Yellow
    exit 0
}
