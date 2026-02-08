<#
.SYNOPSIS
    Analyzes a WinForms panel for unused/underdeveloped elements and modern UI design compliance
.DESCRIPTION
    Scans a panel class file for common issues:
    - Unused private fields
    - Dead/unimplemented event handlers
    - Missing icons on buttons
    - TODO/FIXME comments
    - Commented-out code blocks
    - Orphaned mouse events
    - Incomplete initialization
    - Modern UI design guideline compliance (Fluent Design inspired):
        * Input control heights (40px minimum for single-line)
        * Multi-line text heights (80-120px)
        * Button heights (36-48px ideal)
        * Vertical spacing (24px between rows, 48px between sections)
        * Label widths (160-180px)
        * Padding consistency (8-12px)
        * Font sizes (11-12pt)
        * Base unit multiples (4 or 8px)
.PARAMETER FilePath
    Path to the panel .cs file to analyze
.EXAMPLE
    .\Analyze-Panel.ps1 -FilePath "src\WileyWidget.WinForms\Controls\Panels\PaymentsPanel.cs"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$FilePath
)

$ErrorActionPreference = 'Stop'

# Verify file exists
if (-not (Test-Path $FilePath)) {
    Write-Error "File not found: $FilePath"
    exit 1
}

# Read file content
$content = Get-Content $FilePath -Raw
$lines = Get-Content $FilePath

Write-Host "`n═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🔍 Panel Analysis: $(Split-Path $FilePath -Leaf)" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════`n" -ForegroundColor Cyan

$issues = @()
$warnings = @()
$info = @()

#region Private Field Analysis
Write-Host "🔎 Analyzing private fields..." -ForegroundColor Yellow

$privateFieldPattern = '(?m)^\s*private\s+(?:readonly\s+)?(\w+(?:<[\w,\s<>]+>)?)\s+(_\w+)\s*[;=]'
$privateFields = [regex]::Matches($content, $privateFieldPattern)

foreach ($match in $privateFields) {
    $fieldName = $match.Groups[2].Value

    # Count references (excluding the declaration)
    $usagePattern = [regex]::Escape($fieldName) + '\b'
    $usages = [regex]::Matches($content, $usagePattern)

    # Subtract 1 for the declaration itself
    $usageCount = $usages.Count - 1

    if ($usageCount -eq 0) {
        $issues += @{
            Type = "Unused Field"
            Name = $fieldName
            Details = "Field '$fieldName' is declared but never used"
            Severity = "High"
        }
    }
    elseif ($usageCount -eq 1) {
        # Check if it's only used in initialization
        if ($content -match "(?m)$fieldName\s*=\s*[^;]+;") {
            $warnings += @{
                Type = "Barely Used Field"
                Name = $fieldName
                Details = "Field '$fieldName' is only used in initialization"
                Severity = "Medium"
            }
        }
    }
}
#endregion

#region Button Icon Analysis
Write-Host "🎨 Analyzing button icons..." -ForegroundColor Yellow

# Find button declarations
$buttonPattern = '(?m)^\s*private\s+(?:readonly\s+)?(?:SfButton|Button)\s+(_\w+)\s*[;=]'
$buttons = [regex]::Matches($content, $buttonPattern)

foreach ($match in $buttons) {
    $buttonName = $match.Groups[1].Value

    # Check if LoadIcon or Image is set for this button
    $hasIcon = $content -match "$buttonName\.(?:Image|BackgroundImage)\s*=" -or
               $content -match "LoadIcon\([^)]*\).*$buttonName" -or
               $content -match "$buttonName.*LoadIcon"

    if (-not $hasIcon) {
        $warnings += @{
            Type = "Missing Icon"
            Name = $buttonName
            Details = "Button '$buttonName' has no icon set"
            Severity = "Low"
        }
    }
}
#endregion

#region Event Handler Analysis
Write-Host "🔌 Analyzing event handlers..." -ForegroundColor Yellow

# Find event handler registrations
$eventPattern = '(?m)(\w+)\s*\+=\s*(\w+);'
$events = [regex]::Matches($content, $eventPattern)

foreach ($match in $events) {
    $handlerName = $match.Groups[2].Value

    # Find the handler method
    $handlerMethodPattern = "(?ms)private\s+(?:async\s+)?(?:void|Task)\s+$handlerName\s*\([^)]*\)\s*\{([^}]*)\}"
    $handlerMatch = [regex]::Match($content, $handlerMethodPattern)

    if ($handlerMatch.Success) {
        $handlerBody = $handlerMatch.Groups[1].Value.Trim()

        # Check for empty or trivial handlers
        if ([string]::IsNullOrWhiteSpace($handlerBody)) {
            $issues += @{
                Type = "Empty Event Handler"
                Name = $handlerName
                Details = "Event handler '$handlerName' is empty"
                Severity = "High"
            }
        }
        elseif ($handlerBody -match '^\s*//.*$' -or $handlerBody -match '^\s*/\*.*\*/\s*$') {
            $issues += @{
                Type = "Commented Event Handler"
                Name = $handlerName
                Details = "Event handler '$handlerName' only contains comments"
                Severity = "High"
            }
        }
        elseif ($handlerBody.Length -lt 50 -and $handlerBody -match '\bTODO\b|\bFIXME\b') {
            $warnings += @{
                Type = "Incomplete Event Handler"
                Name = $handlerName
                Details = "Event handler '$handlerName' contains TODO/FIXME"
                Severity = "Medium"
            }
        }
    }
}
#endregion

#region Mouse Event Analysis
Write-Host "🖱️  Analyzing mouse events..." -ForegroundColor Yellow

# Check for mouse event registrations without implementations
$mouseEvents = @('MouseMove', 'MouseEnter', 'MouseLeave', 'MouseHover', 'MouseDown', 'MouseUp')

foreach ($eventName in $mouseEvents) {
    if ($content -match "$eventName\s*\+=") {
        # Find the handler
        $handlerMatch = [regex]::Match($content, "$eventName\s*\+=\s*(\w+)")
        if ($handlerMatch.Success) {
            $handlerName = $handlerMatch.Groups[1].Value

            # Check if handler is trivial or missing
            $handlerPattern = "(?ms)private\s+(?:async\s+)?(?:void|Task)\s+$handlerName\s*\([^)]*\)\s*\{([^}]*)\}"
            $handlerBodyMatch = [regex]::Match($content, $handlerPattern)

            if ($handlerBodyMatch.Success) {
                $body = $handlerBodyMatch.Groups[1].Value.Trim()
                if ([string]::IsNullOrWhiteSpace($body) -or $body -match '^\s*//') {
                    $issues += @{
                        Type = "Dead Mouse Event"
                        Name = "$eventName → $handlerName"
                        Details = "Mouse event registered but handler is empty/commented"
                        Severity = "High"
                    }
                }
            }
        }
    }
}
#endregion

#region TODO/FIXME Analysis
Write-Host "📝 Analyzing TODO/FIXME comments..." -ForegroundColor Yellow

$lineNumber = 0
foreach ($line in $lines) {
    $lineNumber++

    if ($line -match '\b(TODO|FIXME|HACK|XXX)\b(.*)') {
        $keyword = $matches[1]
        $comment = $matches[2].Trim()

        $info += @{
            Type = "TODO Comment"
            Name = "Line $lineNumber"
            Details = "${keyword}: $comment"
            Severity = "Info"
        }
    }
}
#endregion

#region Commented Code Analysis
Write-Host "💬 Analyzing commented code blocks..." -ForegroundColor Yellow

# Find large blocks of commented code
$commentedCodePattern = '(?m)^\s*//.*\n(?:\s*//.*\n){4,}'
$commentedBlocks = [regex]::Matches($content, $commentedCodePattern)

foreach ($match in $commentedBlocks) {
    $lineCount = ($match.Value -split "`n").Length
    $preview = ($match.Value -split "`n" | Select-Object -First 2) -join "`n"

    $warnings += @{
        Type = "Commented Code Block"
        Name = "$lineCount lines"
        Details = "Large commented code block found:`n$preview..."
        Severity = "Low"
    }
}
#endregion

#region Grid/Data Loading Analysis
Write-Host "🗂️  Analyzing data loading..." -ForegroundColor Yellow

# Check if panel has a grid but no Load event
if ($content -match 'SfDataGrid|DataGridView') {
    $hasLoadEvent = $content -match 'Load\s*\+=\s*\w+' -or $content -match 'OnLoad\('

    if (-not $hasLoadEvent) {
        $warnings += @{
            Type = "Missing Load Event"
            Name = "Panel"
            Details = "Panel contains a grid but has no Load event handler"
            Severity = "High"
        }
    }

    # Check if LoadDataAsync or similar method exists
    $hasLoadMethod = $content -match 'LoadDataAsync|LoadAsync|RefreshDataAsync'
    if (-not $hasLoadMethod) {
        $warnings += @{
            Type = "Missing Load Method"
            Name = "Panel"
            Details = "Panel has a grid but no LoadDataAsync/LoadAsync method"
            Severity = "Medium"
        }
    }
}
#endregion

#region Initialize Component Analysis
Write-Host "⚙️  Analyzing InitializeComponent..." -ForegroundColor Yellow

if ($content -match '(?ms)private\s+void\s+InitializeComponent\s*\(\s*\)\s*\{([^}]+)\}') {
    $initBody = $matches[1]

    # Check for hardcoded colors (should use theme)
    if ($initBody -match '\bColor\.From') {
        $warnings += @{
            Type = "Hardcoded Colors"
            Name = "InitializeComponent"
            Details = "InitializeComponent contains hardcoded colors (should use SfSkinManager)"
            Severity = "Medium"
        }
    }

    # Check for missing theme application
    if ($initBody -notmatch 'ThemeName\s*=|SfSkinManager\.SetVisualStyle') {
        $warnings += @{
            Type = "Missing Theme"
            Name = "InitializeComponent"
            Details = "InitializeComponent doesn't set ThemeName or apply SfSkinManager theme"
            Severity = "Medium"
        }
    }
}
#endregion

#region Modern UI Design Guidelines Analysis (Fluent Design Inspired)
Write-Host "🎯 Analyzing modern UI design guidelines..." -ForegroundColor Yellow

# Check for InitializeComponent method with better regex that handles nested braces
if ($content -match '(?ms)private\s+void\s+InitializeComponent\s*\(\s*\)\s*\{(.+?)\n\s*\}(?=\s*(?:private|public|protected|internal|\}))') {
    $initBody = $matches[1]
    
    # 1. CHECK INPUT CONTROL HEIGHTS (should be 40px minimum)
    $inputControls = @('TextBoxExt', 'SfNumericTextBox', 'SfComboBox', 'DateTimePickerAdv', 'ComboBoxAdv', 'TextBox')
    foreach ($controlType in $inputControls) {
        $controlPattern = "(?ms)new\s+$controlType\s*\{[^\}]+\}"
        $controls = [regex]::Matches($initBody, $controlPattern)
        
        foreach ($controlMatch in $controls) {
            $controlDef = $controlMatch.Value
            
            # Skip if it's a multi-line textbox
            if ($controlDef -match 'Multiline\s*=\s*true') {
                continue
            }
            
            # Check for explicit Height property
            if ($controlDef -match "Height\s*=\s*(\d+)") {
                $height = [int]$matches[1]
                
                if ($height -lt 40 -and $height -gt 0) {
                    $warnings += @{
                        Type = "UI: Input Height Too Small"
                        Name = "$controlType ($height`px)"
                        Details = "Single-line input height is ${height}px (recommend 40px minimum for modern UX)"
                        Severity = "Medium"
                    }
                }
                
                # Check if height is a multiple of 4 or 8 (base unit system)
                if ($height -gt 0 -and $height % 8 -ne 0 -and $height % 4 -ne 0) {
                    $info += @{
                        Type = "UI: Non-Standard Height"
                        Name = "$controlType ($height`px)"
                        Details = "Height ${height}px is not a multiple of 4 or 8 (base unit system)"
                        Severity = "Info"
                    }
                }
            }
        }
    }
    
    # 2. CHECK MULTI-LINE TEXT BOX HEIGHTS (should be 80-120px)
    $multilinePattern = '(?ms)(?:new\s+(?:TextBoxExt|TextBox)\s*\{[^\}]*Multiline\s*=\s*true[^\}]*Height\s*=\s*(\d+)|Height\s*=\s*(\d+)[^\}]*Multiline\s*=\s*true)'
    $multilineMatches = [regex]::Matches($initBody, $multilinePattern)
    
    foreach ($match in $multilineMatches) {
        $height = if ($match.Groups[1].Success) { [int]$match.Groups[1].Value } else { [int]$match.Groups[2].Value }
        
        if ($height -lt 80) {
            $warnings += @{
                Type = "UI: Multi-line Too Short"
                Name = "TextBox ($height`px)"
                Details = "Multi-line textbox height is ${height}px (recommend 80-120px for comfortable editing)"
                Severity = "Medium"
            }
        }
        elseif ($height -gt 120) {
            $info += @{
                Type = "UI: Multi-line Very Tall"
                Name = "TextBox ($height`px)"
                Details = "Multi-line textbox height is ${height}px (typically 80-120px, but may be intentional)"
                Severity = "Info"
            }
        }
    }
    
    # 3. CHECK BUTTON HEIGHTS (should be 36-48px, ideally 38-40px)
    $buttonPattern = '(?ms)new\s+(?:SfButton|Button)\s*\{[^\}]+\}'
    $buttons = [regex]::Matches($initBody, $buttonPattern)
    
    foreach ($buttonMatch in $buttons) {
        $buttonDef = $buttonMatch.Value
        
        if ($buttonDef -match "Height\s*=\s*(\d+)") {
            $height = [int]$matches[1]
            
            if ($height -lt 36) {
                $warnings += @{
                    Type = "UI: Button Too Short"
                    Name = "Button ($height`px)"
                    Details = "Button height is ${height}px (recommend 36-48px, ideal 38-40px)"
                    Severity = "Medium"
                }
            }
            elseif ($height -gt 48) {
                $warnings += @{
                    Type = "UI: Button Too Tall"
                    Name = "Button ($height`px)"
                    Details = "Button height is ${height}px (recommend 36-48px, ideal 38-40px)"
                    Severity = "Low"
                }
            }
        }
    }
    
    # 4. CHECK VERTICAL SPACING (RowStyles: should use consistent multiples)
    $rowStylePattern = 'RowStyle\s*\(\s*SizeType\.\w+,\s*(\d+)(?:F|f)?\s*\)'
    $rowStyles = [regex]::Matches($initBody, $rowStylePattern)
    
    $rowHeights = @()
    foreach ($match in $rowStyles) {
        $height = [int]$match.Groups[1].Value
        if ($height -gt 0) {
            $rowHeights += $height
        }
    }
    
    # Check for inconsistent row heights
    if ($rowHeights.Count -gt 3) {
        $uniqueHeights = $rowHeights | Select-Object -Unique | Sort-Object
        
        if ($uniqueHeights.Count -gt 6) {
            $warnings += @{
                Type = "UI: Too Many Row Heights"
                Name = "$($uniqueHeights.Count) different heights"
                Details = "Too many different row heights ($($uniqueHeights -join ', ')px) - consider standardizing to 2-4 values"
                Severity = "Low"
            }
        }
    }
    
    # 5. CHECK LABEL WIDTHS (should be 160-180px)
    $columnStylePattern = 'ColumnStyle\s*\(\s*SizeType\.Absolute,\s*(\d+)(?:F|f)?\s*\)'
    $columnStyles = [regex]::Matches($initBody, $columnStylePattern)
    
    if ($columnStyles.Count -gt 0) {
        $firstColumnWidth = [int]$columnStyles[0].Groups[1].Value
        
        if ($firstColumnWidth -lt 140 -and $firstColumnWidth -gt 0) {
            $warnings += @{
                Type = "UI: Label Column Too Narrow"
                Name = "$firstColumnWidth`px"
                Details = "First column (label) width is ${firstColumnWidth}px (recommend 160-180px for modern forms)"
                Severity = "Medium"
            }
        }
        elseif ($firstColumnWidth -gt 200) {
            $info += @{
                Type = "UI: Label Column Very Wide"
                Name = "$firstColumnWidth`px"
                Details = "First column (label) width is ${firstColumnWidth}px (typically 160-180px, but may be intentional)"
                Severity = "Info"
            }
        }
    }
    
    # 6. CHECK PADDING CONSISTENCY (should use 8-12px typically)
    $paddingPattern = 'Padding\s*=\s*new\s+Padding\s*\(\s*(\d+)(?:\s*,\s*(\d+)(?:\s*,\s*(\d+)(?:\s*,\s*(\d+))?)?)?'
    $paddings = [regex]::Matches($initBody, $paddingPattern)
    
    $paddingValues = @()
    foreach ($match in $paddings) {
        $paddingValues += [int]$match.Groups[1].Value
        if ($match.Groups[2].Success) {
            $paddingValues += [int]$match.Groups[2].Value
        }
        if ($match.Groups[3].Success) {
            $paddingValues += [int]$match.Groups[3].Value
        }
        if ($match.Groups[4].Success) {
            $paddingValues += [int]$match.Groups[4].Value
        }
    }
    
    $unusualPaddings = $paddingValues | Where-Object { ($_ -lt 4 -or $_ -gt 16) -and $_ -gt 0 } | Select-Object -Unique
    
    if ($unusualPaddings.Count -gt 0) {
        $info += @{
            Type = "UI: Unusual Padding Values"
            Name = "$($unusualPaddings -join ', ')px"
            Details = "Some paddings outside typical 4-16px range: $($unusualPaddings -join ', ')px"
            Severity = "Info"
        }
    }
    
    # 7. CHECK MARGIN CONSISTENCY (should use multiples of 4 for spacing harmony)
    $marginPattern = 'Margin\s*=\s*new\s+Padding\s*\(\s*(\d+)(?:\s*,\s*(\d+)(?:\s*,\s*(\d+)(?:\s*,\s*(\d+))?)?)?'
    $margins = [regex]::Matches($initBody, $marginPattern)
    
    $marginValues = @()
    foreach ($match in $margins) {
        $marginValues += [int]$match.Groups[1].Value
        if ($match.Groups[2].Success) {
            $marginValues += [int]$match.Groups[2].Value
        }
        if ($match.Groups[3].Success) {
            $marginValues += [int]$match.Groups[3].Value
        }
        if ($match.Groups[4].Success) {
            $marginValues += [int]$match.Groups[4].Value
        }
    }
    
    $nonStandardMargins = $marginValues | Where-Object { $_ % 4 -ne 0 -and $_ -gt 0 } | Select-Object -Unique
    
    if ($nonStandardMargins.Count -gt 2) {
        $info += @{
            Type = "UI: Non-Standard Margins"
            Name = "$($nonStandardMargins -join ', ')px"
            Details = "Many margins are not multiples of 4: $($nonStandardMargins -join ', ')px (consider 4px system)"
            Severity = "Info"
        }
    }
    
    # 8. CHECK FONT SIZES (should be 11-12pt minimum for readability)
    $fontPattern = 'new\s+Font\s*\([^,]+,\s*(\d+)(?:\.(\d+))?(?:F|f)?'
    $fonts = [regex]::Matches($initBody, $fontPattern)
    
    foreach ($match in $fonts) {
        $fontSize = [int]$match.Groups[1].Value
        
        if ($fontSize -lt 11 -and $fontSize -gt 0) {
            $warnings += @{
                Type = "UI: Font Too Small"
                Name = "$fontSize`pt"
                Details = "Font size ${fontSize}pt is below recommended 11pt minimum (accessibility concern)"
                Severity = "Medium"
            }
        }
    }
    
    # 9. CHECK FOR HARDCODED SIZES (should prefer MinimumSize + Dock/Anchor for DPI scaling)
    $hardcodedSizePattern = '(?<!Minimum)(?<!Maximum)Size\s*=\s*new\s+Size\s*\(\s*\d+\s*,\s*\d+\s*\)'
    $hardcodedSizes = [regex]::Matches($initBody, $hardcodedSizePattern)
    
    if ($hardcodedSizes.Count -gt 5) {
        $info += @{
            Type = "UI: Many Hardcoded Sizes"
            Name = "$($hardcodedSizes.Count) controls"
            Details = "Consider using MinimumSize + Dock/Anchor/Fill for better DPI-aware scaling"
            Severity = "Info"
        }
    }
    
    # 10. CHECK FOR DPI AWARENESS
    $hasDpiAwareness = $initBody -match 'AutoScaleMode\s*=\s*AutoScaleMode\.Dpi' -or 
                       $content -match 'this\.AutoScaleMode\s*=\s*AutoScaleMode\.Dpi'
    
    if (-not $hasDpiAwareness) {
        $warnings += @{
            Type = "UI: Missing DPI Awareness"
            Name = "Panel"
            Details = "Panel should set AutoScaleMode = AutoScaleMode.Dpi for high-DPI display support"
            Severity = "Medium"
        }
    }
    
    # 11. CHECK FOR CONSISTENT CONTROL NAMING
    $controlNamingPattern = 'Name\s*=\s*"([^"]+)"'
    $controlNames = [regex]::Matches($initBody, $controlNamingPattern)
    
    $inconsistentNaming = @()
    foreach ($match in $controlNames) {
        $name = $match.Groups[1].Value
        
        # Check if name follows convention (prefixed: txt, cmb, btn, lbl, etc.)
        if ($name -notmatch '^(txt|cmb|btn|lbl|chk|num|dt|sf|pnl|grp)') {
            $inconsistentNaming += $name
        }
    }
    
    if ($inconsistentNaming.Count -gt 3) {
        $info += @{
            Type = "UI: Inconsistent Control Naming"
            Name = "$($inconsistentNaming.Count) controls"
            Details = "Some controls don't follow naming convention (txt, btn, cmb, lbl prefixes)"
            Severity = "Info"
        }
    }
}
else {
    $info += @{
        Type = "UI: InitializeComponent Not Found"
        Name = "Panel"
        Details = "Could not parse InitializeComponent method - UI design guidelines not fully checked"
        Severity = "Info"
    }
}
#endregion

#region Results Display
Write-Host "`n" -NoNewline

# Display Critical Issues
if ($issues.Count -gt 0) {
    Write-Host "╔══════════════════════════════════════════════════╗" -ForegroundColor Red
    Write-Host "║  🔴 CRITICAL ISSUES FOUND: $($issues.Count.ToString().PadLeft(2))                      ║" -ForegroundColor Red
    Write-Host "╚══════════════════════════════════════════════════╝`n" -ForegroundColor Red

    foreach ($issue in $issues) {
        Write-Host "  ❌ $($issue.Type): " -ForegroundColor Red -NoNewline
        Write-Host $issue.Name -ForegroundColor White
        Write-Host "     $($issue.Details)" -ForegroundColor Gray
        Write-Host ""
    }
}

# Display Warnings
if ($warnings.Count -gt 0) {
    Write-Host "╔══════════════════════════════════════════════════╗" -ForegroundColor Yellow
    Write-Host "║  ⚠️  WARNINGS: $($warnings.Count.ToString().PadLeft(2))                                ║" -ForegroundColor Yellow
    Write-Host "╚══════════════════════════════════════════════════╝`n" -ForegroundColor Yellow

    foreach ($warning in $warnings) {
        Write-Host "  ⚠️  $($warning.Type): " -ForegroundColor Yellow -NoNewline
        Write-Host $warning.Name -ForegroundColor White
        Write-Host "     $($warning.Details)" -ForegroundColor Gray
        Write-Host ""
    }
}

# Display Info
if ($info.Count -gt 0) {
    Write-Host "╔══════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║  ℹ️  INFORMATIONAL: $($info.Count.ToString().PadLeft(2))                          ║" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

    # Limit to first 10 info items
    $displayInfo = $info | Select-Object -First 10
    foreach ($item in $displayInfo) {
        Write-Host "  ℹ️  $($item.Type): " -ForegroundColor Cyan -NoNewline
        Write-Host $item.Name -ForegroundColor White
        Write-Host "     $($item.Details)" -ForegroundColor Gray
        Write-Host ""
    }

    if ($info.Count -gt 10) {
        Write-Host "  ... and $($info.Count - 10) more TODO/FIXME comments`n" -ForegroundColor Gray
    }
}

# Summary
Write-Host "`n═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "📊 SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Critical Issues: " -NoNewline
Write-Host $issues.Count -ForegroundColor $(if ($issues.Count -gt 0) { 'Red' } else { 'Green' })
Write-Host "  Warnings:        " -NoNewline
Write-Host $warnings.Count -ForegroundColor $(if ($warnings.Count -gt 0) { 'Yellow' } else { 'Green' })
Write-Host "  TODO/FIXME:      " -NoNewline
Write-Host $info.Count -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════`n" -ForegroundColor Cyan

if ($issues.Count -eq 0 -and $warnings.Count -eq 0) {
    Write-Host "✅ No significant issues found! Panel looks clean.`n" -ForegroundColor Green
}
else {
    Write-Host "💡 TIP: Address critical issues first, then tackle warnings.`n" -ForegroundColor Gray
}

#endregion

# Exit code based on severity
if ($issues.Count -gt 0) {
    exit 1
}
exit 0
