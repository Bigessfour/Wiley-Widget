#!/usr/bin/env pwsh
# Remove broken reparse points that Git cannot handle

$paths = @(
    'WileyWidget.Abstractions',
    'WileyWidget.Business',
    'WileyWidget.Data',
    'WileyWidget.Facade',
    'WileyWidget.Legacy',
    'WileyWidget.Models',
    'WileyWidget.Services',
    'WileyWidget.Services.Abstractions',
    'WileyWidget.UI',
    'WileyWidget.Webhooks'
)

$rootPath = Split-Path -Parent $PSScriptRoot | Split-Path -Parent
$removed = 0

foreach ($p in $paths) {
    $fullPath = Join-Path $rootPath $p

    if (Test-Path -LiteralPath $fullPath) {
        try {
            Write-Output "Removing reparse point: $p"
            $item = Get-Item -LiteralPath $fullPath -Force
            $item.Delete()
            $removed++
            Write-Output "  ✓ Removed successfully"
        } catch {
            Write-Output "  ✗ Error: $_"
        }
    } else {
        Write-Output "Skipping (not found): $p"
    }
}

Write-Output "`nTotal removed: $removed"
