[CmdletBinding()]
param(
    [string[]]$Paths = @(
        'C:\\Users\\biges\\Desktop\\Wiley_Widget\\scripts\\find-build-artifacts.ps1',
        'C:\\Users\\biges\\Desktop\\Wiley_Widget\\scripts\\delete-wpftmp.ps1',
        'C:\\Users\\biges\\Desktop\\Wiley_Widget\\scripts\\Modules\\FsTools\\FsTools.psm1',
        'C:\\Users\\biges\\Desktop\\Wiley_Widget\\scripts\\scan-nested-projects.ps1'
    ),
    [switch]$InstallIfMissing
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    if (-not (Get-Module -ListAvailable -Name PSScriptAnalyzer)) {
        if ($InstallIfMissing) {
            Write-Output 'Installing PSScriptAnalyzer for current user...'
            Install-Module PSScriptAnalyzer -Scope CurrentUser -Force -AllowClobber -ErrorAction Stop
        }
        else {
            throw 'PSScriptAnalyzer module not found. Re-run with -InstallIfMissing to install.'
        }
    }
    Import-Module PSScriptAnalyzer -Force -ErrorAction Stop | Out-Null
}
catch {
    Write-Error ("PSScriptAnalyzer not available: " + $_.Exception.Message)
    exit 1
}

$results = @()
foreach ($p in $Paths) {
    if (Test-Path $p) {
        Write-Output ("Analyzing: " + $p)
        $results += Invoke-ScriptAnalyzer -Path $p -Severity Warning, Error -Recurse -ErrorAction SilentlyContinue
    }
    else {
        Write-Warning ("Path not found: " + $p)
    }
}

if ($results.Count -gt 0) {
    $results | Select-Object RuleName, Severity, ScriptName, Line, Column, Message | Format-Table -AutoSize
    Write-Output ("Total issues: " + $results.Count)
    exit 0
}
else {
    Write-Output 'No issues found by PSScriptAnalyzer.'
    exit 0
}
$scriptPath = $args[0]
if (-not $scriptPath) { Write-Error 'Usage: run-pssa.ps1 <scriptPath>'; exit 2 }
try {
    $results = Invoke-ScriptAnalyzer -Path $scriptPath -Severity Warning, Error
    if (-not $results -or $results.Count -eq 0) {
        Write-Output 'PSScriptAnalyzer: No warnings or errors found.'
        exit 0
    }
    $results | Select-Object RuleName, Severity, ScriptName, Line, Message | Format-Table -AutoSize
    exit 0
}
catch {
    Write-Error "PSScriptAnalyzer invocation failed: $($_.Exception.Message)"
    exit 3
}
