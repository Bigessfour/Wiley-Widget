# Debug Conhost Script for Wiley Widget
# This script helps debug the console host (conhost.exe) during WPF application startup

param(
    [switch]$DebugConhost,
    [switch]$EarlyDebug,
    [switch]$Help
)

function Show-Help {
    Write-Host "Wiley Widget Conhost Debugging Script" -ForegroundColor Cyan
    Write-Host "====================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "This script helps you debug the console host process during application startup."
    Write-Host ""
    Write-Host "USAGE:" -ForegroundColor Yellow
    Write-Host "  .\debug-conhost.ps1 -DebugConhost    # Debug during App constructor"
    Write-Host "  .\debug-conhost.ps1 -EarlyDebug      # Debug during Program.Main (earliest point)"
    Write-Host "  .\debug-conhost.ps1 -Help            # Show this help"
    Write-Host ""
    Write-Host "DEBUGGING STEPS:" -ForegroundColor Yellow
    Write-Host "1. Run this script with one of the debug flags"
    Write-Host "2. The application will pause and show process information"
    Write-Host "3. In Visual Studio: Debug → Attach to Process"
    Write-Host "4. Look for processes:"
    Write-Host "   - conhost.exe (Console Host) - for console debugging"
    Write-Host "   - WileyWidget.exe - for main process debugging"
    Write-Host "5. Select the process and click Attach"
    Write-Host "6. Set breakpoints in your code as needed"
    Write-Host "7. Press ENTER in the console to continue application startup"
    Write-Host ""
    Write-Host "TIPS:" -ForegroundColor Green
    Write-Host "- Use -EarlyDebug for debugging the earliest startup code"
    Write-Host "- Use -DebugConhost for debugging WPF initialization"
    Write-Host "- The console window will stay visible during debugging"
    Write-Host "- Check the console output for process ID and other info"
}

if ($Help) {
    Show-Help
    exit
}

# Build the application first
Write-Host "Building Wiley Widget..." -ForegroundColor Yellow
dotnet clean WileyWidget.csproj
dotnet build WileyWidget.csproj

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed! Cannot start debugging." -ForegroundColor Red
    exit 1
}

# Prepare arguments
$args = @()
if ($DebugConhost) {
    $args += "--debug-conhost"
    Write-Host "Starting in Conhost Debug Mode..." -ForegroundColor Green
    Write-Host "The application will pause during App constructor initialization." -ForegroundColor Cyan
} elseif ($EarlyDebug) {
    $args += "--debug-conhost"
    Write-Host "Starting in Early Debug Mode..." -ForegroundColor Green
    Write-Host "The application will pause during Program.Main (earliest startup point)." -ForegroundColor Cyan
} else {
    Write-Host "Starting Wiley Widget normally..." -ForegroundColor Green
}

Write-Host ""
Write-Host "DEBUGGING INSTRUCTIONS:" -ForegroundColor Yellow
Write-Host "1. Application will show process info and wait"
Write-Host "2. Open Visual Studio → Debug → Attach to Process"
Write-Host "3. Look for: conhost.exe or WileyWidget.exe"
Write-Host "4. Attach debugger, set breakpoints"
Write-Host "5. Press ENTER in console to continue"
Write-Host ""

# Run the application
Write-Host "Launching application..." -ForegroundColor Green
dotnet run --project WileyWidget.csproj $args
