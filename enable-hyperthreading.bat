# Enable Hyperthreading for Trunk CLI with .env support
@echo off
echo Loading environment variables from .env file...
powershell -ExecutionPolicy Bypass -File scripts\load-env-for-trunk.ps1
echo.
echo Setting up hyperthreading environment...
powershell -ExecutionPolicy Bypass -File scripts\trunk-env-setup.ps1
echo.
echo ✅ Trunk CLI is now configured with hyperthreading and .env support!
echo 💡 You can now run trunk commands with access to your environment variables
pause
