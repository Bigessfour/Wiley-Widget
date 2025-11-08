[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Section($title) { "`n== $title ==" }

Write-Section 'PowerShell Runtime'
Write-Output ("Version: {0}" -f $PSVersionTable.PSVersion)
Write-Output ("Edition: {0}" -f $PSVersionTable.PSEdition)
try {
    $pwsh = Get-Command pwsh -ErrorAction Stop | Select-Object -First 1 -ExpandProperty Source
}
catch { $pwsh = $null }
Write-Output ("Exe path: {0}" -f ($pwsh ?? '<not found>'))

Write-Section 'ExecutionPolicy'
Get-ExecutionPolicy -List | Format-Table -AutoSize | Out-String | Write-Output

Write-Section 'Core Modules'
$pssa = Get-Module -ListAvailable PSScriptAnalyzer | Sort-Object Version -Descending | Select-Object -First 1
$pester = Get-Module -ListAvailable Pester | Sort-Object Version -Descending | Select-Object -First 1
$psrl = Get-Module -ListAvailable PSReadLine | Sort-Object Version -Descending | Select-Object -First 1
Write-Output ("PSScriptAnalyzer: {0}" -f ($pssa ? "$($pssa.Name) $($pssa.Version)" : '<not found>'))
Write-Output ("Pester: {0}" -f ($pester ? "$($pester.Name) $($pester.Version)" : '<not found>'))
Write-Output ("PSReadLine: {0}" -f ($psrl ? "$($psrl.Name) $($psrl.Version)" : '<not found>'))

Write-Section 'VS Code PowerShell Extension'
try {
    $exts = code --list-extensions 2>$null
    if (-not $exts) { $exts = code-insiders --list-extensions 2>$null }
    $hasPsExt = $exts | Where-Object { $_ -match '^ms-vscode\.powershell$' }
    Write-Output ("Extension installed: {0}" -f ([bool]$hasPsExt))
}
catch {
    Write-Output 'Unable to query VS Code extensions (code CLI not found on PATH).'
}

Write-Section 'Analyzer quick summary'
if ($pssa) {
    $results = Invoke-ScriptAnalyzer -Path (Join-Path $PSScriptRoot '..') -Recurse -ErrorAction SilentlyContinue
    if ($results) {
        $summary = $results | Group-Object Severity | Sort-Object Name | ForEach-Object { "  $($_.Name): $($_.Count)" }
        Write-Output ($summary -join "`n")
    }
    else {
        Write-Output '  No findings.'
    }
}
else {
    Write-Output '  PSScriptAnalyzer not found; skipping.'
}
