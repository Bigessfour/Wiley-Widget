#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fixes all issues from GradientPanelExt to System.Windows.Forms.Panel migration
.DESCRIPTION
    Removes obsolete GradientPanelExt properties (BackgroundColor, BrushInfo)
    Fixes type conversion issues and cleans up the migration mess
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$SourcePath = "$PSScriptRoot\..\src\WileyWidget.WinForms"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "=== Fixing GradientPanelExt Migration Issues ===" -ForegroundColor Cyan
Write-Host "Target: $SourcePath`n" -ForegroundColor Gray

$patterns = @(
    # Remove BackgroundColor property assignments (CS0117 fix)
    @{
        Name = "Remove BackgroundColor property"
        Pattern = ',\s*BackgroundColor\s*=\s*new\s+BrushInfo\([^)]+\)'
        Replacement = ''
        IsRegex = $true
    }
    @{
        Name = "Remove BackgroundColor with newline"
        Pattern = '\s*BackgroundColor\s*=\s*new\s+BrushInfo\([^)]+\),?\s*\r?\n'
        Replacement = "`n"
        IsRegex = $true
    }

    # Fix type mismatches - use fully qualified names
    @{
        Name = "Fix Panel return type in method signatures"
        Pattern = '(?<=private\s+static\s+\()Panel(?=\s+panel[,\)])'
        Replacement = 'System.Windows.Forms.Panel'
        IsRegex = $true
    }
    @{
        Name = "Fix Panel in tuple return types"
        Pattern = '(?<=\()Panel(?=\s+panel,)'
        Replacement = 'System.Windows.Forms.Panel'
        IsRegex = $true
    }

    # Fix using alias conflicts
    @{
        Name = "Remove Panel alias that conflicts"
        Pattern = 'using\s+Panel\s*=\s*WileyWidget\.WinForms\.Controls\.Panel;\s*\r?\n'
        Replacement = ''
        IsRegex = $true
    }
)

$filesProcessed = 0
$totalPatternMatches = 0

$files = Get-ChildItem -Path $SourcePath -Filter "*.cs" -Recurse -File

foreach ($file in $files) {
    try {
        $content = Get-Content $file.FullName -Raw -ErrorAction Stop
        if (-not $content) { continue }

        $originalContent = $content
        $fileMatches = 0

        foreach ($pattern in $patterns) {
            if ($pattern.IsRegex) {
                $matches = [regex]::Matches($content, $pattern.Pattern)
                if ($matches.Count -gt 0) {
                    $content = [regex]::Replace($content, $pattern.Pattern, $pattern.Replacement)
                    $fileMatches += $matches.Count
                    Write-Host "  [$($file.Name)] $($pattern.Name): $($matches.Count) matches" -ForegroundColor DarkGray
                }
            } else {
                if ($content.Contains($pattern.Pattern)) {
                    $beforeLength = $content.Length
                    $content = $content.Replace($pattern.Pattern, $pattern.Replacement)
                    $fileMatches++
                    Write-Host "  [$($file.Name)] $($pattern.Name): fixed" -ForegroundColor DarkGray
                }
            }
        }

        if ($content -ne $originalContent) {
            Set-Content -Path $file.FullName -Value $content -NoNewline -Encoding UTF8
            $filesProcessed++
            $totalPatternMatches += $fileMatches
            Write-Host "✓ $($file.Name): $fileMatches fixes applied" -ForegroundColor Green
        }
    }
    catch {
        Write-Warning "Failed to process $($file.FullName): $_"
    }
}

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "Files processed: $filesProcessed" -ForegroundColor Yellow
Write-Host "Total fixes: $totalPatternMatches" -ForegroundColor Yellow

if ($filesProcessed -gt 0) {
    Write-Host "`n✓ Migration fixes applied. Run 'dotnet build' to verify." -ForegroundColor Green
} else {
    Write-Host "`n! No changes needed" -ForegroundColor Gray
}
