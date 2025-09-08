@echo off
REM Manual Syncfusion License Propagation Script
REM Run this to manually update the .env file with Syncfusion license key

echo 🔧 Syncfusion License Propagation Utility
echo =========================================
echo.

cd /d "%~dp0.."

REM Check if Python is available
python --version >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    set PYTHON_CMD=python
) else (
    python3 --version >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        set PYTHON_CMD=python3
    ) else (
        echo ❌ Python not found. Please install Python from https://www.python.org/downloads/
        pause
        exit /b 1
    )
)

echo 📄 Running Syncfusion environment propagation...
echo.

REM Run the Python script
%PYTHON_CMD% scripts\syncfusion_env_propagate.py

REM Check result
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ Syncfusion license configuration updated successfully
) else if %ERRORLEVEL% EQU 2 (
    echo.
    echo ⚠️ Syncfusion license key not found in machine/user environment
    echo Set SYNCFUSION_LICENSE_KEY environment variable or run Azure Key Vault resolution
) else if %ERRORLEVEL% EQU 3 (
    echo.
    echo ⚠️ Syncfusion license key appears to be a placeholder
    echo Update with a valid Syncfusion license key
) else (
    echo.
    echo ❌ Syncfusion environment propagation failed (Exit code: %ERRORLEVEL%)
)

echo.
echo Press any key to continue...
pause >nul
