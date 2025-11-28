<#: 
.SYNOPSIS
    Lint the PowerShell profile files used by the workspace (non-destructive).
.DESCRIPTION
    Runs Invoke-ScriptAnalyzer over the workspace's PowerShell files of interest (scripts/ and .vscode/profile.ps1).
    Exits with non-zero on analyzer errors (so CI can fail).
.PARAMETER Path
    Optional path(s) to include (defaults: ./scripts and ./.vscode/profile.ps1)
#>

param(
    [string[]]$Paths = @("${PSScriptRoot}\..\..\scripts", "${PSScriptRoot}\..\..\.vscode\profile.ps1"),
    [switch]$FailOnWarning
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command Invoke-ScriptAnalyzer -ErrorAction SilentlyContinue)) {
    Write-Host "PSScriptAnalyzer not found; install with: Install-Module -Name PSScriptAnalyzer -Scope CurrentUser" -ForegroundColor Yellow
    exit 0
}

$foundProblems = $false

foreach ($p in $Paths) {
    if (-not (Test-Path $p)) {
        Write-Host "Skipping missing path: $p" -ForegroundColor Gray
        continue
    }

    Write-Host "Analyzing: $p" -ForegroundColor Cyan
    try {
        $results = Invoke-ScriptAnalyzer -Path $p -Recurse -Severity Error,Warning -EnableExit
        if ($results) {
            $foundProblems = $true
            $results | Format-Table -AutoSize
        }
    } catch {
        Write-Warning "Invoke-ScriptAnalyzer threw an error for path $($p): $($_)"
        $foundProblems = $true
    }
}

if ($foundProblems) {
    if ($FailOnWarning) { Write-Error "Script analysis found issues (errors/warnings)."; exit 1 }
    Write-Warning "Script analysis found issues (errors/warnings). To fail the CI set -FailOnWarning true."; exit 1
}

Write-Host "No issues found by PSScriptAnalyzer (checked: $($Paths -join ', '))" -ForegroundColor Green
exit 0
