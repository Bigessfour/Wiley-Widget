﻿<#
.SYNOPSIS
    Generates a fetchability resources manifest for CI/CD pipelines

.DESCRIPTION
    This script creates a machine-readable JSON manifest containing SHA256 hashes,
    timestamps, and metadata for all files in the repository (tracked and untracked).
    Used for file integrity verification and CI/CD pipeline automation.

.PARAMETER OutputPath
    Path where the manifest file will be created. Defaults to "fetchability-resources.json"

.PARAMETER ExcludePatterns
    Array of directory patterns to exclude from the manifest

.EXAMPLE
    .\Generate-FetchabilityManifest.ps1

.EXAMPLE
    .\Generate-FetchabilityManifest.ps1 -OutputPath "custom-manifest.json"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "fetchability-resources.json",

    [System.Diagnostics.CodeAnalysis.SuppressMessage("PSReviewUnusedParameter", "ExcludePatterns")]
    [Parameter(Mandatory = $false)]
    [string[]]$ExcludePatterns = @(
        ".git",
        "bin",
        "obj",
        "tmp",
        ".tmp",
        ".vs",
        "TestResults",
        "node_modules",
        ".trunk",
        "*.tmp",
        "*.log",
        "*.cache",
        "*.user",
        "*.suo",
        "Thumbs.db",
        "Desktop.ini"
    )
)

begin {
    Write-Verbose "Starting fetchability manifest generation..."

    # Ensure ExcludePatterns parameter is recognized as used
    if ($ExcludePatterns.Count -gt 0) {
        Write-Verbose "Using $($ExcludePatterns.Count) exclusion patterns"
    }

    # Ensure we're in a git repository
    if (-not (Test-Path ".git")) {
        throw "This script must be run from the root of a Git repository."
    }

    # Function to calculate SHA256 hash of a file
    function Get-FileSHA256 {
        param([string]$FilePath)

        try {
            $hasher = [System.Security.Cryptography.SHA256]::Create()
            $stream = [System.IO.File]::OpenRead($FilePath)
            $hash = $hasher.ComputeHash($stream)
            $stream.Close()
            return [BitConverter]::ToString($hash).Replace("-", "").ToLower()
        }
        catch {
            Write-Warning "Could not calculate hash for file: $FilePath. Error: $($_.Exception.Message)"
            return $null
        }
    }

    # Function to check if file is tracked by git
    function Test-GitTracked {
        param([string]$FilePath)

        try {
            & git ls-files --error-unmatch $FilePath 2>$null | Out-Null
            return $LASTEXITCODE -eq 0
        }
        catch {
            return $false
        }
    }

    # Function to get git repository information
    function Get-GitInfo {
        $gitInfo = @{
            CommitHash = $null
            Branch     = $null
            IsDirty    = $false
            RemoteUrl  = $null
        }

        try {
            $gitInfo.CommitHash = & git rev-parse HEAD 2>$null
            $gitInfo.Branch = & git rev-parse --abbrev-ref HEAD 2>$null

            # Check if repository is dirty
            $statusOutput = & git status --porcelain 2>$null
            $gitInfo.IsDirty = ($null -ne $statusOutput -and $statusOutput.Length -gt 0)

            # Get remote URL
            $remoteUrl = & git config --get remote.origin.url 2>$null
            if ($remoteUrl) {
                $gitInfo.RemoteUrl = $remoteUrl
            }
        }
        catch {
            Write-Warning "Could not retrieve git information: $($_.Exception.Message)"
        }

        return $gitInfo
    }

    # Function to check if path should be excluded
    function Test-ShouldExclude {
        param([string]$Path)

        foreach ($pattern in $ExcludePatterns) {
            if ($Path -like "*$pattern*" -or $Path -match [regex]::Escape($pattern)) {
                return $true
            }
        }
        return $false
    }
}

process {
    Write-Information "🔍 Scanning repository files..." -InformationAction Continue

    # Get git information
    $gitInfo = Get-GitInfo

    # Get all files in the repository
    $allFiles = Get-ChildItem -Path "." -File -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object { -not (Test-ShouldExclude $_.FullName) } |
        Select-Object -ExpandProperty FullName

    Write-Information "📁 Found $($allFiles.Count) files to process" -InformationAction Continue

    # Process each file
    $fileManifest = @()
    $processedCount = 0

    foreach ($file in $allFiles) {
        $processedCount++
        Write-Progress -Activity "Processing files" -Status "$processedCount/$($allFiles.Count)" -PercentComplete (($processedCount / $allFiles.Count) * 100)

        try {
            $relativePath = [System.IO.Path]::GetRelativePath((Get-Location), $file)
            $fileInfo = Get-Item $file

            # Calculate SHA256
            $sha256 = Get-FileSHA256 -FilePath $file
            if (-not $sha256) {
                Write-Warning "Skipping file due to hash calculation failure: $relativePath"
                continue
            }

            # Check if file is git tracked
            $isTracked = Test-GitTracked -FilePath $relativePath

            # Create file entry
            $fileEntry = @{
                path         = $relativePath.Replace("\", "/")  # Use forward slashes for consistency
                sha256       = $sha256
                size         = $fileInfo.Length
                lastModified = $fileInfo.LastWriteTimeUtc.ToString("o")  # ISO 8601 format
                tracked      = $isTracked
                extension    = $fileInfo.Extension
            }

            $fileManifest += $fileEntry

        }
        catch {
            Write-Warning "Error processing file '$file': $($_.Exception.Message)"
        }
    }

    Write-Progress -Activity "Processing files" -Completed

    # Create manifest object
    $manifest = @{
        metadata = @{
            generatedAt = (Get-Date).ToUniversalTime().ToString("o")
            generator   = "Generate-FetchabilityManifest.ps1"
            repository  = @{
                commitHash = $gitInfo.CommitHash
                branch     = $gitInfo.Branch
                isDirty    = $gitInfo.IsDirty
                remoteUrl  = $gitInfo.RemoteUrl
            }
            statistics  = @{
                totalFiles     = $fileManifest.Count
                trackedFiles   = ($fileManifest | Where-Object { $_.tracked }).Count
                untrackedFiles = ($fileManifest | Where-Object { -not $_.tracked }).Count
                totalSize      = ($fileManifest | Measure-Object -Property size -Sum).Sum
            }
        }
        files    = $fileManifest | Sort-Object -Property path
    }

    # Write manifest to file
    Write-Information "💾 Writing manifest to $OutputPath..." -InformationAction Continue

    try {
        $manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputPath -Encoding UTF8
        Write-Information "✅ Manifest successfully created: $OutputPath" -InformationAction Continue
        Write-Information "📊 Statistics:" -InformationAction Continue
        Write-Information "   • Total files: $($manifest.metadata.statistics.totalFiles)" -InformationAction Continue
        Write-Information "   • Tracked: $($manifest.metadata.statistics.trackedFiles)" -InformationAction Continue
        Write-Information "   • Untracked: $($manifest.metadata.statistics.untrackedFiles)" -InformationAction Continue
        Write-Information "   • Total size: $([math]::Round($manifest.metadata.statistics.totalSize / 1MB, 2)) MB" -InformationAction Continue
    }
    catch {
        throw "Failed to write manifest file: $($_.Exception.Message)"
    }
}

end {
    Write-Information "🎉 Fetchability manifest generation completed!" -InformationAction Continue
}
