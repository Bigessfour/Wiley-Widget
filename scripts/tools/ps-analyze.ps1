#Requires -Version 7.5.4
[CmdletBinding()]
param(
    [Parameter()][string]$Path = (Join-Path $PSScriptRoot '..'),
    [ValidateSet('Error', 'Warning', 'Information', 'None')]
    [string]$FailOn = 'Warning'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$settingsPath = Join-Path (Get-Location) '.vscode/PSScriptAnalyzerSettings.psd1'
if (-not (Test-Path $settingsPath)) { $settingsPath = $null }

Write-Output ("Analyzing path: {0}" -f $Path)
if ($settingsPath) { Write-Output ("Using settings: {0}" -f $settingsPath) }

$invokeParams = @{ Path = $Path; Recurse = $true }
if ($settingsPath) { $invokeParams['Settings'] = $settingsPath }
$results = Invoke-ScriptAnalyzer @invokeParams

if (-not $results) {
    Write-Output 'No ScriptAnalyzer findings.'
    exit 0
}

$results | Sort-Object Severity, RuleName, ScriptPath |
    Format-Table -AutoSize Severity, RuleName, Message, ScriptPath, Line | Out-String | Write-Output

$grouped = $results | Group-Object Severity | Sort-Object Name
Write-Output 'Summary:'
foreach ($g in $grouped) { Write-Output ("  {0}: {1}" -f $g.Name, $g.Count) }

function ShouldFail($severity) {
    switch ($FailOn) {
        'Error' { return $severity -eq 'Error' }
        'Warning' { return $severity -in @('Error', 'Warning') }
        'Information' { return $severity -in @('Error', 'Warning', 'Information') }
        default { return $false }
    }
}

if ($results | Where-Object { ShouldFail $_.Severity }) { exit 1 } else { exit 0 }
