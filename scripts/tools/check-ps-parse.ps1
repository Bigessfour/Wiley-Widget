[CmdletBinding()]
param(
    [string]$Path = (Join-Path $PSScriptRoot '..')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Output "Scanning for PowerShell files under: $Path"
$files = Get-ChildItem -Path $Path -Recurse -Include *.ps1, *.psm1 -File -ErrorAction SilentlyContinue
if (-not $files) {
    Write-Output 'No PowerShell files found.'
    exit 0
}

$parseErrors = @()
foreach ($f in $files) {
    $null = $null
    $tokens = $null
    $ast = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($f.FullName, [ref]$tokens, [ref]$errors)
    if ($errors -and $errors.Count -gt 0) {
        $parseErrors += [pscustomobject]@{
            Path   = $f.FullName
            Errors = ($errors | ForEach-Object { [pscustomobject]@{ Message = $_.Message; Line = $_.Extent.StartLineNumber; Column = $_.Extent.StartColumnNumber } })
        }
    }
}

if ($parseErrors.Count -gt 0) {
    Write-Output "Parse errors found: $($parseErrors.Count) file(s)"
    foreach ($pe in $parseErrors) {
        Write-Output ("--- {0}" -f $pe.Path)
        foreach ($e in $pe.Errors) {
            Write-Output ("  * Line {0}, Col {1}: {2}" -f $e.Line, $e.Column, $e.Message)
        }
    }
    exit 1
}
else {
    Write-Output 'No parsing errors.'
}

# Optional: Run PSScriptAnalyzer if available
try {
    $pssa = Get-Module -ListAvailable PSScriptAnalyzer | Sort-Object Version -Descending | Select-Object -First 1
    if ($pssa) {
        Write-Output ("Running ScriptAnalyzer: {0}" -f $pssa.Version)
        $results = Invoke-ScriptAnalyzer -Path $Path -Recurse -ErrorAction SilentlyContinue
        if ($results) {
            $grouped = $results | Group-Object Severity | Sort-Object Name
            Write-Output 'ScriptAnalyzer findings summary:'
            foreach ($g in $grouped) { Write-Output ("  {0}: {1}" -f $g.Name, $g.Count) }
        }
        else {
            Write-Output 'ScriptAnalyzer: no findings.'
        }
    }
    else {
        Write-Output 'PSScriptAnalyzer not found; skipping lint.'
    }
}
catch {
    Write-Warning ("ScriptAnalyzer run failed: {0}" -f $_.Exception.Message)
}
