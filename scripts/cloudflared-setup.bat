@echo off
setlocal enableextensions enabledelayedexpansion

rem -----------------------------------------------------------------------------
rem Cloudflared Setup Script
rem - Authenticates (interactive login)
rem - Creates (or reuses) a named tunnel
rem - Generates config.yml to expose http://localhost:5000
rem - Routes DNS for a custom hostname to the tunnel
rem - Retries failing steps and logs to logs/wiley-widget-tunnel.log
rem -----------------------------------------------------------------------------

rem Usage:
rem   cloudflared-setup.bat <hostname> <tunnel-name> [--run]
rem Examples:
rem   cloudflared-setup.bat widget.townofwiley.gov wiley-widget-tunnel
rem   cloudflared-setup.bat widget.townofwiley.gov wiley-widget-tunnel --run

set "HOSTNAME=%~1"
set "TUNNEL_NAME=%~2"
set "RUN_FLAG=%~3"

if "%HOSTNAME%"=="" (
  echo ERROR: Missing hostname argument.
  echo Usage: %~nx0 ^<hostname^> ^<tunnel-name^> [--run]
  exit /b 2
)
if "%TUNNEL_NAME%"=="" (
  echo ERROR: Missing tunnel-name argument.
  echo Usage: %~nx0 ^<hostname^> ^<tunnel-name^> [--run]
  exit /b 2
)

rem Resolve repo root/log directory relative to this script when stored in "scripts/"
set "SCRIPT_DIR=%~dp0"
set "CANDIDATE_ROOT=%SCRIPT_DIR%.."
pushd "%CANDIDATE_ROOT%" >nul 2>&1
if %errorlevel% neq 0 (
  popd >nul 2>&1
  set "REPO_ROOT=%cd%"
  goto :root_resolved
)
set "REPO_ROOT=%cd%"
popd >nul 2>&1
:root_resolved

set "LOGDIR=%REPO_ROOT%\logs"
if not exist "%LOGDIR%" mkdir "%LOGDIR%" >nul 2>&1
set "LOGFILE=%LOGDIR%\wiley-widget-tunnel.log"

call :log "Starting cloudflared setup | Hostname=%HOSTNAME% | TunnelName=%TUNNEL_NAME%"
call :log "Logs: %LOGFILE%"

rem Check cloudflared availability
where cloudflared >nul 2>&1
if errorlevel 1 (
  call :log "ERROR: cloudflared not found in PATH. Install from https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/install-and-setup/installation"
  exit /b 3
)
for /f "delims=" %%V in ('cloudflared --version 2^>^&1') do set "CF_VERSION=%%V"
call :log "cloudflared detected: %CF_VERSION%"

rem Ensure interactive login has cert
set "CF_USER_HOME=%USERPROFILE%\.cloudflared"
set "CF_CERT=%CF_USER_HOME%\cert.pem"
if exist "%CF_CERT%" (
  call :log "cloudflared cert already present: %CF_CERT%"
) else (
  call :retry 3 10 "cloudflared login"
  if errorlevel 1 (
    call :log "ERROR: cloudflared login failed after retries."
    exit /b 4
  )
  call :wait_for_file "%CF_CERT%" 36 5 "Waiting for cert.pem after login"
  if errorlevel 1 (
    call :log "ERROR: cert.pem not found after login timeout: %CF_CERT%"
    exit /b 5
  )
  call :log "Login complete, cert found: %CF_CERT%"
)

rem Create or reuse the tunnel
call :retry 3 5 "cloudflared tunnel create %TUNNEL_NAME%"

rem Capture most recent credentials file; cloudflared drops a <TUNNEL_ID>.json in ~/.cloudflared
set "TUNNEL_CREDS="
for /f "delims=" %%F in ('dir /b /o:-d "%CF_USER_HOME%\*.json" 2^>nul') do (
  if not defined TUNNEL_CREDS set "TUNNEL_CREDS=%CF_USER_HOME%\%%F"
)
if not defined TUNNEL_CREDS (
  set "CF_PROGDATA=%ProgramData%\Cloudflare\cloudflared"
  for /f "delims=" %%F in ('dir /b /o:-d "%CF_PROGDATA%\*.json" 2^>nul') do (
    if not defined TUNNEL_CREDS set "TUNNEL_CREDS=%CF_PROGDATA%\%%F"
  )
)

if not defined TUNNEL_CREDS (
  call :log "ERROR: Could not locate tunnel credentials JSON after creation."
  call :log "TIP: Run: cloudflared tunnel list"
  exit /b 6
)

for %%A in ("%TUNNEL_CREDS%") do set "TUNNEL_FILE=%%~nxA"
set "TUNNEL_ID=%TUNNEL_FILE:.json=%"
call :log "Tunnel credentials: %TUNNEL_CREDS%"
call :log "Resolved Tunnel ID: %TUNNEL_ID%"

rem Select config directory (ProgramData preferred; fallback to user profile)
set "CF_CFG_DIR=%ProgramData%\Cloudflare\cloudflared"
mkdir "%CF_CFG_DIR%" >nul 2>&1
if errorlevel 1 (
  set "CF_CFG_DIR=%CF_USER_HOME%"
)
set "CF_CFG=%CF_CFG_DIR%\config.yml"

call :log "Writing config: %CF_CFG%"
> "%CF_CFG%" (
  echo tunnel: %TUNNEL_ID%
  echo credentials-file: %TUNNEL_CREDS%
  echo ingress:
  echo(  - hostname: %HOSTNAME%
  echo(    service: http://localhost:5000
  echo(  - service: http_status:404
)
if errorlevel 1 (
  call :log "ERROR: Failed to write config.yml at %CF_CFG%"
  exit /b 7
)

rem Route DNS for the hostname to this tunnel
call :retry 3 5 "cloudflared tunnel route dns %TUNNEL_NAME% %HOSTNAME%"
if errorlevel 1 (
  call :log "ERROR: DNS routing failed for %HOSTNAME% -> %TUNNEL_NAME%"
  exit /b 8
)

call :log "DNS routing configured: %HOSTNAME% -> %TUNNEL_NAME%"

if /i "%RUN_FLAG%"=="--run" (
  call :log "Starting tunnel run (foreground) with config: %CF_CFG%"
  call :log "Press Ctrl+C to stop. Output is also logged to %LOGFILE%"
  cloudflared --config "%CF_CFG%" tunnel run "%TUNNEL_NAME%" >> "%LOGFILE%" 2>&1
  exit /b !errorlevel!
)

call :log "Setup complete. To run: cloudflared --config \"%CF_CFG%\" tunnel run \"%TUNNEL_NAME%\""
exit /b 0

:log
set "STAMP=[%date% %time%]"
echo %STAMP% %~1
>> "%LOGFILE%" echo %STAMP% %~1
exit /b 0

:retry
rem Usage: call :retry <maxAttempts> <sleepSeconds> <command...>
setlocal enabledelayedexpansion
set "MAX=%~1"
set "SLEEP=%~2"
shift
shift
set "CMD=%*"
set /a ATTEMPT=1
:retry_loop
call :log "Attempt !ATTEMPT! of !MAX!: %CMD%"
cmd /c %CMD% >> "%LOGFILE%" 2>&1
set "RC=!errorlevel!"
if "!RC!"=="0" (
  endlocal & exit /b 0
)
if !ATTEMPT! geq !MAX! (
  call :log "Command failed after !MAX! attempts: %CMD% (rc=!RC!)"
  endlocal & exit /b !RC!
)
call :log "Command failed (rc=!RC!). Sleeping !SLEEP!s before retry..."
timeout /t !SLEEP! /nobreak >nul
set /a ATTEMPT+=1
goto :retry_loop

:wait_for_file
rem Usage: call :wait_for_file "<path>" <maxChecks> <sleepSeconds> "<message>"
setlocal
set "WF_PATH=%~1"
set "WF_MAX=%~2"
set "WF_SLEEP=%~3"
set "WF_MSG=%~4"
set /a WF_TRY=0
:wait_loop
if exist "%WF_PATH%" (
  endlocal & exit /b 0
)
if %WF_TRY% geq %WF_MAX% (
  endlocal & exit /b 1
)
if not "%WF_MSG%"=="" call :log "%WF_MSG% (try %WF_TRY%/%WF_MAX%)"
timeout /t %WF_SLEEP% /nobreak >nul
set /a WF_TRY+=1
goto :wait_loop
