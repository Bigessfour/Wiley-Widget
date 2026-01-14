#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fixes Panel type reference issues caused by bulk find/replace
.DESCRIPTION
    Repairs concatenation issues where "Panel" was replaced with "System.Windows.Forms.Panel"
    and consumed adjacent words (like "privatePanel" instead of "private Panel")
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$SourcePath = "$PSScriptRoot\..\src\WileyWidget.WinForms"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "Fixing Panel type references in $SourcePath..." -ForegroundColor Cyan

# Define all the patterns to fix
$patterns = @(
    # Access modifiers + Panel (without space)
    @{ Pattern = 'privatePanel'; Replacement = 'private Panel' }
    @{ Pattern = 'publicPanel'; Replacement = 'public Panel' }
    @{ Pattern = 'protectedPanel'; Replacement = 'protected Panel' }
    @{ Pattern = 'internalPanel'; Replacement = 'internal Panel' }
    @{ Pattern = 'staticPanel'; Replacement = 'static Panel' }

    # Common concatenations
    @{ Pattern = 'PanelPanel'; Replacement = 'Panel Panel' }
    @{ Pattern = 'isPanel'; Replacement = 'is Panel' }
    @{ Pattern = 'asPanel'; Replacement = 'as Panel' }
    @{ Pattern = 'newPanel'; Replacement = 'new Panel' }

    # Type declarations
    @{ Pattern = ':\s*Panel\b(?!\.)'; Replacement = ': System.Windows.Forms.Panel'; IsRegex = $true }
    @{ Pattern = '<Panel>'; Replacement = '<System.Windows.Forms.Panel>' }
    @{ Pattern = '<Panel\?'; Replacement = '<System.Windows.Forms.Panel?' }
    @{ Pattern = '\(Panel\s'; Replacement = '(System.Windows.Forms.Panel '; IsRegex = $true }
    @{ Pattern = '\(Panel\?'; Replacement = '(System.Windows.Forms.Panel?'; IsRegex = $true }
    @{ Pattern = ',\s*Panel\s'; Replacement = ', System.Windows.Forms.Panel '; IsRegex = $true }
    @{ Pattern = ',\s*Panel\?'; Replacement = ', System.Windows.Forms.Panel?'; IsRegex = $true }
)

$filesChanged = 0
$totalReplacements = 0

# Get all C# files
$files = Get-ChildItem -Path $SourcePath -Filter "*.cs" -Recurse -File

foreach ($file in $files) {
    try {
        $content = Get-Content $file.FullName -Raw -ErrorAction Stop
        if (-not $content) { continue }

        $originalContent = $content
        $fileReplacements = 0

        foreach ($pattern in $patterns) {
            $oldContent = $content
            if ($pattern.ContainsKey('IsRegex') -and $pattern.IsRegex) {
                $content = [regex]::Replace($content, $pattern.Pattern, $pattern.Replacement)
            } else {
                $content = $content.Replace($pattern.Pattern, $pattern.Replacement)
            }

            if ($content -ne $oldContent) {
                $fileReplacements++
            }
        }

        if ($content -ne $originalContent) {
            Set-Content -Path $file.FullName -Value $content -NoNewline
            $filesChanged++
            $totalReplacements += $fileReplacements
            Write-Host "  Fixed $($file.Name): $fileReplacements patterns" -ForegroundColor Green
        }
    } catch {
        Write-Warning "Failed to process $($file.FullName): $_"
    }
}

Write-Host "`nSummary:" -ForegroundColor Cyan
Write-Host "  Files changed: $filesChanged" -ForegroundColor Yellow
Write-Host "  Total pattern fixes: $totalReplacements" -ForegroundColor Yellow

if ($filesChanged -gt 0) {
    Write-Host "`nRecommendation: Run 'dotnet build' to verify fixes" -ForegroundColor Cyan
} else {
    Write-Host "`nNo changes needed - all files are clean" -ForegroundColor Green
}
