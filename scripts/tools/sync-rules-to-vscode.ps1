<#
Verification Required Before Running:
1. Lint/Analyze: Run `pwsh -c 'Invoke-ScriptAnalyzer -Path scripts/tools/sync-rules-to-vscode.ps1 -Severity Information -Settings PSScriptAnalyzerSettings.psd1'`
2. Dry-Run: `pwsh scripts/tools/sync-rules-to-vscode.ps1 -WhatIf`
3. Test: Run locally and ensure that .vscode files are updated as expected.
#>

param(
    [switch]$Force
)

$srcDir = Join-Path -Path $PSScriptRoot -ChildPath "..\..\.continue\rules"
$dstDir = Join-Path -Path $PSScriptRoot -ChildPath "..\..\.vscode"

Write-Output "Syncing rule files from: $srcDir -> $dstDir"

if (-not (Test-Path -Path $srcDir)) {
    Write-Error "Source rules directory not found: $srcDir"
    exit 1
}

Get-ChildItem -Path $srcDir -Filter "*.md" -File | ForEach-Object {
    $src = $_.FullName
    $dst = Join-Path -Path $dstDir -ChildPath $_.Name

    if (Test-Path -Path $dst) {
        $srcText = Get-Content -Path $src -Raw -ErrorAction Stop
        $dstText = Get-Content -Path $dst -Raw -ErrorAction Stop
        if ($srcText -ne $dstText) {
            if ($Force) {
                Copy-Item -Path $src -Destination $dst -Force -ErrorAction Stop
                Write-Output "Updated: $($_.Name)"
            }
            else {
                Write-Output "Different content detected for $($_.Name). Run with -Force to overwrite."
            }
        }
        else {
            Write-Output "No change: $($_.Name)"
        }
    }
    else {
        Copy-Item -Path $src -Destination $dst -Force -ErrorAction Stop
        Write-Output "Copied new rule: $($_.Name)"
    }
}

Write-Output "Sync complete. If you want to force update, run with -Force."