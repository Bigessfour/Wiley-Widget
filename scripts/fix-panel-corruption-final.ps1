<#
.SYNOPSIS
    Fixes remaining Panel corruption from bulk find/replace operation.

.DESCRIPTION
    Systematically fixes corruption patterns identified in build errors:
    1. Panel type ambiguity (System.Windows.Forms.Panel vs WileyWidget.WinForms.Controls.Panel)
    2. Missing CornerRadius property (System.Windows.Forms.Panel doesn't have it)
    3. BackgroundColor property (should be BackColor)
    4. Duplicate using directives

.PARAMETER DryRun
    If specified, shows what changes would be made without modifying files.

.PARAMETER Path
    Root path to search for C# files. Defaults to src/WileyWidget.WinForms.

.EXAMPLE
    .\fix-panel-corruption-final.ps1 -DryRun
    Shows what changes would be made without modifying files.

.EXAMPLE
    .\fix-panel-corruption-final.ps1
    Applies all fixes to the codebase.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter()]
    [switch]$DryRun,

    [Parameter()]
    [string]$Path = "src/WileyWidget.WinForms"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Statistics tracking
$script:Stats = @{
    FilesProcessed = 0
    FilesModified = 0
    TypeAmbiguityFixes = 0
    CornerRadiusFixes = 0
    BackgroundColorFixes = 0
    DuplicateUsingFixes = 0
    NewPanelExpressionFixes = 0
    TotalChanges = 0
}

function Write-FixLog {
    param(
        [string]$Message,
        [string]$Level = 'Info'
    )
    
    $color = switch ($Level) {
        'Success' { 'Green' }
        'Warning' { 'Yellow' }
        'Error' { 'Red' }
        default { 'White' }
    }
    
    Write-Host "[$Level] $Message" -ForegroundColor $color
}

function Test-ShouldProcessFile {
    param(
        [string]$FilePath,
        [string]$ChangeDescription
    )
    
    if ($DryRun) {
        Write-FixLog "[DRY RUN] Would modify: $FilePath - $ChangeDescription" 'Warning'
        return $false
    }
    
    if ($PSCmdlet.ShouldProcess($FilePath, $ChangeDescription)) {
        return $true
    }
    
    return $false
}

function Fix-PanelTypeAmbiguity {
    param(
        [string]$Content,
        [string]$FilePath
    )
    
    $modified = $false
    $changeCount = 0
    
    # Only fix files that have "using WileyWidget.WinForms.Controls"
    $hasControlsUsing = $Content -match 'using\s+WileyWidget\.WinForms\.Controls'
    
    if ($hasControlsUsing) {
        # Pattern 1: 'new System.Windows.Forms.Panel' where it should stay System.Windows.Forms.Panel
        # This is correct - keep it as is
        
        # Pattern 2: Variable assignments like "System.Windows.Forms.Panel tile = new System.Windows.Forms.Panel"
        # Need to check context - if it's being assigned to a System.Windows.Forms.Panel variable, keep it
        
        # Pattern 3: The error "Cannot implicitly convert type 'System.Windows.Forms.Panel' to 'WileyWidget.WinForms.Controls.Panel'"
        # This means code is trying to assign System.Windows.Forms.Panel to WileyWidget.WinForms.Controls.Panel variable
        # We need to find these and make them explicit
        
        # Look for variable declarations that should be System.Windows.Forms.Panel but are typed as Panel (ambiguous)
        $pattern = '(?<![a-zA-Z0-9_\.])Panel\s+(?<varname>[a-z][a-zA-Z0-9_]*)\s*=\s*new\s+System\.Windows\.Forms\.Panel\s*\('
        if ($Content -match $pattern) {
            $originalContent = $Content
            $Content = $Content -replace $pattern, 'System.Windows.Forms.Panel ${varname} = new System.Windows.Forms.Panel('
            $matches = [regex]::Matches($originalContent, $pattern)
            $changeCount += $matches.Count
            $modified = $true
            Write-FixLog "  Fixed $($matches.Count) Panel type declarations in $(Split-Path -Leaf $FilePath)"
        }
    }
    
    $script:Stats.TypeAmbiguityFixes += $changeCount
    
    return @{
        Content = $Content
        Modified = $modified
    }
}

function Fix-NewPanelExpressions {
    param(
        [string]$Content,
        [string]$FilePath
    )
    
    $modified = $false
    $changeCount = 0
    
    # Only fix files that have "using WileyWidget.WinForms.Controls"
    $hasControlsUsing = $Content -match 'using\s+WileyWidget\.WinForms\.Controls'
    
    if ($hasControlsUsing) {
        # Pattern: assignments where Panel instance is being created with "new System.Windows.Forms.Panel"
        # and assigned to what should be a System.Windows.Forms.Panel typed variable
        # Example: var tile = new System.Windows.Forms.Panel { ... }
        # Change to: System.Windows.Forms.Panel tile = new System.Windows.Forms.Panel { ... }
        
        $pattern = '\bvar\s+([a-z][a-zA-Z0-9_]*)\s*=\s*new\s+System\.Windows\.Forms\.Panel\s*(?:\(|{)'
        if ($Content -match $pattern) {
            $originalContent = $Content
            $Content = $Content -replace $pattern, 'System.Windows.Forms.Panel $1 = new System.Windows.Forms.Panel '
            $matches = [regex]::Matches($originalContent, $pattern)
            $changeCount += $matches.Count
            $modified = $true
            Write-FixLog "  Fixed $($matches.Count) 'var = new System.Windows.Forms.Panel' expressions in $(Split-Path -Leaf $FilePath)"
        }
    }
    
    $script:Stats.NewPanelExpressionFixes += $changeCount
    
    return @{
        Content = $Content
        Modified = $modified
    }
}

function Fix-CornerRadiusProperty {
    param(
        [string]$Content,
        [string]$FilePath
    )
    
    $modified = $false
    $changeCount = 0
    
    # Pattern: System.Windows.Forms.Panel doesn't have CornerRadius property
    # Error: 'Panel' does not contain a definition for 'CornerRadius'
    # This typically appears in object initializers
    # Example: new Panel { CornerRadius = 2 }
    # Solution: Remove the CornerRadius line OR change to custom Panel if needed
    
    # Match CornerRadius = value inside object initializers
    $pattern = ',\s*CornerRadius\s*=\s*\d+\s*(?=\r?\n|,|})'
    if ($Content -match $pattern) {
        $originalContent = $Content
        $Content = $Content -replace $pattern, ''
        $matches = [regex]::Matches($originalContent, $pattern)
        $changeCount += $matches.Count
        $modified = $true
        Write-FixLog "  Removed $($matches.Count) CornerRadius properties in $(Split-Path -Leaf $FilePath)"
    }
    
    # Also match CornerRadius at start of initializer
    $pattern2 = '{\s*CornerRadius\s*=\s*\d+\s*,\s*'
    if ($Content -match $pattern2) {
        $originalContent = $Content
        $Content = $Content -replace $pattern2, '{ '
        $matches = [regex]::Matches($originalContent, $pattern2)
        $changeCount += $matches.Count
        $modified = $true
        Write-FixLog "  Removed $($matches.Count) leading CornerRadius properties in $(Split-Path -Leaf $FilePath)"
    }
    
    $script:Stats.CornerRadiusFixes += $changeCount
    
    return @{
        Content = $Content
        Modified = $modified
    }
}

function Fix-BackgroundColorProperty {
    param(
        [string]$Content,
        [string]$FilePath
    )
    
    $modified = $false
    $changeCount = 0
    
    # Pattern: BackgroundColor doesn't exist - should be BackColor
    # Error: does not contain a definition for 'BackgroundColor'
    # Example: control.BackgroundColor = Color.White
    # Solution: Change to BackColor
    
    $pattern = '\.BackgroundColor\s*='
    if ($Content -match $pattern) {
        $originalContent = $Content
        $Content = $Content -replace $pattern, '.BackColor ='
        $matches = [regex]::Matches($originalContent, $pattern)
        $changeCount += $matches.Count
        $modified = $true
        Write-FixLog "  Fixed $($matches.Count) BackgroundColor → BackColor in $(Split-Path -Leaf $FilePath)"
    }
    
    $script:Stats.BackgroundColorFixes += $changeCount
    
    return @{
        Content = $Content
        Modified = $modified
    }
}

function Fix-DuplicateUsings {
    param(
        [string]$Content,
        [string]$FilePath
    )
    
    $modified = $false
    $changeCount = 0
    
    # Pattern: Duplicate using directives (CS0105 error)
    # Example: using WileyWidget.Data; appears twice
    # Solution: Remove duplicates
    
    # Extract all using lines
    $usingLines = [System.Collections.Generic.List[string]]::new()
    $seenUsings = [System.Collections.Generic.HashSet[string]]::new()
    $lines = $Content -split "`r?`n"
    $newLines = [System.Collections.Generic.List[string]]::new()
    
    foreach ($line in $lines) {
        if ($line -match '^\s*using\s+([\w\.]+)\s*;\s*$') {
            $usingNamespace = $Matches[1]
            if ($seenUsings.Contains($usingNamespace)) {
                # Skip duplicate
                $changeCount++
                $modified = $true
                Write-FixLog "  Removed duplicate using: $usingNamespace in $(Split-Path -Leaf $FilePath)"
                continue
            }
            $seenUsings.Add($usingNamespace) | Out-Null
        }
        $newLines.Add($line)
    }
    
    if ($modified) {
        $Content = $newLines -join "`r`n"
    }
    
    $script:Stats.DuplicateUsingFixes += $changeCount
    
    return @{
        Content = $Content
        Modified = $modified
    }
}

function Repair-CSharpFile {
    param(
        [string]$FilePath
    )
    
    $script:Stats.FilesProcessed++
    
    try {
        $content = Get-Content -Path $FilePath -Raw -Encoding UTF8
        $originalContent = $content
        $fileModified = $false
        
        # Apply all fix functions in order
        $result = Fix-DuplicateUsings -Content $content -FilePath $FilePath
        $content = $result.Content
        $fileModified = $fileModified -or $result.Modified
        
        $result = Fix-PanelTypeAmbiguity -Content $content -FilePath $FilePath
        $content = $result.Content
        $fileModified = $fileModified -or $result.Modified
        
        $result = Fix-NewPanelExpressions -Content $content -FilePath $FilePath
        $content = $result.Content
        $fileModified = $fileModified -or $result.Modified
        
        $result = Fix-CornerRadiusProperty -Content $content -FilePath $FilePath
        $content = $result.Content
        $fileModified = $fileModified -or $result.Modified
        
        $result = Fix-BackgroundColorProperty -Content $content -FilePath $FilePath
        $content = $result.Content
        $fileModified = $fileModified -or $result.Modified
        
        # Write changes if modified
        if ($fileModified) {
            if (Test-ShouldProcessFile -FilePath $FilePath -ChangeDescription "Apply Panel corruption fixes") {
                Set-Content -Path $FilePath -Value $content -Encoding UTF8 -NoNewline
                $script:Stats.FilesModified++
                $script:Stats.TotalChanges++
                Write-FixLog "  ✓ Modified: $(Split-Path -Leaf $FilePath)" 'Success'
            }
        }
    }
    catch {
        Write-FixLog "  ✗ Error processing $FilePath : $_" 'Error'
    }
}

# Main execution
function Invoke-PanelCorruptionFix {
    Write-FixLog "=== Panel Corruption Fix Script ===" 'Success'
    Write-FixLog "Mode: $(if ($DryRun) { 'DRY RUN (no changes will be made)' } else { 'LIVE (files will be modified)' })"
    Write-FixLog "Path: $Path"
    Write-FixLog ""
    
    # Validate path
    $fullPath = Join-Path $PSScriptRoot ".." $Path
    if (-not (Test-Path $fullPath)) {
        Write-FixLog "Path not found: $fullPath" 'Error'
        return
    }
    
    # Get all C# files
    $csFiles = Get-ChildItem -Path $fullPath -Filter "*.cs" -Recurse | Where-Object {
        $_.FullName -notmatch '\\obj\\' -and
        $_.FullName -notmatch '\\bin\\' -and
        $_.Name -notmatch '\.Designer\.cs$'
    }
    
    Write-FixLog "Found $($csFiles.Count) C# files to process"
    Write-FixLog ""
    
    # Process each file
    foreach ($file in $csFiles) {
        Repair-CSharpFile -FilePath $file.FullName
    }
    
    # Print summary
    Write-FixLog ""
    Write-FixLog "=== Fix Summary ===" 'Success'
    Write-FixLog "Files processed: $($script:Stats.FilesProcessed)"
    Write-FixLog "Files modified: $($script:Stats.FilesModified)"
    Write-FixLog "Type ambiguity fixes: $($script:Stats.TypeAmbiguityFixes)"
    Write-FixLog "New Panel expression fixes: $($script:Stats.NewPanelExpressionFixes)"
    Write-FixLog "CornerRadius fixes: $($script:Stats.CornerRadiusFixes)"
    Write-FixLog "BackgroundColor fixes: $($script:Stats.BackgroundColorFixes)"
    Write-FixLog "Duplicate using fixes: $($script:Stats.DuplicateUsingFixes)"
    Write-FixLog "Total changes: $($script:Stats.TotalChanges)"
    
    if ($DryRun) {
        Write-FixLog ""
        Write-FixLog "DRY RUN COMPLETE - No files were modified" 'Warning'
        Write-FixLog "Run without -DryRun to apply changes"
    }
    else {
        Write-FixLog ""
        Write-FixLog "✓ All fixes applied successfully!" 'Success'
        Write-FixLog "Recommend running: dotnet build src\WileyWidget.WinForms\WileyWidget.WinForms.csproj"
    }
}

# Execute
Invoke-PanelCorruptionFix
