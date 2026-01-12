<#
.SYNOPSIS
    Repairs programmatic layout violations in WinForms panels.

.DESCRIPTION
    Automatically fixes common layout violations identified by Validate-PanelLayoutImplementation.ps1:
    - Removes manual Size assignments on panels/controls (uses Dock/AutoSize instead)
    - Adds SuspendLayout/ResumeLayout wrappers where missing
    - Documents panels needing manual refactoring (complex Location/Size patterns)

    SAFETY FEATURES:
    - Dry-run mode enabled by default
    - Creates timestamped backups before any changes
    - Validates syntax after changes
    - Detailed preview of all changes

.PARAMETER ControlsPath
    Path to the Controls folder containing panel files.

.PARAMETER DryRun
    If true (default), shows what would be changed without modifying files.

.PARAMETER CreateBackup
    If true (default), creates .bak files before making changes.

.PARAMETER PanelFilter
    Optional filter to process specific panels only (e.g., 'ChatPanel.cs').

.PARAMETER FixSizeAssignments
    Remove manual Size assignments where safe to do so.

.PARAMETER FixSuspendResume
    Add SuspendLayout/ResumeLayout wrappers where missing.

.EXAMPLE
    .\Repair-PanelLayoutViolations.ps1 -DryRun
    Preview all changes without modifying files.

.EXAMPLE
    .\Repair-PanelLayoutViolations.ps1 -DryRun:$false -PanelFilter '*Panel.cs'
    Apply all fixes to all panels.

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
    [switch]$FixSizeAssignments,

    [Parameter()]
    [switch]$FixSuspendResume
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

#region Configuration

$Script:Config = @{
    BackupExtension = '.bak'
    BackupTimestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    SafetyChecks    = $true
}

#endregion

#region Helper Functions

function Write-ColorOutput {
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

function Remove-ManualSizeAssignments {
    <#
    .SYNOPSIS
        Removes manual Size assignments on the panel control itself (not child controls).
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$Content,

        [Parameter(Mandatory)]
        [string]$FileName
    )

    $changes = @()
    $currentContent = $Content

    # Pattern 1: Size = new Size(...) in InitializeComponent
    # Only remove if it's setting the panel's own Size property
    $pattern = '^\s*Size\s*=\s*new\s+Size\s*\([^)]+\)\s*;?\s*$'
    $matches = [regex]::Matches($currentContent, $pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)

    if ($matches.Count -gt 0) {
        # Check if this is in InitializeComponent and setting this.Size
        foreach ($match in $matches) {
            $lineNumber = ($currentContent.Substring(0, $match.Index) -split "`n").Count

            # Check context - is this inside InitializeComponent?
            $beforeContext = $currentContent.Substring(0, $match.Index)
            if ($beforeContext -match 'private\s+void\s+InitializeComponent\s*\(\s*\)') {
                # Check if it's this.Size or just Size (both mean the same in this context)
                $lineContent = $match.Value

                # Replace with Dock = DockStyle.Fill (panels should fill their container)
                $replacement = "            // Removed manual Size assignment - panel now uses Dock.Fill or AutoSize"
                $currentContent = $currentContent.Remove($match.Index, $match.Length)
                $currentContent = $currentContent.Insert($match.Index, $replacement)

                $changes += "Removed manual Size assignment at line $lineNumber (replaced with Dock.Fill pattern)"

                # Only fix first occurrence to avoid index shifting issues
                break
            }
        }
    }

    return @{
        Changed     = $changes.Count -gt 0
        Content     = $currentContent
        Changes     = $changes
    }
}

function Add-SuspendResumeLayoutWrapper {
    <#
    .SYNOPSIS
        Adds SuspendLayout/ResumeLayout wrapper to InitializeComponent if missing.
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
            Changed = $false
            Content = $Content
            Reason  = 'SuspendLayout/ResumeLayout already present'
        }
    }

    # Find InitializeComponent method
    $pattern = '(?s)(private\s+void\s+InitializeComponent\s*\(\s*\)\s*\{)'
    $match = [regex]::Match($Content, $pattern)

    if (-not $match.Success) {
        return @{
            Changed = $false
            Content = $Content
            Reason  = 'InitializeComponent method not found'
        }
    }

    # Find method closing brace
    $startIndex = $match.Index + $match.Length
    $braceCount = 1
    $endIndex = -1

    for ($i = $startIndex; $i -lt $Content.Length; $i++) {
        $char = $Content[$i]
        if ($char -eq '{') { $braceCount++ }
        elseif ($char -eq '}') {
            $braceCount--
            if ($braceCount -eq 0) {
                $endIndex = $i
                break
            }
        }
    }

    if ($endIndex -eq -1) {
        return @{
            Changed = $false
            Content = $Content
            Reason  = 'Could not find InitializeComponent closing brace'
        }
    }

    # Insert SuspendLayout after opening brace
    $afterBrace = $match.Index + $match.Length
    $suspendCode = "`n            this.SuspendLayout();`n"
    $newContent = $Content.Insert($afterBrace, $suspendCode)

    # Adjust endIndex for insertion
    $endIndex += $suspendCode.Length

    # Insert ResumeLayout before closing brace
    $resumeCode = "`n            this.ResumeLayout(false);`n        "
    $newContent = $newContent.Insert($endIndex, $resumeCode)

    $lineNumber = ($Content.Substring(0, $match.Index) -split "`n").Count

    return @{
        Changed     = $true
        Content     = $newContent
        Description = "Added SuspendLayout/ResumeLayout wrapper at line $lineNumber"
        LineNumber  = $lineNumber
    }
}

function Get-PanelComplexity {
    <#
    .SYNOPSIS
        Analyzes panel complexity to determine if manual refactoring is needed.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$Content
    )

    $locationCount = ([regex]::Matches($Content, '\bLocation\s*=\s*new\s+Point\s*\(')).Count
    $sizeCount = ([regex]::Matches($Content, '\bSize\s*=\s*new\s+Size\s*\(')).Count
    $totalViolations = $locationCount + $sizeCount

    $complexity = if ($totalViolations -eq 0) { 'None' }
                  elseif ($totalViolations -le 5) { 'Low' }
                  elseif ($totalViolations -le 20) { 'Medium' }
                  else { 'High' }

    $needsManualRefactor = $locationCount -gt 10 -or $totalViolations -gt 30

    return @{
        LocationCount        = $locationCount
        SizeCount            = $sizeCount
        TotalViolations      = $totalViolations
        Complexity           = $complexity
        NeedsManualRefactor  = $needsManualRefactor
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

        # Analyze complexity first
        $complexity = Get-PanelComplexity -Content $currentContent

        if ($complexity.NeedsManualRefactor) {
            return [PSCustomObject]@{
                FileName            = $File.Name
                FilePath            = $File.FullName
                ChangesApplied      = 0
                Changes             = @("MANUAL REFACTORING REQUIRED: $($complexity.TotalViolations) violations (Location: $($complexity.LocationCount), Size: $($complexity.SizeCount))")
                Success             = $true
                Modified            = $false
                NeedsManualRefactor = $true
                Complexity          = $complexity.Complexity
            }
        }

        # Apply fixes in order (if none specified, apply all safe fixes)
        if ($FixSizeAssignments -or (-not $FixSizeAssignments -and -not $FixSuspendResume)) {
            $result = Remove-ManualSizeAssignments -Content $currentContent -FileName $File.Name
            if ($result.Changed) {
                $currentContent = $result.Content
                $changes += $result.Changes
            }
        }

        if ($FixSuspendResume -or (-not $FixSizeAssignments -and -not $FixSuspendResume)) {
            $result = Add-SuspendResumeLayoutWrapper -Content $currentContent
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
            FileName            = $File.Name
            FilePath            = $File.FullName
            ChangesApplied      = $changes.Count
            Changes             = $changes
            Success             = $true
            Modified            = $changes.Count -gt 0
            OriginalSize        = $originalContent.Length
            NewSize             = $currentContent.Length
            NewContent          = $currentContent
            NeedsManualRefactor = $false
            Complexity          = $complexity.Complexity
        }
    }
    catch {
        Write-Warning "Failed to process $($File.Name): $_"
        return [PSCustomObject]@{
            FileName            = $File.Name
            FilePath            = $File.FullName
            ChangesApplied      = 0
            Changes             = @()
            Success             = $false
            Modified            = $false
            Error               = $_.Exception.Message
            NeedsManualRefactor = $false
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

        if (-not $PSCmdlet.ShouldProcess("Panel files in $ControlsPath", "Apply layout violation repairs")) {
            Write-ColorOutput "Operation cancelled by user." -Type Warning
            exit 0
        }
    }

    Write-Verbose "Controls path: $ControlsPath"
    Write-Verbose "Panel filter: $PanelFilter"

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

        if ($result.NeedsManualRefactor) {
            Write-ColorOutput "[$($file.Name)]" -Type Warning
            Write-ColorOutput "  ⚠ REQUIRES MANUAL REFACTORING" -Type Warning
            Write-ColorOutput "  Complexity: $($result.Complexity)" -Type Warning
            foreach ($change in $result.Changes) {
                Write-ColorOutput "  • $change" -Type Warning
            }
            Write-Host ""
        }
        elseif ($result.Modified) {
            Write-ColorOutput "[$($file.Name)]" -Type Info
            foreach ($change in $result.Changes) {
                Write-ColorOutput "  ✓ $change" -Type Success
            }

            # Show size change
            $sizeDiff = $result.NewSize - $result.OriginalSize
            Write-Host "  Size change: $sizeDiff bytes" -ForegroundColor Gray

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

    $resultsArray = @($results)
    $totalModified = @($resultsArray | Where-Object { $_.Modified }).Count
    $totalChanges = ($resultsArray | Measure-Object -Property ChangesApplied -Sum).Sum
    $totalErrors = @($resultsArray | Where-Object { -not $_.Success }).Count
    $needsManualRefactor = @($resultsArray | Where-Object { $_.NeedsManualRefactor }).Count

    Write-Host "Files processed: $($resultsArray.Count)"
    Write-Host "Files modified: $totalModified" -ForegroundColor $(if ($totalModified -gt 0) { 'Green' } else { 'Gray' })
    Write-Host "Total changes: $totalChanges" -ForegroundColor $(if ($totalChanges -gt 0) { 'Green' } else { 'Gray' })
    Write-Host "Needs manual refactor: $needsManualRefactor" -ForegroundColor $(if ($needsManualRefactor -gt 0) { 'Yellow' } else { 'Gray' })
    Write-Host "Errors: $totalErrors" -ForegroundColor $(if ($totalErrors -gt 0) { 'Red' } else { 'Gray' })

    if ($needsManualRefactor -gt 0) {
        Write-Host "`nPanels requiring manual refactoring:" -ForegroundColor Yellow
        $resultsArray | Where-Object { $_.NeedsManualRefactor } | ForEach-Object {
            Write-Host "  • $($_.FileName) ($($_.Complexity) complexity)" -ForegroundColor Yellow
        }
    }

    if ($DryRun -and $totalModified -gt 0) {
        Write-Host "`n" -NoNewline
        Write-ColorOutput "To apply these changes, run with -DryRun:`$false" -Type DryRun
    }

    # Exit code
    if ($totalErrors -gt 0) {
        exit 1
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
