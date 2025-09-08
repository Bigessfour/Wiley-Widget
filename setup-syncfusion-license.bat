@echo off
REM Complete Syncfusion License Key Setup
REM Makes the license key ALWAYS available across all scenarios

echo Complete Syncfusion License Key Setup
echo =========================================
echo.

cd /d "%~dp0"

REM Ensure we're in the correct directory
if not exist "scripts\ensure-syncfusion-license.ps1" (
    echo ❌ Error: scripts\ensure-syncfusion-license.ps1 not found
    echo Current directory: %CD%
    echo Expected: C:\Users\biges\Desktop\Wiley_Widget
    pause
    exit /b 1
)

echo Step 1: Setting license key in all environment scopes...
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\ensure-syncfusion-license.ps1" -Force

if %ERRORLEVEL% NEQ 0 (
    echo ❌ Failed to set environment variables
    pause
    exit /b 1
)

echo.
echo Step 2: Installing scheduled task for persistence...
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\schedule-license-monitor.ps1" -Install

if %ERRORLEVEL% NEQ 0 (
    echo ⚠️ Scheduled task installation failed (continuing anyway)
)

echo.
echo Step 3: Verifying setup...
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\ensure-syncfusion-license.ps1" -Verify

echo.
echo Setup Complete!
echo ================
echo.
echo Syncfusion license key is now available in:
echo    • Machine environment (persistent across reboots)
echo    • User environment (persistent for your account)
echo    • Process environment (current session)
echo    • Windows Registry (backup)
echo.
echo Automatic monitoring:
echo    • PowerShell profile loads key on startup
echo    • Scheduled task checks every hour
echo    • Application startup verifies key availability
echo.
echo Test the setup:
echo    • Restart PowerShell and check: $env:SYNCFUSION_LICENSE_KEY
echo    • Run application: dotnet run --project WileyWidget.csproj
echo    • Verify no more "invalid syncfusion key" errors
echo.
echo Manual commands if needed:
echo    • Verify: .\scripts\ensure-syncfusion-license.ps1 -Verify
echo    • Update: .\scripts\ensure-syncfusion-license.ps1 -Force
echo    • Monitor: .\scripts\schedule-license-monitor.ps1 -Run
echo.
pause
