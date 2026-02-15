# Navigation Debugger Control Script
# This script helps you enable/disable programmatic breakpoints in NavigationDebugger.cs

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Enable", "Disable", "Status")]
    [string]$Action = "Status",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("All", "Entry", "Critical", "Failures")]
    [string]$BreakpointSet = "All"
)

$NavigationDebuggerPath = "src/WileyWidget.WinForms/Diagnostics/NavigationDebugger.cs"

function Get-BreakpointStatus {
    $content = Get-Content $NavigationDebuggerPath -Raw
    
    $breakpoints = @(
        @{ Name = "BP1: ShowPanel Entry"; Pattern = "// Debugger.Break\(\);"; MethodName = "BreakOnShowPanelEntry" }
        @{ Name = "BP2: Navigation Start"; Pattern = "// Debugger.Break\(\);"; MethodName = "BreakOnNavigationStart" }
        @{ Name = "BP3: PanelNavigator NULL (CRITICAL)"; Pattern = "Debugger.Break\(\);"; MethodName = "BreakOnPanelNavigatorNull" }
        @{ Name = "BP4: Before Navigation Action"; Pattern = "// Debugger.Break\(\);"; MethodName = "BreakBeforeNavigationAction" }
        @{ Name = "BP6: Navigation Failed (CRITICAL)"; Pattern = "Debugger.Break\(\);"; MethodName = "BreakOnNavigationFailure" }
        @{ Name = "BP7: Navigation Exception (CRITICAL)"; Pattern = "Debugger.Break\(\);"; MethodName = "BreakOnNavigationException" }
    )
    
    Write-Host "`n=== Navigation Breakpoint Status ===" -ForegroundColor Cyan
    
    foreach ($bp in $breakpoints) {
        $methodContent = $content -match "(?s)$($bp.MethodName).*?(?=public|private|\z)"
        if ($methodContent) {
            $method = $matches[0]
            $isEnabled = $method -notmatch "//\s*Debugger.Break\(\);"

            $status = if ($isEnabled) { "ENABLED ✅" } else { "DISABLED ⏸️" }
            $color = if ($isEnabled) { "Green" } else { "Yellow" }

            Write-Host "$($bp.Name): " -NoNewline
            Write-Host $status -ForegroundColor $color
        }
    }
    
    Write-Host "`n=== Legend ===" -ForegroundColor Cyan
    Write-Host "✅ ENABLED  = Debugger will break at this point"
    Write-Host "⏸️  DISABLED = Breakpoint is commented out (no break)"
    Write-Host "(CRITICAL) = These breakpoints are ALWAYS enabled (critical failures)`n"
}

function Enable-Breakpoint {
    param([string]$Pattern, [string]$Replacement)
    
    $content = Get-Content $NavigationDebuggerPath -Raw
    $updated = $content -replace $Pattern, $Replacement
    Set-Content $NavigationDebuggerPath -Value $updated -NoNewline
}

function Enable-AllBreakpoints {
    Write-Host "`nEnabling all optional breakpoints..." -ForegroundColor Yellow
    
    # Enable BP1 (ShowPanel Entry)
    Enable-Breakpoint `
        -Pattern "(?<=BreakOnShowPanelEntry[\s\S]*?)//\s*Debugger\.Break\(\);" `
        -Replacement "Debugger.Break();"
    
    # Enable BP2 (Navigation Start)
    Enable-Breakpoint `
        -Pattern "(?<=BreakOnNavigationStart[\s\S]*?)//\s*Debugger\.Break\(\);" `
        -Replacement "Debugger.Break();"
    
    # Enable BP4 (Before Navigation Action)
    Enable-Breakpoint `
        -Pattern "(?<=BreakBeforeNavigationAction[\s\S]*?)//\s*Debugger\.Break\(\);" `
        -Replacement "Debugger.Break();"
    
    Write-Host "✅ All optional breakpoints enabled!" -ForegroundColor Green
    Write-Host "⚠️  Critical breakpoints (BP3, BP6, BP7) are always enabled" -ForegroundColor Yellow
}

function Disable-AllBreakpoints {
    Write-Host "`nDisabling all optional breakpoints..." -ForegroundColor Yellow
    
    # Disable BP1 (ShowPanel Entry)
    Enable-Breakpoint `
        -Pattern "(?<=BreakOnShowPanelEntry[\s\S]*?)Debugger\.Break\(\);" `
        -Replacement "// Debugger.Break();"
    
    # Disable BP2 (Navigation Start)
    Enable-Breakpoint `
        -Pattern "(?<=BreakOnNavigationStart[\s\S]*?)Debugger\.Break\(\);" `
        -Replacement "// Debugger.Break();"
    
    # Disable BP4 (Before Navigation Action)
    Enable-Breakpoint `
        -Pattern "(?<=BreakBeforeNavigationAction[\s\S]*?)Debugger\.Break\(\);" `
        -Replacement "// Debugger.Break();"
    
    Write-Host "✅ All optional breakpoints disabled!" -ForegroundColor Green
    Write-Host "⚠️  Critical breakpoints (BP3, BP6, BP7) remain enabled" -ForegroundColor Yellow
}

# Main execution
switch ($Action) {
    "Status" {
        Get-BreakpointStatus
    }
    "Enable" {
        Enable-AllBreakpoints
        Get-BreakpointStatus
    }
    "Disable" {
        Disable-AllBreakpoints
        Get-BreakpointStatus
    }
}

Write-Host "`n=== Usage Examples ===" -ForegroundColor Cyan
Write-Host "Check status:        " -NoNewline; Write-Host "./scripts/Debug-Navigation.ps1 -Action Status" -ForegroundColor White
Write-Host "Enable all:          " -NoNewline; Write-Host "./scripts/Debug-Navigation.ps1 -Action Enable" -ForegroundColor White
Write-Host "Disable optional:    " -NoNewline; Write-Host "./scripts/Debug-Navigation.ps1 -Action Disable" -ForegroundColor White
Write-Host ""
