<#
psa-runner.ps1
Runs PSScriptAnalyzer on a target script and prints readable output.
This avoids complex -Command quoting issues when invoking pwsh from another process.

Usage:
    pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\psa-runner.ps1 -Path .\scripts\tools\run-mcp.ps1
#>
param(
    [Parameter(Mandatory = $false)]
        [string]$Path = "scripts/tools/run-mcp.ps1",
    [Parameter(Mandatory = $false)]
    [string]$Severity = "Warning,Error"
)

Write-Host "Running PSScriptAnalyzer runner"
Write-Host "PowerShell version:" -NoNewline; Write-Host $PSVersionTable.PSVersion

try {
    if (-not (Get-Module -ListAvailable -Name PSScriptAnalyzer)) {
        Write-Host 'PSScriptAnalyzer not found — installing to CurrentUser scope...' -ForegroundColor Yellow
        Install-Module PSScriptAnalyzer -Scope CurrentUser -Force -AllowClobber -ErrorAction Stop
    }
    Import-Module PSScriptAnalyzer -Force -ErrorAction Stop
}
catch {
    Write-Host "Failed to install or import PSScriptAnalyzer: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}

try {
    Write-Host "Analyzing: $Path" -ForegroundColor Cyan
    $sevList = $Severity -split ',' | ForEach-Object { $_.Trim() }
    $results = Invoke-ScriptAnalyzer -Path $Path -Severity $sevList -ErrorAction Stop
    if (-not $results -or $results.Count -eq 0) {
        Write-Output "No issues found by Invoke-ScriptAnalyzer."
        exit 0
    }

    foreach ($r in $results) {
        Write-Host "Rule: $($r.RuleName) -- Severity: $($r.Severity)" -ForegroundColor Yellow
        Write-Host "Message: $($r.Message)"
        Write-Host "File: $($r.ScriptName):$($r.Line)" -ForegroundColor DarkCyan
        Write-Host "Extent: $($r.ExtentText)"
        Write-Host "----"
    }
    exit 1
}
catch {
    Write-Host "Invoke-ScriptAnalyzer failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 3
}
