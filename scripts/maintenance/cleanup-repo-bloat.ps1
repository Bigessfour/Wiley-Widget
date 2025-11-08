<#
.SYNOPSIS
    Cleans repository bloat including temporary files, build artifacts, and generated files.

.DESCRIPTION
    This script removes common sources of repository bloat:
    - WPF temporary project files (*_wpftmp.csproj)
    - Build artifacts in bin/ and obj/ directories
    - Log files (*.log)
    - Temporary files (*.tmp, *.cache, *.bak)
    - Python cache (__pycache__, *.pyc)
    - Node modules cache
    - Generated manifest files

.PARAMETER DryRun
    Show what would be deleted without actually deleting.

.PARAMETER Force
    Skip confirmation prompts.

.EXAMPLE
    .\cleanup-repo-bloat.ps1 -DryRun
    Preview what would be deleted.

.EXAMPLE
    .\cleanup-repo-bloat.ps1 -Force
    Clean repository without prompts.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter()]
    [switch]$DryRun,

    [Parameter()]
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# Get repository root
$RepoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "🧹 Repository Cleanup Tool" -ForegroundColor Cyan
Write-Host "Repository: $RepoRoot`n" -ForegroundColor Gray

# Define cleanup patterns
$CleanupPatterns = @(
    @{
        Name = "WPF Temporary Project Files"
        Pattern = "*_wpftmp.csproj"
        Recursive = $true
        Color = "Yellow"
    },
    @{
        Name = "Build Artifacts (bin/obj)"
        Pattern = @("bin", "obj")
        Recursive = $false
        IsDirectory = $true
        Color = "Magenta"
    },
    @{
        Name = "Log Files"
        Pattern = "*.log"
        Recursive = $true
        Color = "DarkYellow"
    },
    @{
        Name = "Temporary Files"
        Pattern = @("*.tmp", "*.cache", "*.bak", "*.old")
        Recursive = $true
        Color = "DarkGray"
    },
    @{
        Name = "Python Cache"
        Pattern = @("__pycache__", "*.pyc", "*.pyo", "*.pyd")
        Recursive = $true
        Color = "Blue"
    },
    @{
        Name = "Generated Manifests"
        Pattern = "ai-fetchable-manifest.json"
        Recursive = $true
        Color = "DarkCyan"
    }
)

$TotalSize = 0
$TotalFiles = 0
$TotalDirs = 0

foreach ($cleanup in $CleanupPatterns) {
    Write-Host "`n[$($cleanup.Name)]" -ForegroundColor $cleanup.Color

    $patterns = if ($cleanup.Pattern -is [array]) { $cleanup.Pattern } else { @($cleanup.Pattern) }

    foreach ($pattern in $patterns) {
        $items = @()

        if ($cleanup.Recursive) {
            $items = Get-ChildItem -Path $RepoRoot -Filter $pattern -Recurse -Force -ErrorAction SilentlyContinue
        } else {
            $items = Get-ChildItem -Path $RepoRoot -Filter $pattern -Force -ErrorAction SilentlyContinue
        }

        if ($cleanup.IsDirectory) {
            $items = $items | Where-Object { $_.PSIsContainer }
        }

        foreach ($item in $items) {
            $size = 0
            $fileCount = 0

            if ($item.PSIsContainer) {
                $files = Get-ChildItem -Path $item.FullName -Recurse -File -Force -ErrorAction SilentlyContinue
                $fileCount = $files.Count
                $size = ($files | Measure-Object -Property Length -Sum -ErrorAction SilentlyContinue).Sum
                $TotalDirs++
            } else {
                $size = $item.Length
                $fileCount = 1
                $TotalFiles++
            }

            $TotalSize += $size
            $sizeStr = if ($size -gt 1MB) { "{0:N2} MB" -f ($size / 1MB) }
                      elseif ($size -gt 1KB) { "{0:N2} KB" -f ($size / 1KB) }
                      else { "$size bytes" }

            $relativePath = $item.FullName.Replace($RepoRoot, "").TrimStart('\', '/')

            if ($DryRun) {
                Write-Host "  [DRY RUN] Would delete: $relativePath ($sizeStr" -NoNewline
                if ($item.PSIsContainer) { Write-Host ", $fileCount files" -NoNewline }
                Write-Host ")"
            } else {
                Write-Host "  Deleting: $relativePath ($sizeStr" -NoNewline
                if ($item.PSIsContainer) { Write-Host ", $fileCount files" -NoNewline }
                Write-Host ")"

                try {
                    if ($Force -or $PSCmdlet.ShouldProcess($relativePath, "Delete")) {
                        Remove-Item -Path $item.FullName -Recurse -Force -ErrorAction Stop
                    }
                } catch {
                    Write-Warning "  Failed to delete: $_"
                }
            }
        }
    }
}

# Summary
Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
Write-Host "📊 Cleanup Summary" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan

$totalSizeStr = if ($TotalSize -gt 1GB) { "{0:N2} GB" -f ($TotalSize / 1GB) }
               elseif ($TotalSize -gt 1MB) { "{0:N2} MB" -f ($TotalSize / 1MB) }
               elseif ($TotalSize -gt 1KB) { "{0:N2} KB" -f ($TotalSize / 1KB) }
               else { "$TotalSize bytes" }

if ($DryRun) {
    Write-Host "Would remove:" -ForegroundColor Yellow
} else {
    Write-Host "Removed:" -ForegroundColor Green
}

Write-Host "  • Files: $TotalFiles" -ForegroundColor White
Write-Host "  • Directories: $TotalDirs" -ForegroundColor White
Write-Host "  • Total Size: $totalSizeStr" -ForegroundColor White

if ($DryRun) {
    Write-Host "`n💡 Run without -DryRun to actually delete files" -ForegroundColor Yellow
} else {
    Write-Host "`n✅ Cleanup complete!" -ForegroundColor Green
}

Write-Host ""
