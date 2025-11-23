#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Scan and assist removal of Syncfusion and Prism references, sanitize NuGet.config,
    optionally run restore/build, create branch and commit changes (opt-in).

.DESCRIPTION
    This script performs a safe, auditable sweep across the repository and:
    - Finds files referencing "Syncfusion" and "Prism" and produces a report
    - Backs up and sanitizes root `NuGet.config` and subdirectory nuget.config files (removes feeds containing "syncfusion")
    - Optionally runs `dotnet restore` and `dotnet build`
    - Optionally creates a git branch `refactor-remove-syncfusion-prism` and commits changes

.PARAMETER RunRestoreBuild
    If specified, run `dotnet restore` then `dotnet build` after changes.

.PARAMETER AutoCommit
    If specified, automatically `git add` and `git commit` the sanitized `NuGet.config` and the generated report on the new branch.

.PARAMETER CleanArtifacts
    If specified, attempts to remove `bin` and `obj` folders under the repo (safe delete).

.PARAMETER RepoRoot
    Path to repository root. Defaults to two levels up from the script location.

.EXAMPLE
    pwsh ./scripts/tools/remove-syncfusion-prism.ps1 -RunRestoreBuild -AutoCommit
#>

[CmdletBinding()]
param(
    [switch]$RunRestoreBuild,
    [switch]$AutoCommit,
    [switch]$CleanArtifacts,
    [string]$RepoRoot = $(Resolve-Path (Join-Path $PSScriptRoot '..\..') ).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Output "Repository root: $RepoRoot"

# Prepare report path
$logsDir = Join-Path $RepoRoot 'logs'
if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Path $logsDir | Out-Null }
$reportPath = Join-Path $logsDir 'remove-syncfusion-prism-report.txt'
"Report generated at: $(Get-Date -Format o)`n" | Out-File -FilePath $reportPath -Encoding utf8

# Helper: exclude common build dirs
$excludeDirs = @('bin','obj','.git')

Write-Output "Scanning for occurrences of Syncfusion and Prism (excluding bin/obj/.git)..."
Add-Content -Path $reportPath -Value "==== Occurrences: Syncfusion / Prism ===="

# File extensions to search
$exts = @('.cs','.xaml','.csproj','.props','.targets','.config','.ps1','.psm1','.json','.md')

# Enumerate files safely and search
$files = Get-ChildItem -Path $RepoRoot -Recurse -File -ErrorAction SilentlyContinue
foreach ($file in $files) {
    try {
        if (-not $file -or -not $file.PSIsContainer) {
            # ensure extension matches and path not in bin/obj/.git
            if (($exts -contains $file.Extension) -and ($file.FullName -notmatch '\\(bin|obj)\\') -and ($file.FullName -notmatch '\\.git\\')) {
                $matches = Select-String -Path $file.FullName -Pattern @('Syncfusion','Prism','IEventAggregator') -SimpleMatch -ErrorAction SilentlyContinue
                if ($matches) {
                    foreach ($m in $matches) {
                        $rel = $file.FullName.Replace($RepoRoot + '\\','')
                        $lineText = $m.Line.Trim()
                        $line = "{0}:{1}:{2}: {3}" -f $rel, $m.LineNumber, $m.Match.Value, $lineText
                        Add-Content -Path $reportPath -Value $line
                    }
                }
            }
        }
    } catch {
        Add-Content -Path $reportPath -Value "Failed to search file: $($file.FullName) -> $($_.Exception.Message)"
    }
}

# Summarize results
$occurrences = (Get-Content $reportPath | Select-String -Pattern ':' | Measure-Object).Count
Add-Content -Path $reportPath -Value "`nSummary: $occurrences matches found.`n"
Write-Output "Scan complete. Report written to: $reportPath"

# Backup and sanitize NuGet.config files that contain Syncfusion feeds
Add-Content -Path $reportPath -Value "==== NuGet.config sanitization ===="
$nugetFiles = Get-ChildItem -Path $RepoRoot -Recurse -Include 'NuGet.config','nuget.config' -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }

if (-not $nugetFiles) {
    Add-Content -Path $reportPath -Value "No nuget.config files found under repo root (aside from default)."
} else {
    foreach ($nf in $nugetFiles) {
        Add-Content -Path $reportPath -Value "Processing: $($nf.FullName)"
        try {
            $xml = [xml](Get-Content -Path $nf.FullName -Raw)
        } catch {
            Add-Content -Path $reportPath -Value "  - Failed to parse XML: $($_.Exception.Message)"
            continue
        }

        $modified = $false
        $ns = $xml.configuration.packageSources
        if ($ns -ne $null) {
            # Collect nodes to remove
            $toRemove = @()
            foreach ($add in $ns.add) {
                $key = $add.key -as [string]
                $val = $add.value -as [string]
                if ($key -match '(?i)syncfusion' -or $val -match '(?i)syncfusion' -or $val -match '\\Syncfusion\\') {
                    $toRemove += $add
                }
            }
            foreach ($n in $toRemove) {
                Add-Content -Path $reportPath -Value "  - Removing package source: $($n.key) -> $($n.value)"
                $ns.RemoveChild($n) | Out-Null
                $modified = $true
            }
        }

        if ($modified) {
            $backup = "$($nf.FullName).bak.$((Get-Date).ToString('yyyyMMddHHmmss'))"
            Copy-Item -Path $nf.FullName -Destination $backup -Force
            Add-Content -Path $reportPath -Value "  - Backed up original to: $backup"
            $xml.Save($nf.FullName)
            Add-Content -Path $reportPath -Value "  - Saved sanitized file: $($nf.FullName)"
        } else {
            Add-Content -Path $reportPath -Value "  - No Syncfusion sources found in this NuGet.config"
        }
    }
}

# Optional: clean build artifacts
if ($CleanArtifacts) {
    Add-Content -Path $reportPath -Value "`n==== Cleaning build artifacts (bin/obj) ===="
    $candidates = Get-ChildItem -Path $RepoRoot -Recurse -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -in @('bin','obj') }
    foreach ($d in $candidates) {
        try {
            Remove-Item -LiteralPath $d.FullName -Recurse -Force -ErrorAction SilentlyContinue
            Add-Content -Path $reportPath -Value "  - Removed: $($d.FullName)"
        } catch {
            Add-Content -Path $reportPath -Value "  - Failed to remove: $($d.FullName) -> $($_.Exception.Message)"
        }
    }
}

# Optionally create branch and commit sanitized files
if ($AutoCommit) {
    Add-Content -Path $reportPath -Value "`n==== Git: create branch and commit ===="
    Push-Location $RepoRoot
    try {
        $branchName = 'refactor-remove-syncfusion-prism'
        # Create branch (if exists, checkout)
        $existing = & git rev-parse --verify $branchName 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Output "Branch $branchName already exists; checking out"
            git checkout $branchName
        } else {
            git checkout -b $branchName
        }

        # Stage changed files (only nuget.config and report)
        git add (Resolve-Path $reportPath).Path
        foreach ($nf in $nugetFiles) {
            git add $nf.FullName
        }

        $commitMsg = 'chore: sanitize NuGet.config and report after removing Syncfusion/Prism references'
        git commit -m $commitMsg || Write-Output "No changes to commit"
        Add-Content -Path $reportPath -Value "  - Committed changes with message: $commitMsg"
    } catch {
        Add-Content -Path $reportPath -Value "  - Git operation failed: $($_.Exception.Message)"
    } finally { Pop-Location }
}

# Optionally run dotnet restore and build to verify no references remain
if ($RunRestoreBuild) {
    Add-Content -Path $reportPath -Value "`n==== dotnet restore/build ===="
    Push-Location $RepoRoot
    try {
        $sln = Get-ChildItem -Path $RepoRoot -Filter *.sln -File -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($sln) {
            Write-Output "Running: dotnet restore $($sln.Name)"
            & dotnet restore $sln.FullName --verbosity minimal 2>&1 | Tee-Object -FilePath (Join-Path $logsDir 'dotnet-restore.log')
            Write-Output "Running: dotnet build $($sln.Name)"
            & dotnet build $sln.FullName --no-restore --configuration Debug --verbosity minimal 2>&1 | Tee-Object -FilePath (Join-Path $logsDir 'dotnet-build.log')
            Add-Content -Path $reportPath -Value "  - dotnet restore/build logs: $logsDir\dotnet-restore.log , $logsDir\dotnet-build.log"
        } else {
            Add-Content -Path $reportPath -Value "  - No solution file found; skipping restore/build"
        }
    } catch {
        Add-Content -Path $reportPath -Value "  - dotnet operations failed: $($_.Exception.Message)"
    } finally { Pop-Location }
}

Add-Content -Path $reportPath -Value "`n==== Completed: $(Get-Date -Format o) ===="
Write-Output "Done. See report: $reportPath"

# Exit with success
exit 0
