<#
.SYNOPSIS
  Scan Views for prism:ViewModelLocator.AutoWireViewModel="True"

.DESCRIPTION
  Scans all .xaml files under the provided ViewsPath and reports whether the XAML contains
  the attribute prism:ViewModelLocator.AutoWireViewModel="True". Designed to be PSScriptAnalyzer-friendly.

.PARAMETER ViewsPath
  Path to Views folder (defaults to workspace path).

.EXAMPLE
  pwsh -NoProfile -File .\scripts\check-autowire.ps1 -ViewsPath 'C:\Users\biges\Desktop\Wiley_Widget\src\Views'
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string] $ViewsPath = "c:\Users\biges\Desktop\Wiley_Widget\src\Views"
)

if (-not (Test-Path -Path $ViewsPath)) {
    Write-Error "Views path not found: $ViewsPath"
    exit 2
}

$results = Get-ChildItem -Path $ViewsPath -Recurse -Filter '*.xaml' -File | ForEach-Object {
    $file = $_.FullName
    $content = Get-Content -Raw -Path $file -ErrorAction SilentlyContinue
    $hasAutoWire = $false

    if ($null -ne $content) {
        # Use regex to tolerate whitespace and case
        $pattern = 'prism:ViewModelLocator\.AutoWireViewModel\s*=\s*"(True|true)"'
        if ($content -match $pattern) {
            $hasAutoWire = $true
        }
    }

    [PSCustomObject]@{
        File     = $file
        AutoWire = $hasAutoWire
    }
}

# Output a neat table and also write a summary to stdout
$results | Sort-Object -Property File | Format-Table -AutoSize

$total = ($results | Measure-Object).Count
$with = ($results | Where-Object { $_.AutoWire } | Measure-Object).Count
$without = $total - $with

Write-Host "`nSummary: Total XAML files: $total; AutoWire=True: $with; Missing AutoWire: $without" -ForegroundColor Green

if ($without -gt 0) {
    Write-Host "Files missing AutoWire:" -ForegroundColor Yellow
    $results | Where-Object { -not $_.AutoWire } | ForEach-Object { Write-Host " - $($_.File)" }
}

# Exit with non-zero if any missing (so CI can pick it up if desired)
if ($without -gt 0) { exit 3 } else { exit 0 }
