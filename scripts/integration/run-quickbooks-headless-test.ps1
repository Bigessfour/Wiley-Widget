param(
    [string]$ScriptPath = "tests/WileyWidget.UITests/Scripts/QuickBooksPanelTest.csx",
    [int]$TimeoutSeconds = 60
)

# Ensure non-interactive auth for tests
$env:WW_SKIP_INTERACTIVE = '1'
$env:WW_PRINT_AUTH_URL = $null

# Provide sensible test defaults if not already present
if (-not $env:SYNCFUSION_LICENSE_KEY) { $env:SYNCFUSION_LICENSE_KEY = 'TEST-SYNCFUSION-KEY' }
if (-not $env:QBO_CLIENT_ID) { $env:QBO_CLIENT_ID = 'test-client-id' }
if (-not $env:QBO_CLIENT_SECRET) { $env:QBO_CLIENT_SECRET = 'test-client-secret' }

Write-Host "Environment variables (effective in this session):"
Write-Host "  WW_SKIP_INTERACTIVE = $($env:WW_SKIP_INTERACTIVE)"
Write-Host "  WW_PRINT_AUTH_URL   = $($env:WW_PRINT_AUTH_URL)"
Write-Host "  SYNCFUSION_LICENSE_KEY = $($env:SYNCFUSION_LICENSE_KEY)"
Write-Host "  QBO_CLIENT_ID = $($env:QBO_CLIENT_ID)"
Write-Host "  QBO_CLIENT_SECRET = **REDACTED**"

Write-Host "Running RunHeadlessFormTest for script $ScriptPath (timeout ${TimeoutSeconds}s) ..."

# Run the MCP tool via dotnet run with robust streaming, logging, and timeout handling
$start = Get-Date

# Ensure logs directory exists
$logsDir = Join-Path $PSScriptRoot 'logs'
if (-not (Test-Path $logsDir)) { New-Item -Path $logsDir -ItemType Directory | Out-Null }

function Get-McpServerProcess {
    Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match 'WileyWidgetMcpServer.csproj' -or ($_.Name -match 'WileyWidgetMcpServer') }
}

function Ensure-McpServerStarted {
    param(
        [int]$TimeoutSeconds = 60
    )

    $existing = Get-McpServerProcess
    if ($existing) {
        Write-Host "Found existing MCP server process (PID $($existing.ProcessId)). Assuming ready."
        return @{ ProcessId = $existing.ProcessId; LogPath = $null; Started = $false }
    }

    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $logPath = Join-Path $logsDir "mcp-server-$timestamp.log"
    Write-Host "Starting MCP server (logs: $logPath)"
    $proc = Start-Process -FilePath 'dotnet' -ArgumentList @('run','--project','tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj') -RedirectStandardOutput $logPath -RedirectStandardError $logPath -NoNewWindow -PassThru

    $end = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $end) {
        if (Test-Path $logPath) {
            $tail = Get-Content $logPath -Tail 50 -Raw
            if ($tail -match 'Starting MCP server') {
                Write-Host "MCP server started (PID $($proc.Id))"
                return @{ ProcessId = $proc.Id; LogPath = $logPath; Started = $true }
            }
        }
        Start-Sleep -Seconds 1
    }

    Write-Error "MCP server did not start within $TimeoutSeconds seconds. Log: $logPath"
    return @{ ProcessId = $proc.Id; LogPath = $logPath; Started = $false }
}

$serverStatus = Ensure-McpServerStarted -TimeoutSeconds $TimeoutSeconds
if (-not $serverStatus.Started -and $serverStatus.LogPath) {
    Write-Error "Failed to start MCP server; aborting. See log: $($serverStatus.LogPath)"
    exit 1
}

$runTimestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runLog = Join-Path $logsDir "run-headless-$runTimestamp.log"
$dotnetArgs = @('run','--project','tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj','--','RunHeadlessFormTest','--scriptPath', $ScriptPath, '--timeoutSeconds', $TimeoutSeconds.ToString())

Write-Host "Executing: dotnet $($dotnetArgs -join ' ')"
Write-Host "Logging to $runLog"
$proc = Start-Process -FilePath 'dotnet' -ArgumentList $dotnetArgs -RedirectStandardOutput $runLog -RedirectStandardError $runLog -NoNewWindow -PassThru

# Stream output to console in real-time using a background job that tails the log file
$tailJob = Start-Job -ScriptBlock {
    param($path)
    # Tail file and write new lines to host
    Get-Content -Path $path -Wait -Tail 0 | ForEach-Object { Write-Host $_ }
} -ArgumentList $runLog

$didExit = $false
try {
    $didExit = $proc.WaitForExit($TimeoutSeconds * 1000)
}
catch {
    Write-Warning "WaitForExit threw: $_"
}

if (-not $didExit) {
    Write-Error "Run did not finish within $TimeoutSeconds seconds. Killing process PID $($proc.Id)"
    try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch {}
    Start-Sleep -Seconds 1
    Write-Host "Last 200 lines of log ($runLog):"
    if (Test-Path $runLog) { Get-Content $runLog -Tail 200 | ForEach-Object { Write-Host $_ } }
    Stop-Job $tailJob -Force -ErrorAction SilentlyContinue
    exit 2
}
else {
    Start-Sleep -Milliseconds 200
    Stop-Job $tailJob -Force -ErrorAction SilentlyContinue
    $duration = (Get-Date) - $start
    Write-Host "Finished in $($duration.TotalSeconds) seconds. Tool output below:"
    Write-Host "--- BEGIN TOOL OUTPUT ($runLog) ---"
    if (Test-Path $runLog) {
        Get-Content $runLog | ForEach-Object { Write-Host $_ }
    } else {
        Write-Host "(no log file found)"
    }
    Write-Host "--- END TOOL OUTPUT ---"

    try { $exitCode = $proc.ExitCode } catch { $exitCode = $null }
    if ($exitCode -ne $null -and $exitCode -ne 0) {
        Write-Error "dotnet process exited with code $exitCode"
        exit $exitCode
    }
}
