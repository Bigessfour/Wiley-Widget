<#
.SYNOPSIS
    Validates that all ViewModels are completely and fully implemented.

.DESCRIPTION
    Performs comprehensive static analysis on all ViewModel classes to ensure:
    - Proper inheritance from ObservableObject/ObservableRecipient
    - Correct use of [ObservableProperty] and [RelayCommand] attributes
    - ILogger<T> injection and usage
    - Service dependency injection
    - IDisposable implementation where needed
    - XML documentation on public members
    - Pure MVVM (no direct UI references)
    - Thread-safety for async operations

.PARAMETER Path
    Root path to scan for ViewModels. Defaults to workspace root.

.PARAMETER FailOnViolations
    If set, exits with non-zero code when violations are found (for CI/CD).

.PARAMETER GenerateReport
    If set, generates a JSON report of all findings.

.PARAMETER OutputPath
    Path for the JSON report. Defaults to 'viewmodel-validation-report.json'.

.EXAMPLE
    .\validate-viewmodels.ps1
    # Run validation and display results

.EXAMPLE
    .\validate-viewmodels.ps1 -FailOnViolations -GenerateReport
    # Run in CI mode with report generation

.EXAMPLE
    .\validate-viewmodels.ps1 -Path "src/WileyWidget.WinForms/ViewModels" -Verbose
    # Validate specific directory with verbose output
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Path = "$PSScriptRoot\..\..",

    [Parameter()]
    [switch]$FailOnViolations,

    [Parameter()]
    [switch]$GenerateReport,

    [Parameter()]
    [string]$OutputPath = "viewmodel-validation-report.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

#region Helper Functions

function Write-ValidationHeader {
    Write-Host "`n╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║           ViewModel Validation Framework                  ║" -ForegroundColor Cyan
    Write-Host "║           Comprehensive Implementation Checker            ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan
}

function Test-ViewModelInheritance {
    param([string]$Content, [string]$FileName)

    $violations = @()

    # Check for proper base class
    if ($Content -notmatch '\bclass\s+\w+ViewModel\s*:\s*(ObservableObject|ObservableRecipient)') {
        $violations += @{
            Severity = 'Error'
            Rule = 'VM001'
            Message = "ViewModel must inherit from ObservableObject or ObservableRecipient"
            File = $FileName
        }
    }

    return $violations
}

function Test-PropertyImplementation {
    param([string]$Content, [string]$FileName)

    $violations = @()

    # Find all properties
    $propertyPattern = '(?m)^\s*public\s+(?!class|interface)\w+(?:<[^>]+>)?\s+(\w+)\s*\{'
    $properties = [regex]::Matches($Content, $propertyPattern)

    foreach ($prop in $properties) {
        $propertyName = $prop.Groups[1].Value
        $lineNumber = ($Content.Substring(0, $prop.Index) -split "`n").Count

        # Check if property has [ObservableProperty] or proper backing field
        $contextStart = [Math]::Max(0, $prop.Index - 200)
        $context = $Content.Substring($contextStart, 200)

        $hasObservableAttribute = $context -match '\[ObservableProperty\]'
        $hasBackingField = $Content -match "private\s+\w+\s+_$($propertyName.ToLower())"
        $hasPropertyChangedCall = $prop.Value -match 'OnPropertyChanged|SetProperty'

        # Skip command properties and collections
        $isCommand = $propertyName -match 'Command$'
        $isCollection = $prop.Value -match 'ObservableCollection|ICollection'

        if (-not $hasObservableAttribute -and -not $hasBackingField -and -not $hasPropertyChangedCall -and -not $isCommand -and -not $isCollection) {
            $violations += @{
                Severity = 'Warning'
                Rule = 'VM002'
                Message = "Property '$propertyName' may not raise PropertyChanged event"
                File = $FileName
                Line = $lineNumber
            }
        }
    }

    return $violations
}

function Test-CommandImplementation {
    param([string]$Content, [string]$FileName)

    $violations = @()

    # Find command properties
    $commandPattern = '(?m)^\s*public\s+(IAsyncRelayCommand|IRelayCommand|AsyncRelayCommand|RelayCommand)(<[^>]+>)?\s+(\w+)\s*\{'
    $commands = [regex]::Matches($Content, $commandPattern)

    foreach ($cmd in $commands) {
        $commandName = $cmd.Groups[3].Value
        $lineNumber = ($Content.Substring(0, $cmd.Index) -split "`n").Count

        # Check if command has [RelayCommand] attribute or proper initialization
        $contextStart = [Math]::Max(0, $cmd.Index - 200)
        $context = $Content.Substring($contextStart, 200)

        $hasRelayAttribute = $context -match '\[RelayCommand\]'
        $hasManualInit = $Content -match "new (Async)?RelayCommand\("

        if (-not $hasRelayAttribute -and -not $hasManualInit) {
            $violations += @{
                Severity = 'Warning'
                Rule = 'VM003'
                Message = "Command '$commandName' may not be properly initialized"
                File = $FileName
                Line = $lineNumber
            }
        }
    }

    return $violations
}

function Test-DependencyInjection {
    param([string]$Content, [string]$FileName)

    $violations = @()

    # Check for ILogger injection
    if ($Content -notmatch 'ILogger<\w+ViewModel>') {
        $violations += @{
            Severity = 'Warning'
            Rule = 'VM004'
            Message = "ViewModel should inject ILogger<T> for diagnostics"
            File = $FileName
        }
    }

    # Check for null checks in constructor
    $constructorPattern = '(?ms)public\s+\w+ViewModel\s*\([^)]+\)\s*\{[^}]*\}'
    if ($Content -match $constructorPattern) {
        $constructor = $Matches[0]

        # Check if constructor parameters are null-checked
        $paramPattern = 'ILogger<\w+>|I\w+Service'
        $params = [regex]::Matches($Content, $paramPattern)

        foreach ($param in $params) {
            $paramName = $param.Value -replace '^I', '' -replace '<.*>$', ''
            if ($constructor -notmatch "throw new ArgumentNullException.*$paramName") {
                $violations += @{
                    Severity = 'Info'
                    Rule = 'VM005'
                    Message = "Consider adding null check for '$($param.Value)' in constructor"
                    File = $FileName
                }
            }
        }
    }

    return $violations
}

function Test-DisposablePattern {
    param([string]$Content, [string]$FileName)

    $violations = @()

    # Check if ViewModel uses async operations or subscribes to events
    $needsDisposable = $Content -match '(CancellationTokenSource|\.Subscribe\(|EventHandler|Timer)' -and
                       $Content -match 'private.*CancellationTokenSource|private.*IDisposable'

    if ($needsDisposable -and $Content -notmatch ':\s*\w+,\s*IDisposable') {
        $violations += @{
            Severity = 'Warning'
            Rule = 'VM006'
            Message = "ViewModel manages disposable resources but doesn't implement IDisposable"
            File = $FileName
        }
    }

    return $violations
}

function Test-XmlDocumentation {
    param([string]$Content, [string]$FileName)

    $violations = @()

    # Check for class-level XML doc
    if ($Content -notmatch '(?ms)///\s*<summary>.*?</summary>.*?public\s+(partial\s+)?class\s+\w+ViewModel') {
        $violations += @{
            Severity = 'Info'
            Rule = 'VM007'
            Message = "ViewModel class should have XML documentation"
            File = $FileName
        }
    }

    # Count public members without XML docs
    $publicMembersPattern = '(?m)^\s*public\s+(?!class|interface)'
    $publicMembers = [regex]::Matches($Content, $publicMembersPattern)

    $undocumentedCount = 0
    foreach ($member in $publicMembers) {
        $contextStart = [Math]::Max(0, $member.Index - 150)
        $context = $Content.Substring($contextStart, 150)

        if ($context -notmatch '///\s*<summary>') {
            $undocumentedCount++
        }
    }

    if ($undocumentedCount -gt 0) {
        $violations += @{
            Severity = 'Info'
            Rule = 'VM008'
            Message = "$undocumentedCount public members lack XML documentation"
            File = $FileName
        }
    }

    return $violations
}

function Test-MvvmPurity {
    param([string]$Content, [string]$FileName)

    $violations = @()

    # Check for direct UI references (anti-patterns)
    $uiPatterns = @(
        @{ Pattern = '\bForm\b'; Message = 'Direct Form reference' }
        @{ Pattern = '\bControl\b(?!\.ControlCollection)'; Message = 'Direct Control reference' }
        @{ Pattern = '\bButton\b'; Message = 'Direct Button reference' }
        @{ Pattern = '\bTextBox\b'; Message = 'Direct TextBox reference' }
        @{ Pattern = '\.Show\(|\.Hide\(|\.Close\('; Message = 'Direct window manipulation' }
        @{ Pattern = 'MessageBox\.Show'; Message = 'Direct MessageBox usage (use dialog service)' }
    )

    foreach ($pattern in $uiPatterns) {
        if ($Content -match $pattern.Pattern) {
            # Exclude using statements and comments
            $matches = [regex]::Matches($Content, $pattern.Pattern)
            foreach ($match in $matches) {
                $lineStart = $Content.LastIndexOf("`n", $match.Index) + 1
                $lineEnd = $Content.IndexOf("`n", $match.Index)
                if ($lineEnd -eq -1) { $lineEnd = $Content.Length }
                $line = $Content.Substring($lineStart, $lineEnd - $lineStart)

                if ($line -notmatch '^\s*(using |//)') {
                    $lineNumber = ($Content.Substring(0, $match.Index) -split "`n").Count
                    $violations += @{
                        Severity = 'Error'
                        Rule = 'VM009'
                        Message = "MVVM violation: $($pattern.Message)"
                        File = $FileName
                        Line = $lineNumber
                    }
                    break
                }
            }
        }
    }

    return $violations
}

function Test-ThreadSafety {
    param([string]$Content, [string]$FileName)

    $violations = @()

    # Check for async void methods (should be async Task)
    if ($Content -match '(?m)^\s*public\s+async\s+void\s+') {
        $violations += @{
            Severity = 'Error'
            Rule = 'VM010'
            Message = "Avoid 'async void' - use 'async Task' for proper error handling"
            File = $FileName
        }
    }

    # Check for proper CancellationToken usage in async methods
    $asyncMethodPattern = '(?m)public\s+async\s+Task\w*\s+(\w+)\s*\([^)]*\)'
    $asyncMethods = [regex]::Matches($Content, $asyncMethodPattern)

    foreach ($method in $asyncMethods) {
        $methodSignature = $method.Value
        if ($methodSignature -notmatch 'CancellationToken') {
            $methodName = $method.Groups[1].Value
            $violations += @{
                Severity = 'Info'
                Rule = 'VM011'
                Message = "Async method '$methodName' should accept CancellationToken parameter"
                File = $FileName
            }
        }
    }

    return $violations
}

function Get-ViewModelFiles {
    param([string]$RootPath)

    $viewModelPath = Join-Path $RootPath "src\WileyWidget.WinForms\ViewModels"

    if (-not (Test-Path $viewModelPath)) {
        Write-Warning "ViewModels directory not found: $viewModelPath"
        return @()
    }

    return Get-ChildItem -Path $viewModelPath -Filter "*ViewModel.cs" -File
}

function Invoke-ValidationRules {
    param(
        [string]$Content,
        [string]$FileName
    )

    $allViolations = @()

    $allViolations += Test-ViewModelInheritance -Content $Content -FileName $FileName
    $allViolations += Test-PropertyImplementation -Content $Content -FileName $FileName
    $allViolations += Test-CommandImplementation -Content $Content -FileName $FileName
    $allViolations += Test-DependencyInjection -Content $Content -FileName $FileName
    $allViolations += Test-DisposablePattern -Content $Content -FileName $FileName
    $allViolations += Test-XmlDocumentation -Content $Content -FileName $FileName
    $allViolations += Test-MvvmPurity -Content $Content -FileName $FileName
    $allViolations += Test-ThreadSafety -Content $Content -FileName $FileName

    return $allViolations
}

function Write-ViolationSummary {
    param([array]$AllViolations)

    $errors = @($AllViolations | Where-Object { $_.Severity -eq 'Error' })
    $warnings = @($AllViolations | Where-Object { $_.Severity -eq 'Warning' })
    $infos = @($AllViolations | Where-Object { $_.Severity -eq 'Info' })

    $errorCount = if ($errors) { $errors.Length } else { 0 }
    $warningCount = if ($warnings) { $warnings.Length } else { 0 }
    $infoCount = if ($infos) { $infos.Length } else { 0 }

    Write-Host "`n╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                    Validation Summary                      ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

    if ($errorCount -gt 0) {
        Write-Host "  ❌ Errors:   $errorCount" -ForegroundColor Red
    } else {
        Write-Host "  ✅ Errors:   0" -ForegroundColor Green
    }

    if ($warningCount -gt 0) {
        Write-Host "  ⚠️  Warnings: $warningCount" -ForegroundColor Yellow
    } else {
        Write-Host "  ✅ Warnings: 0" -ForegroundColor Green
    }

    Write-Host "  ℹ️  Info:     $infoCount" -ForegroundColor Cyan
    Write-Host ""

    return @{
        Errors = $errorCount
        Warnings = $warningCount
        Info = $infoCount
    }
}

function Write-ViolationDetails {
    param([array]$Violations)

    $groupedByFile = $Violations | Group-Object -Property File

    foreach ($fileGroup in $groupedByFile) {
        Write-Host "`n📄 $($fileGroup.Name)" -ForegroundColor White
        Write-Host ("─" * 60) -ForegroundColor Gray

        foreach ($violation in $fileGroup.Group) {
            $icon = switch ($violation.Severity) {
                'Error' { '❌' }
                'Warning' { '⚠️ ' }
                'Info' { 'ℹ️ ' }
            }

            $color = switch ($violation.Severity) {
                'Error' { 'Red' }
                'Warning' { 'Yellow' }
                'Info' { 'Cyan' }
            }

            $lineInfo = if ($violation.ContainsKey('Line') -and $violation.Line) {
                " (Line $($violation.Line))"
            } else {
                ""
            }
            Write-Host "  $icon [$($violation.Rule)] $($violation.Message)$lineInfo" -ForegroundColor $color
        }
    }
}#endregion

#region Main Execution

try {
    Write-ValidationHeader

    $resolvedPath = Resolve-Path $Path -ErrorAction Stop
    Write-Verbose "Scanning ViewModels in: $resolvedPath"

    $viewModelFiles = Get-ViewModelFiles -RootPath $resolvedPath

    if ($viewModelFiles.Count -eq 0) {
        Write-Warning "No ViewModel files found."
        exit 0
    }

    Write-Host "Found $($viewModelFiles.Count) ViewModel files to validate..." -ForegroundColor Green
    Write-Host ""

    $allViolations = @()
    $filesProcessed = 0

    foreach ($file in $viewModelFiles) {
        $filesProcessed++
        Write-Verbose "[$filesProcessed/$($viewModelFiles.Count)] Validating: $($file.Name)"

        $content = Get-Content -Path $file.FullName -Raw -ErrorAction Stop
        $violations = @(Invoke-ValidationRules -Content $content -FileName $file.Name)

        if ($violations -and $violations.Length -gt 0) {
            $allViolations += $violations
        }
    }

    # Display results
    if (-not $allViolations -or $allViolations.Length -eq 0) {
        Write-Host "🎉 All ViewModels passed validation!" -ForegroundColor Green
        Write-Host "   All $($viewModelFiles.Count) ViewModels are fully implemented." -ForegroundColor Green
        exit 0
    }

    Write-ViolationDetails -Violations $allViolations
    $summary = Write-ViolationSummary -AllViolations $allViolations

    # Generate report if requested
    if ($GenerateReport) {
        $reportPath = Join-Path $resolvedPath $OutputPath
        $report = @{
            Timestamp = (Get-Date).ToString('o')
            TotalFiles = $viewModelFiles.Count
            Summary = $summary
            Violations = $allViolations
        }

        $report | ConvertTo-Json -Depth 10 | Set-Content -Path $reportPath -Encoding UTF8
        Write-Host "`n📊 Report generated: $reportPath" -ForegroundColor Cyan
    }

    # Exit with error code if violations found and FailOnViolations is set
    if ($FailOnViolations -and ($summary.Errors -gt 0 -or $summary.Warnings -gt 0)) {
        Write-Host "`n❌ Validation failed with $($summary.Errors) errors and $($summary.Warnings) warnings" -ForegroundColor Red
        exit 1
    }

} catch {
    Write-Error "Validation failed: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}

#endregion
