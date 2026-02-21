#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Categorize unwired methods by implementation strategy
#>

$ErrorActionPreference = 'Stop'
$methodsPath = "tmp/methods-to-implement.json"

if (-not (Test-Path $methodsPath)) {
    Write-Error "Run Verify-AllDeadCode.ps1 first"
    exit 1
}

$methods = Get-Content $methodsPath | ConvertFrom-Json

# Categories
$viewModelCommands = @()
$eventHandlers = @()
$helperMethods = @()
$invalid = @()

foreach ($method in $methods) {
    $name = $method.Method
    $file = $method.File

    # Category 1: ViewModel commands
    if ($file -match 'ViewModels\\' -and
        ($name -match 'Async$' -or $name -match '^Can' -or
         $name -match 'Delete|Save|Load|Create|Import|Export|Sync|Apply|Refresh|Clear|Mark|Calculate|Reset|Generate')) {
        $action = if ($name -match '^Can') { "Add CanExecute to RelayCommand" }
                  else { "Add [RelayCommand] attribute" }
        $viewModelCommands += [PSCustomObject]@{
            Method = $name
            File = $file
            Line = $method.Line
            Action = $action
        }
        continue
    }

    # Category 2: Event Handlers
    if ($name -match '_Click$|_Changed$|_Activated$|_Load$|DockState|ZOrder') {
        $eventHandlers += [PSCustomObject]@{
            Method = $name
            File = $file
            Line = $method.Line
            Action = "Wire up event subscription"
        }
        continue
    }

    # Category 3: Invalid entries
    if ($name -eq 'readonly' -or $name.Length -lt 3) {
        $invalid += [PSCustomObject]@{
            Method = $name
            File = $file
            Line = $method.Line
            Action = "Investigate/Fix"
        }
        continue
    }

    # Category 4: Helper methods
    $helperMethods += [PSCustomObject]@{
        Method = $name
        File = $file
        Line = $method.Line
        Action = "Call from code OR delete if unused"
    }
}

# Display results
Write-Host "`n📊 CATEGORIZED IMPLEMENTATION PLAN" -ForegroundColor Cyan
Write-Host ("=" * 70)

Write-Host "`n🎯 1. VIEWMODEL COMMANDS: $($viewModelCommands.Count) methods" -ForegroundColor Yellow
Write-Host "   Action: Add [RelayCommand] attributes or wire to Command properties"
Write-Host ("   " + ("-" * 66))
$viewModelCommands | Select-Object Method, File, Action | Format-Table -AutoSize

Write-Host "`n🔌 2. EVENT HANDLERS: $($eventHandlers.Count) methods" -ForegroundColor Yellow
Write-Host "   Action: Wire up event subscriptions (+= methodName)"
Write-Host ("   " + ("-" * 66))
$eventHandlers | Select-Object Method, File | Format-Table -AutoSize

Write-Host "`n🛠️ 3. HELPER METHODS: $($helperMethods.Count) methods" -ForegroundColor Yellow
Write-Host "   Action: Call from appropriate locations OR mark [Obsolete] and delete"
Write-Host ("   " + ("-" * 66))
$helperMethods | Select-Object Method, File | Format-Table -AutoSize

if ($invalid.Count -gt 0) {
    Write-Host "`n⚠️ 4. INVALID/NEEDS REVIEW: $($invalid.Count) entries" -ForegroundColor Red
    $invalid | Format-Table -AutoSize
}

# Summary
Write-Host "`n📋 IMPLEMENTATION SUMMARY" -ForegroundColor Cyan
Write-Host ("=" * 70)
Write-Host "Total methods: $($methods.Count)"
Write-Host "  - ViewModel Commands: $($viewModelCommands.Count) (add [RelayCommand])"
Write-Host "  - Event Handlers: $($eventHandlers.Count) (wire up events)"
Write-Host "  - Helper Methods: $($helperMethods.Count) (implement or delete)"
Write-Host "  - Invalid: $($invalid.Count) (needs review)"

# Save categorized results
$output = @{
    ViewModelCommands = $viewModelCommands
    EventHandlers = $eventHandlers
    HelperMethods = $helperMethods
    Invalid = $invalid
    Summary = @{
        Total = $methods.Count
        ViewModelCommands = $viewModelCommands.Count
        EventHandlers = $eventHandlers.Count
        HelperMethods = $helperMethods.Count
        Invalid = $invalid.Count
    }
}

$outputPath = "tmp/implementation-plan.json"
$output | ConvertTo-Json -Depth 4 | Out-File $outputPath
Write-Host "`n💾 Saved categorized plan to: $outputPath" -ForegroundColor Green
