<#
.SYNOPSIS
    Audits WinForms panels against Done_Checklist.md and generates a per-panel remediation report.

.DESCRIPTION
    Performs static checklist validation for files in Controls/Panels and optionally Forms/RatesPage.cs.
    Outputs a markdown and JSON report with:
    - readiness (Green/Yellow/Red/N-A)
    - failed checklist sections
    - code-level remediation hints
    - runtime-only certification reminders

.PARAMETER PanelsPath
    Path to Controls/Panels directory.

.PARAMETER ChecklistPath
    Path to Done_Checklist.md.

.PARAMETER IncludeRatesPage
    Includes Forms/RatesPage.cs as an informational N/A row.

.PARAMETER OutputPath
    Output folder for generated reports.

.EXAMPLE
    ./scripts/validation/Validate-PanelDoneChecklist.ps1

.NOTES
    Requires: PowerShell 7.5.4+
#>

#Requires -Version 7.5.4

# NOTE: PanelHeader direct construction is ACCEPTED (ReportsPanel precedent, Batch 2 unblock)

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateScript({ Test-Path $_ -PathType Container })]
    [string]$PanelsPath = "$PSScriptRoot/../../src/WileyWidget.WinForms/Controls/Panels",

    [Parameter()]
    [ValidateScript({ Test-Path $_ -PathType Leaf })]
    [string]$ChecklistPath = "$PSScriptRoot/../../Done_Checklist.md",

    [Parameter()]
    [switch]$IncludeRatesPage,

    [Parameter()]
    [switch]$StrictFactoryOnly,

    [Parameter()]
    [string]$OutputPath = "$PSScriptRoot/../../tmp/validation-reports"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-Result {
    param(
        [string]$File,
        [string]$Role,
        [string]$Readiness,
        [string[]]$FailedSections,
        [string[]]$FailedSectionDetails,
        [string]$Summary,
        [string[]]$Actions,
        [string[]]$RuntimeChecks
    )

    [PSCustomObject]@{
        File                 = $File
        Role                 = $Role
        Readiness            = $Readiness
        FailedSections       = $FailedSections
        FailedSectionDetails = $FailedSectionDetails
        Summary              = $Summary
        Actions              = $Actions
        RuntimeChecks        = $RuntimeChecks
    }
}

function Test-NAFile {
    param([string]$FileName)

    return ($FileName -like '*.Designer.cs' -or
        $FileName -like '*.Layout.cs' -or
        $FileName -like '*TabControl.cs' -or
        $FileName -eq 'KpiCardControl.cs')
}

function Test-Regex {
    param([string]$Content, [string]$Pattern)
    return [regex]::IsMatch($Content, $Pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
}

function Get-ItemCount {
    param([object]$Value)

    if ($null -eq $Value) { return 0 }
    if ($Value -is [string]) {
        if ([string]::IsNullOrWhiteSpace($Value)) {
            return 0
        }

        return 1
    }
    if ($Value -is [System.Collections.ICollection]) { return $Value.Count }
    return @($Value).Count
}

function Get-PanelRole {
    param([string]$Content)

    if (Test-Regex $Content 'class\s+\w+\s*:\s*ScopedPanelBase') { return 'Scoped panel' }
    if (Test-Regex $Content 'class\s+\w+\s*:\s*UserControl') { return 'UserControl panel/helper' }
    if (Test-Regex $Content 'class\s+\w+\s*:\s*SfForm|class\s+\w+\s*:\s*Form') { return 'Form' }
    return 'Unknown'
}

function Get-FailedSectionDetails {
    param([string[]]$Sections)

    if ((Get-ItemCount -Value $Sections) -eq 0) {
        return @()
    }

    $sectionMap = @{
        '1' = 'Sec 1: Construction & skeleton'
        '2' = 'Sec 2: Load/grid data contract'
        '3' = 'Sec 3: CRUD/refresh behavior'
        '5' = 'Sec 5: Theme/style compliance'
        '6' = 'Sec 6: Lifecycle/ICompletable contract'
        '7' = 'Sec 7: Syncfusion theme cascade compliance'
        '8' = 'Sec 8: Accessibility/tooltips'
        '9' = 'Sec 9: Host-control exception path'
    }

    $details = [System.Collections.Generic.List[string]]::new()
    foreach ($section in @($Sections | Sort-Object)) {
        if ($sectionMap.ContainsKey($section)) {
            $details.Add($sectionMap[$section])
        } else {
            $details.Add("Sec ${section}: Unmapped")
        }
    }

    return @($details)
}

function Test-PanelChecklist {
    param(
        [System.IO.FileInfo]$File,
        [string]$ChecklistText
    )

    $content = Get-Content -Path $File.FullName -Raw
    $fileName = $File.Name

    if (Test-NAFile -FileName $fileName) {
        return New-Result -File $File.FullName -Role 'Support artifact' -Readiness 'N/A' -FailedSections @() -FailedSectionDetails @() -Summary 'Designer/partial/helper artifact outside standalone panel gate.' -Actions @() -RuntimeChecks @()
    }

    $role = Get-PanelRole -Content $content
    $fails = [System.Collections.Generic.HashSet[string]]::new()
    $actions = [System.Collections.Generic.List[string]]::new()

    # Section 9: host-control exception path
    $isHostControl = Test-Regex $content '(BlazorWebView|WebView2|IAsyncInitializable|JARVISChatUserControl)'
    $hostHasSurface = Test-Regex $content '(BlazorWebView|WebView2)'
    $hostHasAsyncInit = Test-Regex $content '(IAsyncInitializable|InitializeAsync\s*\()'

    if ($isHostControl) {
        if (-not $hostHasSurface) {
            [void]$fails.Add('9')
            $actions.Add('Host-control panel should expose a WebView host surface (BlazorWebView/WebView2).')
        }
        if (-not $hostHasAsyncInit) {
            [void]$fails.Add('9')
            $actions.Add('Host-control panel should implement async host lifecycle (IAsyncInitializable/InitializeAsync).')
        }
    }

    # Section 1 checks (non-host path)
    if (-not $isHostControl) {
        $inheritsScoped = Test-Regex $content ':\s*ScopedPanelBase(?:<[^>]+>)?'
        $ctorVmFactory = Test-Regex $content '(?:\[ActivatorUtilitiesConstructor\][\s\r\n]*)?public\s+\w+\s*\([^)]*\b\w+ViewModel\s+\w+[^)]*\bSyncfusionControlFactory\s+\w+[^)]*\)'
        $safeSuspend = Test-Regex $content 'SafeSuspendAndLayout\s*\(\s*[A-Za-z_][A-Za-z0-9_]*\s*\)'
        $headerViaFactory = Test-Regex $content '(?:_factory|factory|Factory|ControlFactory)\.(?:CreatePanelHeader|Create\s*PanelHeader)\s*\('
        $headerDirect = Test-Regex $content 'new\s+PanelHeader\b'
        $headerPresent = if ($StrictFactoryOnly) { $headerViaFactory } else { $headerViaFactory -or $headerDirect }
        $rootContent = ((Test-Regex $content '_content\s*=\s*new\s+TableLayoutPanel') -or (Test-Regex $content '_content\s*=\s*[A-Za-z_][A-Za-z0-9_]*\s*;')) -and (Test-Regex $content 'TableLayoutPanel') -and (Test-Regex $content 'Dock\s*=\s*DockStyle\.Fill')
        $loaderFactory = (Test-Regex $content '(?:_factory|factory|Factory|ControlFactory)\.CreateLoadingOverlay\s*\(') -or (Test-Regex $content '_(?:loader|loadingOverlay)\s*=\s*new\s+LoadingOverlay\b')
        $autoScaleDpi = Test-Regex $content 'AutoScaleMode\s*=\s*AutoScaleMode\.Dpi'
        $minimumSize = (Test-Regex $content 'MinimumSize\s*=\s*(?:RecommendedDockedPanelMinimumLogicalSize|new\s+Size\s*\(\s*1024\s*,\s*720\s*\)|new\s+Size\s*\(\s*Math\.Max\(MinimumSize\.Width\s*,\s*[^\)]*1024f?[^\)]*\)\s*,\s*Math\.Max\(MinimumSize\.Height\s*,\s*[^\)]*720f?[^\)]*\)\s*\))') -or ((Test-Regex $content 'LogicalToDeviceUnits\s*\(\s*1024f?\s*\)') -and (Test-Regex $content 'LogicalToDeviceUnits\s*\(\s*720f?\s*\)') -and (Test-Regex $content 'MinimumSize\s*=\s*newMin'))
        $onHandleCreated = Test-Regex $content 'override\s+void\s+OnHandleCreated\s*\('

        if (-not $inheritsScoped) { [void]$fails.Add('1'); $actions.Add('Inherit ScopedPanelBase<TViewModel> for panel lifecycle compliance.') }
        if (-not $ctorVmFactory) { [void]$fails.Add('1'); $actions.Add('Add canonical constructor overload with (ViewModel vm, SyncfusionControlFactory factory).') }
        if (-not $safeSuspend) { [void]$fails.Add('1'); $actions.Add('Use SafeSuspendAndLayout(...) from constructor.') }
        if (-not $headerPresent) {
            [void]$fails.Add('1')
            if ($StrictFactoryOnly) {
                $actions.Add('Create PanelHeader via factory path and wire Refresh/Close handlers.')
            } else {
                $actions.Add('Create PanelHeader shared host control (direct or factory path) and wire Refresh/Close handlers.')
            }
        }
        if (-not $rootContent) { [void]$fails.Add('1'); $actions.Add('Define root _content as Dock=Fill TableLayoutPanel.') }
        if (-not $loaderFactory) { [void]$fails.Add('1'); $actions.Add('Create loading overlay via factory (preferred) or canonical LoadingOverlay field, Dock=Fill, Visible=false.') }
        if (-not $minimumSize) { [void]$fails.Add('1'); $actions.Add('Set minimum size to recommended docked baseline (1024x720 logical).') }
        if (-not $autoScaleDpi -and -not (Test-Regex $content 'LogicalToDeviceUnits\s*\(\s*1024f?\s*\)')) { [void]$fails.Add('1'); $actions.Add('Use AutoScaleMode.Dpi (or equivalent DPI-aware logical sizing path) for panel layout consistency.') }
        if (-not $onHandleCreated) { [void]$fails.Add('1'); $actions.Add('Override OnHandleCreated to enforce minimum size/layout after handle creation.') }
    }

    # Section 5/7 strict style/theme checks
    if (-not $isHostControl) {
        $manualThemeName = (Test-Regex $content '(?<!\bthis\.)\b[A-Za-z_][A-Za-z0-9_]*\s*\.\s*ThemeName\s*=') -or (Test-Regex $content 'new\s+Sf[A-Za-z0-9_]+\s*\{[\s\S]*?\bThemeName\s*=')
        $manualSetVisualStyle = Test-Regex $content 'SfSkinManager\.SetVisualStyle\s*\(\s*(?!this\b)'
        $manualBackColor = Test-Regex $content '\bBackColor\s*='
        $manualFont = Test-Regex $content '\bFont\s*='
        $manualForeColorMatches = [regex]::Matches($content, '(?im)^\s*[^\r\n;]*\bForeColor\s*=\s*[^\r\n;]+')
        $hasDisallowedForeColor = $false
        foreach ($match in $manualForeColorMatches) {
            $line = $match.Value
            $isSemantic = [regex]::IsMatch($line, 'Color\.(Red|Green|Orange)|SystemColors\.ControlText', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            if (-not $isSemantic) {
                $hasDisallowedForeColor = $true
                break
            }
        }

        if ($manualThemeName -or $manualSetVisualStyle) {
            [void]$fails.Add('5')
            [void]$fails.Add('7')
            $actions.Add('Remove per-control ThemeName/SetVisualStyle; rely on global SfSkinManager cascade.')
        }
        if ($manualBackColor -or $hasDisallowedForeColor -or $manualFont) {
            [void]$fails.Add('5')
            $actions.Add('Remove manual BackColor/ForeColor/Font assignments (semantic status colors only).')
        }
    }

    # Section 1 factory-only Syncfusion creation (heuristic)
    if (-not $isHostControl) {
        $directSyncfusionNews = [regex]::Matches($content, 'new\s+(Sf[A-Za-z0-9_]+|ChartControl|DockingManager|RibbonControlAdv|ProgressBarAdv|SplitContainerAdv|CheckBoxAdv|TextBoxExt|SfGauge|SfButtonAdv|SfComboBox|SfNumericTextBox)\b').Count
        if ($directSyncfusionNews -gt 0) {
            [void]$fails.Add('1')
            $actions.Add("Replace direct Syncfusion instantiation with factory methods (found $directSyncfusionNews direct new calls).")
        }
    }

    # Section 2/3/6/8 heuristics
    $hasLoadAsync = Test-Regex $content 'override\s+(?:async\s+)?Task\s+LoadAsync\s*\('
    $hasGrid = Test-Regex $content 'SfDataGrid'
    $hasVirtualization = Test-Regex $content 'EnableDataVirtualization\s*=\s*true'
    $hasAllCellsAutosize = Test-Regex $content 'AutoSizeColumnsMode\s*=\s*AutoSizeColumnsMode\.AllCells'
    $hasGridBinding = Test-Regex $content '(?:DataSource|ItemsSource)\s*='
    $hasCrudOps = Test-Regex $content 'AddEntry|EditEntry|UpdateEntry|DeleteEntry|BulkAdjust|CopyToNextYear'
    $hasApplyFilters = Test-Regex $content 'ApplyFiltersAsync|ApplyFiltersCommand'
    $hasRefreshCall = Test-Regex $content '\.Refresh\s*\('
    $hasValidation = Test-Regex $content 'ValidateAsync\s*\('
    $hasSave = Test-Regex $content 'SaveAsync\s*\('
    $hasFocusFirstError = Test-Regex $content 'FocusFirstError\s*\('
    $hasDispose = Test-Regex $content 'override\s+void\s+Dispose\s*\('
    $hasTooltips = Test-Regex $content 'ToolTip|SetToolTip\s*\('

    $requiresCrudPerformanceChecks = $hasCrudOps -or (Test-Regex $content '\b(Create|Update|Delete|Save)\b.*\b(Bill|Account|Payment|Entry)\b')
    $requiresCompletableContract = $hasCrudOps -or (Test-Regex $content 'ICompletablePanel')

    if (-not $isHostControl) {
        if ($hasGrid -and $requiresCrudPerformanceChecks -and (-not $hasVirtualization -or -not $hasGridBinding)) {
            [void]$fails.Add('2')
            $actions.Add('Bind grid to full filtered collection and enable data virtualization.')
        }
        if ($hasGrid -and $requiresCrudPerformanceChecks -and -not $hasAllCellsAutosize) {
            [void]$fails.Add('2')
            $actions.Add('Set AutoSizeColumnsMode=AllCells and enforce sensible MinimumWidth values.')
        }
        if ($hasCrudOps -and (-not $hasApplyFilters -or -not $hasRefreshCall)) {
            [void]$fails.Add('3')
            $actions.Add('After Add/Edit/Delete/Bulk, call ApplyFiltersAsync and force grid Refresh with collection notify.')
        }
        if ($requiresCrudPerformanceChecks -and -not $hasLoadAsync) {
            [void]$fails.Add('2')
            $actions.Add('Implement LoadAsync(CancellationToken) with loader visibility handling.')
        }
        if ($requiresCompletableContract -and (-not $hasValidation -or -not $hasSave -or -not $hasFocusFirstError -or -not $hasDispose)) {
            [void]$fails.Add('6')
            $actions.Add('Complete lifecycle contract: ValidateAsync, SaveAsync, FocusFirstError, and robust Dispose cleanup.')
        }
        if (-not $hasTooltips) {
            [void]$fails.Add('8')
            $actions.Add('Add plain-language tooltips for all interactive controls.')
        }
    }

    $runtimeChecks = @(
        'Measure first-paint to populated-grid <= 800ms',
        'Measure Add/Edit/Delete visible refresh <= 150ms',
        'Measure theme switch while open <= 300ms',
        'Verify keyboard shortcuts and close-during-load cancellation behavior'
    )

    $readiness = if ($isHostControl) {
        if ($fails.Count -eq 0) { 'EXCEPTION-PASS (Host-Control)' } else { 'EXCEPTION-TRACK (Host-Control)' }
    } elseif ($fails.Contains('1')) { 'Red' }
    elseif ($fails.Count -gt 0) { 'Yellow' }
    else { 'Green' }
    $summary = switch ($readiness) {
        'Green' { 'Static checklist checks pass; runtime SLA certification still required.' }
        'Yellow' { 'Partially compliant; targeted refactors required outside mandatory skeleton blockers.' }
        'Red' { 'Fails mandatory skeleton/style requirements; structural refactor required.' }
        'EXCEPTION-PASS (Host-Control)' { 'Host-control exception path passes static checks; runtime host certification still required.' }
        'EXCEPTION-TRACK (Host-Control)' { 'Host-control exception path identified; targeted exception remediation required.' }
    }

    $sortedFails = @($fails) | Sort-Object
    return New-Result -File $File.FullName -Role $role -Readiness $readiness -FailedSections $sortedFails -FailedSectionDetails (Get-FailedSectionDetails -Sections $sortedFails) -Summary $summary -Actions ($actions | Select-Object -Unique) -RuntimeChecks $runtimeChecks
}

$checklistText = Get-Content -Path $ChecklistPath -Raw
$panelFiles = Get-ChildItem -Path $PanelsPath -Filter '*.cs' -File | Sort-Object Name

$results = [System.Collections.Generic.List[object]]::new()
foreach ($file in $panelFiles) {
    $results.Add((Test-PanelChecklist -File $file -ChecklistText $checklistText))
}

if ($IncludeRatesPage) {
    $rootPath = (Resolve-Path "$PSScriptRoot/../..").Path
    $ratesPath = Join-Path -Path $rootPath -ChildPath 'src/WileyWidget.WinForms/Forms/RatesPage.cs'
    if (Test-Path -Path $ratesPath -PathType Leaf) {
        $results.Add((New-Result -File (Resolve-Path $ratesPath).Path -Role 'Form (non-panel)' -Readiness 'N/A' -FailedSections @() -FailedSectionDetails @() -Summary 'RatesPage is a form and outside panel DoD skeleton gate.' -Actions @('Create separate Form DoD checklist, or host in FormHostPanel if panel gating is required.') -RuntimeChecks @()))
    }
}

if (-not (Test-Path -Path $OutputPath)) {
    New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$jsonOut = Join-Path -Path $OutputPath -ChildPath "panel-done-audit-$timestamp.json"
$mdOut = Join-Path -Path $OutputPath -ChildPath "panel-done-audit-$timestamp.md"

$payload = [PSCustomObject]@{
    GeneratedAt   = (Get-Date).ToString('o')
    ChecklistPath = (Resolve-Path $ChecklistPath).Path
    PanelsPath    = (Resolve-Path $PanelsPath).Path
    ResultCount   = $results.Count
    Results       = $results
}

$payload | ConvertTo-Json -Depth 8 | Set-Content -Path $jsonOut -Encoding UTF8

$rootPath = (Resolve-Path "$PSScriptRoot/../..").Path
$md = [System.Text.StringBuilder]::new()
[void]$md.AppendLine('# Panel Done Checklist Audit')
[void]$md.AppendLine()
[void]$md.AppendLine("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$md.AppendLine()
[void]$md.AppendLine('| File | Role | Readiness | Failed Sections | Failed Section Detail | Summary |')
[void]$md.AppendLine('|---|---|---|---|---|---|')

foreach ($row in $results) {
    $relPath = $row.File.Replace($rootPath + [IO.Path]::DirectorySeparatorChar, '')
    $failedCount = Get-ItemCount -Value $row.FailedSections
    $failed = if ($failedCount -eq 0) { '-' } else { (@($row.FailedSections) -join ', ') }
    $failedDetailsCount = Get-ItemCount -Value $row.FailedSectionDetails
    $failedDetails = if ($failedDetailsCount -eq 0) { '-' } else { (@($row.FailedSectionDetails) -join '; ') }
    [void]$md.AppendLine("| $relPath | $($row.Role) | $($row.Readiness) | $failed | $failedDetails | $($row.Summary) |")
}

[void]$md.AppendLine()
[void]$md.AppendLine('## Per-file Remediation Actions')
[void]$md.AppendLine()
foreach ($row in $results | Where-Object { $_.Readiness -notin @('Green', 'N/A', 'EXCEPTION-PASS (Host-Control)') }) {
    $relPath = $row.File.Replace($rootPath + [IO.Path]::DirectorySeparatorChar, '')
    [void]$md.AppendLine("### $relPath")
    foreach ($action in @($row.Actions)) {
        [void]$md.AppendLine("- $action")
    }
    [void]$md.AppendLine('- Runtime certification:')
    foreach ($runtime in @($row.RuntimeChecks)) {
        [void]$md.AppendLine("  - $runtime")
    }
    [void]$md.AppendLine()
}

$md.ToString() | Set-Content -Path $mdOut -Encoding UTF8

Write-Host "Checklist audit written:" -ForegroundColor Cyan
Write-Host "  Markdown: $mdOut" -ForegroundColor White
Write-Host "  JSON:     $jsonOut" -ForegroundColor White

$results | Sort-Object Readiness, File | Format-Table -AutoSize File, Role, Readiness, @{Name = 'Failed'; Expression = { if ((Get-ItemCount -Value $_.FailedSections) -eq 0) { '-' } else { (@($_.FailedSections) -join ',') } } }
