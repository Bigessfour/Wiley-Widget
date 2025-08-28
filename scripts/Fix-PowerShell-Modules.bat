@echo off
REM PowerShell Module Fix Batch Runner
REM This batch file runs the PowerShell module fix script

echo 🚀 Running PowerShell Module Fix...
echo.

REM Run the PowerShell script
powershell.exe -ExecutionPolicy Bypass -File "%~dp0Fix-PowerShell-Modules-Direct.ps1"

echo.
echo ✅ Script execution complete!
echo.
echo 💡 Next steps:
echo 1. Restart VS Code completely
echo 2. Close all PowerShell terminals
echo 3. Test with: Get-Module -Name PSScriptAnalyzer -ListAvailable
echo.
pause
