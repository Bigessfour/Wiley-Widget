#!/usr/bin/env pwsh
# Script to inspect problematic directories that Git cannot access

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

foreach ($p in $paths) {
    $fullPath = Join-Path $rootPath $p

    Write-Host "`n=== Inspecting: $p ===" -ForegroundColor Cyan

    if (-not (Test-Path -LiteralPath $fullPath)) {
        Write-Host "  STATUS: DOES NOT EXIST" -ForegroundColor Red
        continue
    }

    try {
        $item = Get-Item -LiteralPath $fullPath -Force -ErrorAction Stop

        Write-Host "  FullName: $($item.FullName)" -ForegroundColor Green
        Write-Host "  Mode: $($item.Mode)"
        Write-Host "  Attributes: $($item.Attributes)"

        if ($item.LinkType) {
            Write-Host "  LinkType: $($item.LinkType)" -ForegroundColor Yellow
            Write-Host "  Target: $($item.Target)" -ForegroundColor Yellow
        }

        # Check if it's a junction/symlink/reparse point
        if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            Write-Host "  *** REPARSE POINT DETECTED ***" -ForegroundColor Magenta
        }

        # Try to list children
        Write-Host "`n  Child items (first 5):"
        $children = Get-ChildItem -LiteralPath $fullPath -Force -ErrorAction SilentlyContinue | Select-Object -First 5
        if ($children) {
            $children | Format-Table Mode, Name, Length -AutoSize | Out-String | Write-Host
        } else {
            Write-Host "    (No children or access denied)" -ForegroundColor Yellow
        }

    } catch {
        Write-Host "  ERROR: $_" -ForegroundColor Red
        Write-Host "  Exception Type: $($_.Exception.GetType().FullName)" -ForegroundColor Red
    }
}

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "Git reported: 'error: open(""WileyWidget.Abstractions""): Function not implemented'"
Write-Host "This typically indicates a symlink, junction, or other special reparse point that Git cannot handle."
