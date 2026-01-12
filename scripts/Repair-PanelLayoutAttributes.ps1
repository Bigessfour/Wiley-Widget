<#
.SYNOPSIS
    Repairs missing layout attributes in WinForms panels per Syncfusion documentation.

.DESCRIPTION
    Automatically adds missing critical layout attributes to panel files:
    - AutoScaleMode.Dpi
    - SuspendLayout/ResumeLayout wrapper
    - SfSkinManager.SetVisualStyle calls

    SAFETY FEATURES:
    - Dry-run mode enabled by default
    - Creates timestamped backups before any changes
    - Validates syntax after changes
    - Detailed preview of all changes
    - Rollback capability

.PARAMETER ControlsPath
    Path to the Controls folder containing panel files.

.PARAMETER DryRun
    If true (default), shows what would be changed without modifying files.

.PARAMETER CreateBackup
    If true (default), creates .bak files before making changes.

.PARAMETER PanelFilter
    Optional filter to process specific panels only (e.g., 'ChatPanel', 'Dashboard*').

.PARAMETER FixAutoScaleMode
    Add AutoScaleMode.Dpi to InitializeComponent.

.PARAMETER FixSuspendResume
    Wrap control additions in SuspendLayout/ResumeLayout.

.PARAMETER FixSkinManager
    Add SfSkinManager.SetVisualStyle calls.

.EXAMPLE
    .\Repair-PanelLayoutAttributes.ps1 -DryRun
    Preview changes without modifying any files.

.EXAMPLE
    .\Repair-PanelLayoutAttributes.ps1 -DryRun:$false -PanelFilter 'ChatPanel.cs'
    Apply fixes to ChatPanel only.

.EXAMPLE
    .\Repair-PanelLayoutAttributes.ps1 -DryRun:$false -FixAutoScaleMode -FixSuspendResume
    Apply only AutoScaleMode and SuspendLayout fixes.

.NOTES
    Author: Wiley Widget Development Team
    Date: 2026-01-11
    Requires: PowerShell 7+
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter()]
    [ValidateScript({ Test-Path $_ -PathType Container })]
    [string]$ControlsPath = "$PSScriptRoot\..\src\WileyWidget.WinForms\Controls",

    [Parameter()]
    [switch]$DryRun = $true,

    [Parameter()]
    [switch]$CreateBackup = $true,
    [Parameter()]
    [string]$PanelFilter = '*Panel.cs',

    [Parameter()]
    [switch]$FixAutoScaleMode,

    [Parameter()]
    [switch]$FixSuspendResume,

    [Parameter()]
    [switch]$FixSkinManager
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

#region Configuration

$Script:Config = @{
    BackupExtension     = '.bak'
    BackupTimestamp     = Get-Date -Format 'yyyyMMdd_HHmmss'
    ValidationTimeout   = 30
    MaxLineLength       = 10000
    SafetyChecks        = $true
}

#endregion

#region Helper Functions

function Write-ColorOutput {
    <#
    .SYNOPSIS
        Writes colored console output for better readability.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter()]
        [ValidateSet('Info', 'Success', 'Warning', 'Error', 'DryRun')]
        [string]$Type = 'Info'
    )

    $color = switch ($Type) {
        'Info' { 'Cyan' }
        'Success' { 'Green' }
        'Warning' { 'Yellow' }
        'Error' { 'Red' }
        'DryRun' { 'Magenta' }
    }

    Write-Host $Message -ForegroundColor $color
}

function Backup-PanelFile {
    <#
    .SYNOPSIS
        Creates a timestamped backup of a panel file.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath
    )

    if (-not $CreateBackup) {
        return $null
    }

    $backupPath = "$FilePath.$($Script:Config.BackupExtension).$($Script:Config.BackupTimestamp)"

    try {
        Copy-Item -Path $FilePath -Destination $backupPath -Force -ErrorAction Stop
        Write-Verbose "Backup created: $backupPath"
        return $backupPath
    }
    catch {
        Write-Error "Failed to create backup: $_"
        throw
    }
}

function Test-CSharpSyntax {
    <#
    .SYNOPSIS
        Validates C# file syntax using basic pattern matching.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Content
    )

    # Basic syntax validation
    $checks = @(
        @{ Name = 'Balanced braces'; Test = { ($Content -split '\{').Count -eq ($Content -split '\}').Count } }
        @{ Name = 'Balanced parens'; Test = { ($Content -split '\(').Count -eq ($Content -split '\)').Count } }
    )

    foreach ($check in $checks) {
        if (-not (& $check.Test)) {
            Write-Warning "Syntax check failed: $($check.Name)"
            return $false
        }
    }

    return $true
}

function Find-InitializeComponentMethod {
    <#
    .SYNOPSIS
        Finds the InitializeComponent method location in file content.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$Content
    )

    # Find InitializeComponent method
    $pattern = '(?s)private\s+void\s+InitializeComponent\s*\(\s*\)\s*\{'
    $match = [regex]::Match($Content, $pattern)

    if (-not $match.Success) {
        return @{ Found = $false }
    }

    # Find method bounds
    $startIndex = $match.Index
    $braceCount = 0
    $inMethod = $false
    $endIndex = -1

    for ($i = $startIndex; $i -lt $Content.Length; $i++) {
        $char = $Content[$i]

        if ($char -eq '{') {
            $braceCount++
            $inMethod = $true
        }
        elseif ($char -eq '}') {
            $braceCount--
            if ($inMethod -and $braceCount -eq 0) {
                $endIndex = $i
                break
            }
        }
    }

    if ($endIndex -eq -1) {
        return @{ Found = $false }
    }

    $methodContent = $Content.Substring($startIndex, $endIndex - $startIndex + 1)

    return @{
        Found         = $true
        StartIndex    = $startIndex
        EndIndex      = $endIndex
        Content       = $methodContent
        LineNumber    = ($Content.Substring(0, $startIndex) -split "`n").Count
    }
}

function Add-AutoScaleModeAttribute {
    <#
    .SYNOPSIS
        Adds AutoScaleMode.Dpi to InitializeComponent if missing.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$Content
    )

    # Check if already present
    if ($Content -match 'AutoScaleMode\s*=\s*AutoScaleMode\.Dpi') {
        return @{
            Changed     = $false
            Content     = $Content
            Reason      = 'AutoScaleMode.Dpi already present'
        }
    }

    $initMethod = Find-InitializeComponentMethod -Content $Content

    if (-not $initMethod.Found) {
        return @{
            Changed     = $false
            Content     = $Content
            Reason      = 'InitializeComponent method not found'
        }
    }

    # Find insertion point (after Size or at beginning)
    $methodContent = $initMethod.Content
    $insertPattern = '(this\.Size\s*=\s*new\s+Size\s*\([^)]+\)\s*;)'

    if ($methodContent -match $insertPattern) {
        $replacement = '$1' + "`n            try { this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi; } catch { }"
        $newMethodContent = $methodContent -replace $insertPattern, $replacement
    }
    else {
        # Insert at start of method
        $insertPattern = '(private\s+void\s+InitializeComponent\s*\(\s*\)\s*\{)'
        $replacement = '$1' + "`n            try { this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi; } catch { }"
        $newMethodContent = $methodContent -replace $insertPattern, $replacement
    }

    $newContent = $Content.Substring(0, $initMethod.StartIndex) +
                  $newMethodContent +
                  $Content.Substring($initMethod.EndIndex + 1)

    return @{
        Changed         = $true
        Content         = $newContent
        Description     = "Added AutoScaleMode.Dpi at line $($initMethod.LineNumber)"
        LineNumber      = $initMethod.LineNumber
    }
}

function Add-SuspendResumeLayout {
    <#
    .SYNOPSIS
        Wraps InitializeComponent content in SuspendLayout/ResumeLayout.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$Content
    )

    # Check if already present
    if ($Content -match 'this\.SuspendLayout\s*\(\s*\)' -and $Content -match 'this\.ResumeLayout\s*\(') {
        return @{
            Changed     = $false
            Content     = $Content
            Reason      = 'SuspendLayout/ResumeLayout already present'
        }
    }

    $initMethod = Find-InitializeComponentMethod -Content $Content

    if (-not $initMethod.Found) {
        return @{
            Changed     = $false
            Content     = $Content
            Reason      = 'InitializeComponent method not found'
        }
    }

    # Add SuspendLayout at beginning, ResumeLayout at end
    $methodContent = $initMethod.Content

    # Add SuspendLayout after opening brace
    $methodContent = $methodContent -replace '(private\s+void\s+InitializeComponent\s*\(\s*\)\s*\{)', ('$1' + "`n            this.SuspendLayout();`n")

    # Add ResumeLayout before closing brace
    $methodContent = $methodContent -replace '(\s*)\}(\s*)$', ("`n            this.ResumeLayout(false);`n" + '$1}$2')

    $newContent = $Content.Substring(0, $initMethod.StartIndex) +
                  $methodContent +
                  $Content.Substring($initMethod.EndIndex + 1)

    return @{
        Changed         = $true
        Content         = $newContent
        Description     = "Added SuspendLayout/ResumeLayout wrapper at line $($initMethod.LineNumber)"
        LineNumber      = $initMethod.LineNumber
    }
}

function Add-SkinManagerCall {
    <#
    .SYNOPSIS
        Adds SfSkinManager.SetVisualStyle call if missing.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$Content
    )

    # Check if already present
    if ($Content -match 'SfSkinManager\.SetVisualStyle\s*\(\s*this') {
        return @{
            Changed     = $false
            Content     = $Content
            Reason      = 'SfSkinManager.SetVisualStyle already present'
        }
    }

    # Find constructor or InitializeComponent
    $pattern = '(?s)(public\s+\w+Panel\s*\([^)]*\)\s*(?::\s*base\([^)]*\))?\s*\{)'
    $match = [regex]::Match($Content, $pattern)

    if (-not $match.Success) {
        return @{
            Changed     = $false
            Content     = $Content
            Reason      = 'Constructor not found'
        }
    }

    # Insert SfSkinManager call after InitializeComponent() call
    $insertPattern = '(InitializeComponent\s*\(\s*\)\s*;)'

    if ($Content -match $insertPattern) {
        $skinManagerCode = @'
$1

            // Apply theme via SfSkinManager (single source of truth)
            try { Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, "Office2019Colorful"); } catch { }
'@
        $newContent = $Content -replace $insertPattern, $skinManagerCode

        $lineNumber = ($Content.Substring(0, $match.Index) -split "`n").Count

        return @{
            Changed         = $true
            Content         = $newContent
            Description     = "Added SfSkinManager.SetVisualStyle call at line $lineNumber"
            LineNumber      = $lineNumber
        }
    }

    return @{
        Changed     = $false
        Content     = $Content
        Reason      = 'InitializeComponent() call not found in constructor'
    }
}

function Repair-PanelFile {
    <#
    .SYNOPSIS
        Applies all enabled repairs to a panel file.
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [System.IO.FileInfo]$File
    )

    Write-Verbose "Processing: $($File.Name)"

    try {
        # Read original content
        $originalContent = Get-Content -Path $File.FullName -Raw -ErrorAction Stop
        $currentContent = $originalContent
        $changes = @()

        # Apply fixes in order
        if ($FixAutoScaleMode -or (-not $FixAutoScaleMode -and -not $FixSuspendResume -and -not $FixSkinManager)) {
            $result = Add-AutoScaleModeAttribute -Content $currentContent
            if ($result.Changed) {
                $currentContent = $result.Content
                $changes += $result.Description
            }
        }

        if ($FixSuspendResume -or (-not $FixAutoScaleMode -and -not $FixSuspendResume -and -not $FixSkinManager)) {
            $result = Add-SuspendResumeLayout -Content $currentContent
            if ($result.Changed) {
                $currentContent = $result.Content
                $changes += $result.Description
            }
        }

        if ($FixSkinManager -or (-not $FixAutoScaleMode -and -not $FixSuspendResume -and -not $FixSkinManager)) {
            $result = Add-SkinManagerCall -Content $currentContent
            if ($result.Changed) {
                $currentContent = $result.Content
                $changes += $result.Description
            }
        }

        # Validate syntax
        if ($changes.Count -gt 0 -and $Script:Config.SafetyChecks) {
            if (-not (Test-CSharpSyntax -Content $currentContent)) {
                throw "Syntax validation failed after changes"
            }
        }

        return [PSCustomObject]@{
            FileName        = $File.Name
            FilePath        = $File.FullName
            ChangesApplied  = $changes.Count
            Changes         = $changes
            Success         = $true
            Modified        = $changes.Count -gt 0
            OriginalSize    = $originalContent.Length
            NewSize         = $currentContent.Length
            NewContent      = $currentContent
        }
    }
    catch {
        Write-Warning "Failed to process $($File.Name): $_"
        return [PSCustomObject]@{
            FileName        = $File.Name
            FilePath        = $File.FullName
            ChangesApplied  = 0
            Changes         = @()
            Success         = $false
            Modified        = $false
            Error           = $_.Exception.Message
        }
    }
}

#endregion

#region Main Script

try {
    # Display mode banner
    if ($DryRun) {
        Write-ColorOutput "=== DRY RUN MODE ===" -Type DryRun
        Write-ColorOutput "No files will be modified. Review changes below.`n" -Type DryRun
    }
    else {
        Write-ColorOutput "=== LIVE MODE ===" -Type Warning
        Write-ColorOutput "Files WILL be modified. Backups will be created.`n" -Type Warning

        if (-not $PSCmdlet.ShouldProcess("Panel files in $ControlsPath", "Apply layout attribute repairs")) {
            Write-ColorOutput "Operation cancelled by user." -Type Warning
            exit 0
        }
    }

    Write-Verbose "Controls path: $ControlsPath"
    Write-Verbose "Panel filter: $PanelFilter"
    Write-Verbose "Fixes enabled: AutoScaleMode=$FixAutoScaleMode, SuspendResume=$FixSuspendResume, SkinManager=$FixSkinManager"

    # Find panel files
    $panelFiles = @(Get-ChildItem -Path $ControlsPath -Filter $PanelFilter -File -ErrorAction Stop |
        Where-Object { $_.Name -notmatch '\.(Designer|fixed|New|bak)\.cs$' })

    if ($panelFiles.Count -eq 0) {
        Write-Warning "No panel files found matching filter: $PanelFilter"
        exit 0
    }

    Write-ColorOutput "Found $($panelFiles.Count) panel file(s) to process`n" -Type Info

    # Process each file
    [System.Collections.ArrayList]$results = @()
    foreach ($file in $panelFiles) {
        $result = Repair-PanelFile -File $file
        [void]$results.Add($result)

        if ($result.Modified) {
            Write-ColorOutput "[$($file.Name)]" -Type Info
            foreach ($change in $result.Changes) {
                Write-ColorOutput "  ✓ $change" -Type Success
            }

            # Show size change
            $sizeDiff = $result.NewSize - $result.OriginalSize
            Write-Host "  Size change: +$sizeDiff bytes" -ForegroundColor Gray

            # Apply changes if not dry run
            if (-not $DryRun) {
                # Create backup
                $backupPath = Backup-PanelFile -FilePath $file.FullName

                # Write new content
                Set-Content -Path $file.FullName -Value $result.NewContent -NoNewline -ErrorAction Stop
                Write-ColorOutput "  ✓ Changes applied (backup: $(Split-Path $backupPath -Leaf))" -Type Success
            }
            else {
                Write-ColorOutput "  [DRY RUN - no changes written]" -Type DryRun
            }

            Write-Host ""
        }
        elseif ($result.Success) {
            Write-Host "[$($file.Name)] No changes needed" -ForegroundColor Gray
        }
        else {
            Write-ColorOutput "[$($file.Name)] ERROR: $($result.Error)" -Type Error
        }
    }

    # Summary
    Write-Host "`n==========================================" -ForegroundColor Cyan
    Write-Host "SUMMARY" -ForegroundColor Cyan
    Write-Host "==========================================" -ForegroundColor Cyan

    # Ensure $results is always treated as array
    $resultsArray = @($results)
    $totalModified = @($resultsArray | Where-Object { $_.Modified }).Count
    $totalChanges = ($resultsArray | Measure-Object -Property ChangesApplied -Sum).Sum
    $totalErrors = @($resultsArray | Where-Object { -not $_.Success }).Count

    Write-Host "Files processed: $($resultsArray.Count)"
    Write-Host "Files modified: $totalModified" -ForegroundColor $(if ($totalModified -gt 0) { 'Green' } else { 'Gray' })
    Write-Host "Total changes: $totalChanges" -ForegroundColor $(if ($totalChanges -gt 0) { 'Green' } else { 'Gray' })
    Write-Host "Errors: $totalErrors" -ForegroundColor $(if ($totalErrors -gt 0) { 'Red' } else { 'Gray' })

    if ($DryRun -and $totalModified -gt 0) {
        Write-Host "`n" -NoNewline
        Write-ColorOutput "To apply these changes, run with -DryRun:`$false" -Type DryRun
    }

    # Exit code
    if ($totalErrors -gt 0) {
        exit 1
    }
    elseif ($totalModified -eq 0) {
        exit 0
    }
    else {
        exit 0
    }
}
catch {
    Write-ColorOutput "Fatal error: $_" -Type Error
    Write-Error $_.ScriptStackTrace
    exit 2
}

#endregion
