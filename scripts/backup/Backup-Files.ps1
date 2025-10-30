<#
.SYNOPSIS
    Backup files from a source directory to a destination directory.

.DESCRIPTION
    Recursively copies files from the source directory to the destination directory,
    preserving directory structure. Supports -WhatIf and -Confirm through
    SupportsShouldProcess. Includes optional retention cleanup for files older
    than a configurable number of days.

.PARAMETER SourcePath
    The path to the directory to back up. Must exist.

.PARAMETER DestinationPath
    The destination directory where files will be copied. Will be created if it
    does not exist.

.PARAMETER DaysToKeep
    Optional. If provided and greater than zero, files in the destination
    older than this many days will be removed after the backup. Default: 30.

.PARAMETER IncludeHidden
    If specified, hidden and system files will be included.

.EXAMPLE
    Backup-Files -SourcePath 'C:\Data' -DestinationPath 'D:\Backups\Data' -Verbose

.NOTES
    - PowerShell 7.5.4 compatible
    - Avoids aliases and follows PSScriptAnalyzer best practices
    - Use -WhatIf to preview changes
#>

[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
param (
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [ValidateScript({ Test-Path $_ -PathType Container })]
    [string]
    $SourcePath,

    [Parameter(Mandatory = $true, Position = 1)]
    [ValidateNotNullOrEmpty()]
    [string]
    $DestinationPath,

    [Parameter()]
    [ValidateRange(0, 3650)]
    [int]
    $DaysToKeep = 30,

    [Parameter()]
    [switch]
    $IncludeHidden
)

function Backup-FilesInternal {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param (
        [Parameter(Mandatory = $true)]
        [string] $ResolvedSource,

        [Parameter(Mandatory = $true)]
        [string] $ResolvedDestination,

        [int] $RetentionDays,

        [bool] $IncludeHiddenFiles
    )

    try {
        Write-Verbose "Source: $ResolvedSource"
        Write-Verbose "Destination: $ResolvedDestination"
        Write-Verbose "Retention (days): $RetentionDays"

        # Ensure destination exists
        if ($PSCmdlet.ShouldProcess($ResolvedDestination, 'Create destination directory')) {
            $null = New-Item -Path $ResolvedDestination -ItemType Directory -Force -ErrorAction Stop
        }

        # Build the file enumeration options
        $gciParameters = @{ Path = $ResolvedSource; File = $true; Recurse = $true; ErrorAction = 'Stop' }
        if ($IncludeHiddenFiles) {
            $gciParameters.Add('Force', $true)
        }

        $allFiles = Get-ChildItem @gciParameters
        $total = $allFiles.Count
        $index = 0

        foreach ($file in $allFiles) {
            $index += 1
            $progressPercent = [int](($index / [Math]::Max($total, 1)) * 100)
            Write-Progress -Activity 'Backing up files' -Status $file.FullName -PercentComplete $progressPercent

            # Compute relative path and destination path
            $relative = [System.IO.Path]::GetRelativePath($ResolvedSource, $file.DirectoryName)
            if ([string]::IsNullOrEmpty($relative) -or $relative -eq '.') {
                $targetDir = $ResolvedDestination
            }
            else {
                $targetDir = Join-Path -Path $ResolvedDestination -ChildPath $relative
            }

            # Ensure target directory exists
            if ($PSCmdlet.ShouldProcess($targetDir, 'Create directory')) {
                $null = New-Item -Path $targetDir -ItemType Directory -Force -ErrorAction Stop
            }

            $destFile = Join-Path -Path $targetDir -ChildPath $file.Name

            if ($PSCmdlet.ShouldProcess($file.FullName, "Copy to $destFile")) {
                try {
                    Copy-Item -Path $file.FullName -Destination $destFile -Force -ErrorAction Stop
                    Write-Verbose "Copied: $($file.FullName) -> $destFile"
                }
                catch {
                    Write-Error -Message "Failed to copy '$($file.FullName)' to '$destFile'. Error: $($_.Exception.Message)"
                }
            }
        }

        # Optional retention cleanup
        if ($RetentionDays -gt 0) {
            Write-Verbose "Performing retention cleanup in destination"
            $cutoff = (Get-Date).AddDays(-1 * $RetentionDays)

            $oldFiles = Get-ChildItem -Path $ResolvedDestination -File -Recurse -ErrorAction Stop | Where-Object { $_.LastWriteTime -lt $cutoff }

            foreach ($old in $oldFiles) {
                if ($PSCmdlet.ShouldProcess($old.FullName, 'Remove old backup file')) {
                    try {
                        Remove-Item -Path $old.FullName -Force -ErrorAction Stop
                        Write-Verbose "Removed old file: $($old.FullName)"
                    }
                    catch {
                        Write-Error -Message "Failed to remove '$($old.FullName)'. Error: $($_.Exception.Message)"
                    }
                }
            }
        }

        Write-Verbose 'Backup completed successfully.'
        return $true
    }
    catch {
        Write-Error -Message "Backup failed: $($_.Exception.Message)"
        return $false
    }
}

try {
    # Resolve paths to full canonical forms
    $resolvedSource = (Resolve-Path -Path $SourcePath -ErrorAction Stop | Select-Object -First 1 -ExpandProperty Path).TrimEnd('\')
    $resolvedDestination = (Join-Path -Path (Resolve-Path -Path (Split-Path -Path $DestinationPath -Parent -ErrorAction SilentlyContinue) -ErrorAction SilentlyContinue).Path -ChildPath (Split-Path -Path $DestinationPath -Leaf))

    if ([string]::IsNullOrWhiteSpace($resolvedDestination)) {
        # Destination base not resolvable (doesn't exist yet) — build full path
        $resolvedDestination = [System.IO.Path]::GetFullPath($DestinationPath)
    }

    $success = Backup-FilesInternal -ResolvedSource $resolvedSource -ResolvedDestination $resolvedDestination -RetentionDays $DaysToKeep -IncludeHiddenFiles ([bool]$IncludeHidden)

    if (-not $success) {
        Exit 1
    }
}
catch {
    Write-Error -Message "Script failed: $($_.Exception.Message)"
    Exit 2
}
