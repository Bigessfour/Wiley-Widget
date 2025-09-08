@echo off
REM Wiley Widget Development Environment Setup
REM Ensures all required environment variables are set for development

echo 🚀 Wiley Widget Development Environment Setup
echo ==============================================
echo.

cd /d "%~dp0"

REM Ensure Syncfusion license key is available
echo 🔑 Ensuring Syncfusion license key is available...
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\ensure-syncfusion-license.ps1" -Force

if %ERRORLEVEL% EQU 0 (
    echo ✅ Syncfusion license key setup complete
) else (
    echo ❌ Failed to setup Syncfusion license key
    echo.
    echo 🔧 Manual setup options:
    echo   1. Run: powershell scripts\ensure-syncfusion-license.ps1 -Verify
    echo   2. Check .env file for SYNCFUSION_LICENSE_KEY
    echo   3. Set environment variable manually if needed
    echo.
    pause
    exit /b 1
)

echo.
echo 🎯 Development environment ready!
echo 💡 You can now run: dotnet run --project WileyWidget.csproj
echo.
pause
