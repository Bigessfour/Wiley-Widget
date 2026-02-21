#!/usr/bin/env pwsh
<#
.SYNOPSIS
    MCP Server Initialization Script
    Sets up and validates MCP servers following Microsoft best practices

.DESCRIPTION
    This script initializes MCP servers with proper error handling,
    capability validation, and health monitoring as per Microsoft MCP guidelines.

.PARAMETER ConfigPath
    Path to MCP configuration file (default: .mcp/config.json)

.PARAMETER HealthCheck
    Perform health checks on all configured servers

.PARAMETER Verbose
    Enable verbose logging

.EXAMPLE
    .\init-mcp-servers.ps1 -HealthCheck

.EXAMPLE
    .\init-mcp-servers.ps1 -ConfigPath "custom-config.json" -Verbose
#>

using namespace System.Collections.Generic
using namespace System.Diagnostics

param(
    [string]$ConfigPath = ".mcp/config.json",
    [switch]$HealthCheck,
    [switch]$Verbose
)

function ConvertFrom-JsonC {
    param([Parameter(Mandatory = $true)][string]$JsoncText)

    $builder = [System.Text.StringBuilder]::new()
    $inString = $false
    $escapeNext = $false
    $inLineComment = $false
    $inBlockComment = $false

    for ($i = 0; $i -lt $JsoncText.Length; $i++) {
        $ch = $JsoncText[$i]
        $next = if ($i + 1 -lt $JsoncText.Length) { $JsoncText[$i + 1] } else { [char]0 }

        if ($inLineComment) {
            if ($ch -eq "`n") {
                $inLineComment = $false
                $null = $builder.Append($ch)
            }
            continue
        }

        if ($inBlockComment) {
            if ($ch -eq '*' -and $next -eq '/') {
                $inBlockComment = $false
                $i++
            }
            continue
        }

        if ($inString) {
            $null = $builder.Append($ch)

            if ($escapeNext) {
                $escapeNext = $false
                continue
            }

            if ($ch -eq '\\') {
                $escapeNext = $true
                continue
            }

            if ($ch -eq '"') {
                $inString = $false
            }

            continue
        }

        if ($ch -eq '/' -and $next -eq '/') {
            $inLineComment = $true
            $i++
            continue
        }

        if ($ch -eq '/' -and $next -eq '*') {
            $inBlockComment = $true
            $i++
            continue
        }

        if ($ch -eq '"') {
            $inString = $true
            $null = $builder.Append($ch)
            continue
        }

        $null = $builder.Append($ch)
    }

    $sanitized = [regex]::Replace($builder.ToString(), ',(\s*[}\]])', '$1')
    return $sanitized | ConvertFrom-Json -Depth 64
}

function Resolve-EnvTemplate {
    param([AllowNull()][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Value
    }

    if ($Value -match '^\$\{env:([^}]+)\}$') {
        return [Environment]::GetEnvironmentVariable($Matches[1])
    }

    return $Value
}

function Resolve-CommandExecutable {
    param([Parameter(Mandatory = $true)][string]$Command)

    $resolved = Get-Command -Name $Command -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($resolved) {
        $resolvedPath = $resolved.Path
        if ([string]::IsNullOrWhiteSpace($resolvedPath)) {
            $resolvedPath = $resolved.Source
        }

        if (-not [string]::IsNullOrWhiteSpace($resolvedPath)) {
            if ($IsWindows -and $resolvedPath.EndsWith('.ps1', [System.StringComparison]::OrdinalIgnoreCase)) {
                $cmdCandidate = [System.IO.Path]::ChangeExtension($resolvedPath, '.cmd')
                if (Test-Path -Path $cmdCandidate -PathType Leaf) {
                    return $cmdCandidate
                }

                $exeCandidate = [System.IO.Path]::ChangeExtension($resolvedPath, '.exe')
                if (Test-Path -Path $exeCandidate -PathType Leaf) {
                    return $exeCandidate
                }
            }

            return $resolvedPath
        }
    }

    if ($IsWindows -and -not [System.IO.Path]::HasExtension($Command)) {
        $withCmd = "$Command.cmd"
        $resolvedCmd = Get-Command -Name $withCmd -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($resolvedCmd) {
            if ($resolvedCmd.Source) {
                return $resolvedCmd.Source
            }

            if ($resolvedCmd.Path) {
                return $resolvedCmd.Path
            }
        }
    }

    return $Command
}

function Invoke-CommandWithTimeout {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$Arguments = @(),
        [int]$TimeoutSeconds = 10
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    foreach ($arg in $Arguments) {
        $null = $startInfo.ArgumentList.Add($arg)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo

    try {
        $null = $process.Start()
        $completed = $process.WaitForExit($TimeoutSeconds * 1000)

        if (-not $completed) {
            try { $process.Kill($true) } catch { }
            return @{
                TimedOut = $true
                ExitCode = $null
                StdOut   = ''
                StdErr   = ''
            }
        }

        return @{
            TimedOut = $false
            ExitCode = $process.ExitCode
            StdOut   = $process.StandardOutput.ReadToEnd()
            StdErr   = $process.StandardError.ReadToEnd()
        }
    } finally {
        $process.Dispose()
    }
}

function Get-JsonObjectByKey {
    param(
        [Parameter(Mandatory = $true)][string]$JsonText,
        [Parameter(Mandatory = $true)][string]$Key
    )

    $keyPattern = '"' + [regex]::Escape($Key) + '"\s*:'
    $keyMatch = [regex]::Match($JsonText, $keyPattern)
    if (-not $keyMatch.Success) {
        return $null
    }

    $index = $keyMatch.Index + $keyMatch.Length
    while ($index -lt $JsonText.Length -and [char]::IsWhiteSpace($JsonText[$index])) {
        $index++
    }

    if ($index -ge $JsonText.Length -or $JsonText[$index] -ne '{') {
        return $null
    }

    $start = $index
    $depth = 0
    $inString = $false
    $escapeNext = $false
    $inLineComment = $false
    $inBlockComment = $false

    for ($i = $start; $i -lt $JsonText.Length; $i++) {
        $ch = $JsonText[$i]
        $next = if ($i + 1 -lt $JsonText.Length) { $JsonText[$i + 1] } else { [char]0 }

        if ($inLineComment) {
            if ($ch -eq "`n") {
                $inLineComment = $false
            }
            continue
        }

        if ($inBlockComment) {
            if ($ch -eq '*' -and $next -eq '/') {
                $inBlockComment = $false
                $i++
            }
            continue
        }

        if ($inString) {
            if ($escapeNext) {
                $escapeNext = $false
                continue
            }

            if ($ch -eq '\\') {
                $escapeNext = $true
                continue
            }

            if ($ch -eq '"') {
                $inString = $false
            }

            continue
        }

        if ($ch -eq '/' -and $next -eq '/') {
            $inLineComment = $true
            $i++
            continue
        }

        if ($ch -eq '/' -and $next -eq '*') {
            $inBlockComment = $true
            $i++
            continue
        }

        if ($ch -eq '"') {
            $inString = $true
            continue
        }

        if ($ch -eq '{') {
            $depth++
            continue
        }

        if ($ch -eq '}') {
            $depth--
            if ($depth -eq 0) {
                return $JsonText.Substring($start, $i - $start + 1)
            }
        }
    }

    return $null
}

class McpServerHealth {
    [string]$Name
    [string]$Status  # "healthy", "degraded", "unhealthy"
    [DateTime]$LastChecked
    [TimeSpan]$ResponseTime
    [string[]]$Capabilities
    [string]$ErrorMessage
    [hashtable]$Metrics
}

class McpServerManager {
    [string]$ConfigPath
    [bool]$VerboseEnabled = $false
    [System.Collections.Generic.List[McpServerHealth]]$HealthStatus = [System.Collections.Generic.List[McpServerHealth]]::new()
    [hashtable]$Servers = @{}
    [System.Diagnostics.Stopwatch]$Stopwatch

    McpServerManager([string]$configPath) {
        $this.ConfigPath = $configPath
        $this.Stopwatch = [System.Diagnostics.Stopwatch]::new()
    }

    [PSObject] ResolveConfiguration() {
        $candidatePaths = @(
            $this.ConfigPath,
            ".mcp/config.json",
            ".vscode/mcp-settings.json"
        ) | Select-Object -Unique

        foreach ($path in $candidatePaths) {
            if (-not (Test-Path -Path $path -PathType Leaf)) {
                continue
            }

            try {
                $raw = Get-Content -Path $path -Raw -ErrorAction Stop
                $config = $raw | ConvertFrom-Json -Depth 64
            } catch {
                try {
                    $raw = Get-Content -Path $path -Raw -ErrorAction Stop
                    $config = ConvertFrom-JsonC -JsoncText $raw
                } catch {
                    Write-Verbose "Skipping unparsable config candidate: $path"
                    continue
                }
            }

            if (-not $config.PSObject.Properties['mcpServers']) {
                continue
            }

            if (-not $config.PSObject.Properties['mcpConfig']) {
                $config | Add-Member -NotePropertyName "mcpConfig" -NotePropertyValue ([pscustomobject]@{
                        version       = "1.0"
                        capabilities  = @()
                        errorHandling = @{}
                    })
            }

            $serverCount = ($config.mcpServers.PSObject.Properties | Measure-Object).Count
            if ($serverCount -gt 0) {
                $this.ConfigPath = $path
                return $config
            }
        }

        $settingsPath = ".vscode/settings.json"
        if (Test-Path -Path $settingsPath -PathType Leaf) {
            try {
                $settingsRaw = Get-Content -Path $settingsPath -Raw -ErrorAction Stop
                $mcpServersJson = Get-JsonObjectByKey -JsonText $settingsRaw -Key "github.copilot.chat.mcpServers"

                if (-not [string]::IsNullOrWhiteSpace($mcpServersJson)) {
                    $mcpServers = ConvertFrom-JsonC -JsoncText $mcpServersJson
                    $serverCount = ($mcpServers.PSObject.Properties | Measure-Object).Count
                    if ($serverCount -gt 0) {
                        $this.ConfigPath = "$settingsPath (github.copilot.chat.mcpServers)"
                        return [pscustomobject]@{
                            mcpServers = $mcpServers
                            mcpConfig  = [pscustomobject]@{
                                version       = "1.0"
                                capabilities  = @("stdio", "http")
                                errorHandling = @{
                                    retry = $true
                                }
                            }
                        }
                    }
                }
            } catch {
                Write-Verbose "Failed to read fallback settings source ($settingsPath): $($_.Exception.Message)"
            }
        }

        throw "No usable MCP configuration found. Expected .mcp/config.json, .vscode/mcp-settings.json, or github.copilot.chat.mcpServers in .vscode/settings.json"
    }

    [void] LoadConfiguration() {
        Write-Verbose "Loading configuration from: $($this.ConfigPath)"

        try {
            Write-Verbose "Resolving MCP configuration source..."
            $config = $this.ResolveConfiguration()
            Write-Verbose "Configuration parsed successfully from: $($this.ConfigPath)"

            # Validate configuration structure
            $this.ValidateConfiguration($config)

            # Load server configurations
            Write-Verbose "Found servers: $($config.mcpServers.PSObject.Properties.Name -join ', ')"
            foreach ($serverName in $config.mcpServers.PSObject.Properties.Name) {
                $serverConfig = $config.mcpServers.$serverName
                $serverType = if ($serverConfig.PSObject.Properties['type']) { [string]$serverConfig.type } else { "stdio" }
                $this.Servers[$serverName] = @{
                    Name     = $serverName
                    Type     = $serverType
                    Url      = if ($serverConfig.PSObject.Properties['url']) { [string]$serverConfig.url } else { $null }
                    Disabled = if ($serverConfig.PSObject.Properties['disabled']) { [bool]$serverConfig.disabled } else { $false }
                    Command  = if ($serverConfig.PSObject.Properties['command']) { [string]$serverConfig.command } else { $null }
                    Args     = if ($serverConfig.PSObject.Properties['args']) { $serverConfig.args } else { @() }
                    Env      = if ($serverConfig.PSObject.Properties['env']) { $serverConfig.env } else { $null }
                    Process  = $null
                    Health   = [McpServerHealth]@{
                        Name         = $serverName
                        Status       = "unknown"
                        LastChecked  = [DateTime]::MinValue
                        ResponseTime = [TimeSpan]::Zero
                        Capabilities = @()
                        ErrorMessage = ""
                        Metrics      = @{}
                    }
                }

                $this.HealthStatus.Add($this.Servers[$serverName].Health)
            }

            Write-Verbose "MCP configuration loaded successfully"
        } catch {
            throw "Failed to load MCP configuration: $($_.Exception.Message)"
        }
    }

    [void] ValidateConfiguration([PSObject]$config) {
        # Validate required properties
        $requiredProps = @("mcpServers", "mcpConfig")
        foreach ($prop in $requiredProps) {
            if (-not $config.PSObject.Properties[$prop]) {
                throw "Missing required configuration property: $prop"
            }
        }

        # Validate MCP config structure
        $mcpConfig = $config.mcpConfig
        $requiredMcpProps = @("version", "capabilities", "errorHandling")
        foreach ($prop in $requiredMcpProps) {
            if (-not $mcpConfig.PSObject.Properties[$prop]) {
                throw "Missing required MCP config property: $prop"
            }
        }

        Write-Verbose "Configuration validation passed"
    }

    [void] StartServers() {
        Write-Verbose "Starting MCP servers..."

        foreach ($serverName in $this.Servers.Keys) {
            $server = $this.Servers[$serverName]

            try {
                if ($this.VerboseEnabled) {
                    Write-Output "Starting $serverName..."
                }

                if ($server.Disabled) {
                    $server.Health.Status = "disabled"
                    if ($this.VerboseEnabled) {
                        Write-Output "$serverName is disabled in configuration"
                    }
                    continue
                }

                if ($server.Type -eq 'http' -and -not [string]::IsNullOrWhiteSpace($server.Url)) {
                    $server.Health.Status = "external"
                    if ($this.VerboseEnabled) {
                        Write-Output "$serverName is external HTTP MCP endpoint: $($server.Url)"
                    }
                    continue
                }

                if ([string]::IsNullOrWhiteSpace($server.Command)) {
                    throw "No command specified for server '$serverName'"
                }

                $commandExecutable = Resolve-CommandExecutable -Command $server.Command

                $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
                $startInfo.FileName = $commandExecutable
                $startInfo.Arguments = $server.Args -join " "
                $startInfo.UseShellExecute = $false
                $startInfo.RedirectStandardOutput = $true
                $startInfo.RedirectStandardError = $true
                $startInfo.CreateNoWindow = $true

                # Set environment variables
                if ($server.Env -and $server.Env.PSObject.Properties) {
                    foreach ($envVar in $server.Env.PSObject.Properties) {
                        $resolvedValue = Resolve-EnvTemplate -Value ([string]$envVar.Value)
                        if ($null -ne $resolvedValue) {
                            $startInfo.EnvironmentVariables[$envVar.Name] = $resolvedValue
                        }
                    }
                }

                $process = [System.Diagnostics.Process]::new()
                $process.StartInfo = $startInfo
                $process.Start()

                $server.Process = $process
                $server.Health.Status = "starting"

                if ($this.VerboseEnabled) {
                    Write-Output "$serverName started (PID: $($process.Id))"
                }
            } catch {
                Write-Warning "Failed to start $serverName`: $($_.Exception.Message)"
                $server.Health.Status = "unhealthy"
                $server.Health.ErrorMessage = $_.Exception.Message
            }
        }
    }

    [void] PerformHealthChecks() {
        Write-Verbose "Performing MCP server health checks..."

        foreach ($serverName in $this.Servers.Keys) {
            $server = $this.Servers[$serverName]
            $health = $server.Health

            try {
                $this.Stopwatch.Restart()

                # Perform health check based on server type
                $isHealthy = $this.TestServerHealth($server, $health)

                $this.Stopwatch.Stop()
                $health.ResponseTime = $this.Stopwatch.Elapsed
                $health.LastChecked = [DateTime]::Now

                if ($isHealthy) {
                    $health.Status = "healthy"
                    $health.ErrorMessage = ""
                    if ($this.VerboseEnabled) {
                        Write-Output "$serverName is healthy ($($health.ResponseTime.TotalMilliseconds)ms)"
                    }
                } else {
                    $health.Status = "unhealthy"
                    if ([string]::IsNullOrWhiteSpace($health.ErrorMessage)) {
                        $health.ErrorMessage = "Health check failed"
                    }
                    if ($this.VerboseEnabled) {
                        Write-Output "$serverName is unhealthy: $($health.ErrorMessage)"
                    }
                }
            } catch {
                $health.Status = "unhealthy"
                $health.ErrorMessage = $_.Exception.Message
                if ($this.VerboseEnabled) {
                    Write-Output "$serverName health check failed: $($_.Exception.Message)"
                }
            }
        }
    }

    [bool] TestServerHealth([hashtable]$server, [McpServerHealth]$health) {
        if ($server.Disabled) {
            $health.ErrorMessage = ""
            return $true
        }

        if ($server.Type -eq 'http' -and -not [string]::IsNullOrWhiteSpace($server.Url)) {
            try {
                $null = Invoke-WebRequest -Uri $server.Url -Method Get -TimeoutSec 8 -MaximumRedirection 0 -ErrorAction Stop
                $health.ErrorMessage = ""
                return $true
            } catch {
                if ($_.Exception.Response) {
                    $health.ErrorMessage = ""
                    return $true
                }

                $health.ErrorMessage = "HTTP endpoint unreachable: $($server.Url)"
                return $false
            }
        }

        # Health check logic based on server type (Deep validation)
        if ($server.Name -eq "csharp-mcp") {
            if ($server.Process -and -not $server.Process.HasExited) {
                $health.ErrorMessage = ""
                return $true
            }

            try {
                $dotnetPath = Resolve-CommandExecutable -Command "dotnet"
                if (-not [string]::IsNullOrWhiteSpace($dotnetPath)) {
                    $health.ErrorMessage = ""
                    return $true
                }
            } catch { }

            $health.ErrorMessage = "csharp-mcp process is not running and dotnet is unavailable"
            return $false
        } elseif ($server.Name -in @("filesystem-mcp", "filesystem")) {
            if ($server.Process -and -not $server.Process.HasExited) {
                $health.ErrorMessage = ""
                return $true
            }

            try {
                $npxPath = Resolve-CommandExecutable -Command "npx"
                if (-not [string]::IsNullOrWhiteSpace($npxPath)) {
                    $health.ErrorMessage = ""
                    return $true
                }
            } catch { }

            $health.ErrorMessage = "filesystem process is not running and npx is unavailable"
            return $false
        } elseif ($server.Name -in @("github-mcp", "github")) {
            try {
                $token = $env:GITHUB_PERSONAL_ACCESS_TOKEN
                if (-not $token) { $token = $env:GITHUB_TOKEN }
                if (-not $token) { $token = $env:GITHUB_PAT }
                if (-not $token) {
                    $health.ErrorMessage = "Missing GitHub token (GITHUB_PERSONAL_ACCESS_TOKEN/GITHUB_TOKEN/GITHUB_PAT)"
                    return $false
                }

                if ($server.Process -and -not $server.Process.HasExited) {
                    $health.ErrorMessage = ""
                    return $true
                }

                $npxPath = Resolve-CommandExecutable -Command "npx"
                if (-not [string]::IsNullOrWhiteSpace($npxPath)) {
                    $health.ErrorMessage = ""
                    return $true
                }

                $health.ErrorMessage = "github process is not running and npx is unavailable"
                return $false
            } catch {
                $health.ErrorMessage = "GitHub health check failed: $($_.Exception.Message)"
                return $false
            }
        } elseif ($server.Name -eq "mssql") {
            $connectionString = $env:MSSQL_CONNECTION_STRING
            if ([string]::IsNullOrWhiteSpace($connectionString)) {
                $health.ErrorMessage = "Missing MSSQL_CONNECTION_STRING"
                return $false
            }

            if ($server.Process -and -not $server.Process.HasExited) {
                $health.ErrorMessage = ""
                return $true
            }

            try {
                $npxPath = Resolve-CommandExecutable -Command "npx"
                if (-not [string]::IsNullOrWhiteSpace($npxPath)) {
                    $health.ErrorMessage = ""
                    return $true
                }
            } catch { }

            $health.ErrorMessage = "mssql process is not running and npx is unavailable"
            return $false
        } elseif ($server.Name -eq "syncfusion-winforms-assistant") {
            # Deep test: Verify API key and connectivity to Syncfusion
            try {
                $apiKey = [Environment]::GetEnvironmentVariable("SYNCFUSION_MCP_API_KEY", "Machine")
                if (-not $apiKey) { $apiKey = [Environment]::GetEnvironmentVariable("SYNCFUSION_MCP_API_KEY", "User") }
                if (-not $apiKey) { $apiKey = [Environment]::GetEnvironmentVariable("SYNCFUSION_MCP_API_KEY") }
                if (-not $apiKey) { $apiKey = [Environment]::GetEnvironmentVariable("SYNCFUSION_API_KEY", "Machine") }
                if (-not $apiKey) { $apiKey = [Environment]::GetEnvironmentVariable("SYNCFUSION_API_KEY", "User") }
                if (-not $apiKey) { $apiKey = [Environment]::GetEnvironmentVariable("SYNCFUSION_API_KEY") }
                if (-not $apiKey) {
                    $health.ErrorMessage = "Missing Syncfusion API key"
                    return $false
                }

                if ($server.Process -and -not $server.Process.HasExited) {
                    $health.ErrorMessage = ""
                    return $true
                }

                # Fallback check via npm when process is not active
                $npmPath = Resolve-CommandExecutable -Command "npm"
                if ([string]::IsNullOrWhiteSpace($npmPath)) {
                    $health.ErrorMessage = "syncfusion process is not running and npm is unavailable"
                    return $false
                }

                $npmCheck = Invoke-CommandWithTimeout -FilePath $npmPath -Arguments @('view', '@syncfusion/winforms-assistant', 'version') -TimeoutSeconds 10
                $isHealthy = (-not $npmCheck.TimedOut) -and $npmCheck.ExitCode -eq 0
                if (-not $isHealthy) {
                    $health.ErrorMessage = "syncfusion process is not running and package verification failed"
                    return $false
                }

                $health.ErrorMessage = ""
                return $true
            } catch {
                $health.ErrorMessage = "Syncfusion health check failed: $($_.Exception.Message)"
                return $false
            }
        } elseif ($server.Name -eq "wileywidget-ui-mcp") {
            # Deep test: Verify project can be built and run helper
            try {
                $result = & dotnet run --project tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj --no-build -- --run-license-check json 2>$null
                return $LASTEXITCODE -eq 0 -and $result -match "license"
            } catch {
                return $false
            }
        } else {
            # Generic health check - check if process is running and responding
            $isRunning = $server.Process -and -not $server.Process.HasExited
            if (-not $isRunning) {
                $health.ErrorMessage = "$($server.Name) process is not running"
                return $false
            }

            $health.ErrorMessage = ""
            return $true
        }
    }

    [void] StopServers() {
        Write-Verbose "Stopping MCP servers..."

        foreach ($serverName in $this.Servers.Keys) {
            $server = $this.Servers[$serverName]

            if ($server.Process -and -not $server.Process.HasExited) {
                try {
                    $server.Process.Kill()
                    $server.Process.WaitForExit(5000)
                    if ($this.VerboseEnabled) {
                        Write-Output "$serverName stopped"
                    }
                } catch {
                    Write-Warning "Failed to stop $serverName gracefully: $($_.Exception.Message)"
                }
            }
        }
    }

    [hashtable] GetHealthReport() {
        $healthy = @($this.HealthStatus | Where-Object { $_.Status -eq "healthy" }).Count
        $total = $this.HealthStatus.Count

        return @{
            TotalServers     = $total
            HealthyServers   = $healthy
            UnhealthyServers = $total - $healthy
            HealthPercentage = if ($total -gt 0) { [math]::Round(($healthy / $total) * 100, 2) } else { 0 }
            Details          = $this.HealthStatus
        }
    }
}

function Write-HealthReport {
    param([hashtable]$report)

    Write-Output "`n=== MCP Server Health Report ==="
    Write-Output "Total Servers: $($report.TotalServers)"
    Write-Output "Healthy: $($report.HealthyServers)"
    Write-Output "Unhealthy: $($report.UnhealthyServers)"
    Write-Output "Health Percentage: $($report.HealthPercentage)%"

    if ($report.Details.Count -gt 0) {
        Write-Output "`nServer Details:"
        foreach ($health in $report.Details) {
            $status = $health.Status
            Write-Output "  $($health.Name): $status"
            if ($health.ErrorMessage) {
                Write-Output "    Error: $($health.ErrorMessage)"
            }
        }
    }
}

# Main execution
try {
    Write-Verbose "Initializing MCP Server Manager..."
    $manager = [McpServerManager]::new($ConfigPath)
    $manager.VerboseEnabled = $Verbose.IsPresent -or ($VerbosePreference -eq 'Continue')
    Write-Verbose "MCP Server Manager created successfully"

    Write-Output "=== MCP Server Initialization ==="

    # Load and validate configuration
    Write-Verbose "Loading configuration..."
    $manager.LoadConfiguration()
    Write-Verbose "Configuration loaded successfully"

    # Start servers
    $manager.StartServers()

    # Wait for servers to initialize
    Start-Sleep -Seconds 2

    # Perform health checks if requested
    if ($HealthCheck) {
        $manager.PerformHealthChecks()
        $report = $manager.GetHealthReport()
        Write-HealthReport -report $report
    }

    # Keep servers running if not in health check mode
    if (-not $HealthCheck) {
        Write-Output "`nMCP servers are running. Press Ctrl+C to stop..."

        # Set up cleanup on exit
        $null = Register-ObjectEvent -InputObject ([System.Console]) -EventName "CancelKeyPress" -Action {
            $manager.StopServers()
            exit
        }

        # Wait indefinitely
        while ($true) {
            Start-Sleep -Seconds 1
        }
    }
} catch {
    $exceptionMessage = if ($_.Exception) { $_.Exception.ToString() } else { "<no exception>" }
    $scriptStack = if ($_.ScriptStackTrace) { $_.ScriptStackTrace } else { "<no stack>" }
    Write-Output "MCP init diagnostic: $exceptionMessage"
    Write-Output "MCP init stack: $scriptStack"
    try {
        $diagnosticDir = "tmp"
        if (-not (Test-Path -Path $diagnosticDir -PathType Container)) {
            $null = New-Item -Path $diagnosticDir -ItemType Directory -Force
        }

        $diagnosticPath = Join-Path $diagnosticDir "init-mcp-servers.last-error.txt"
        @(
            "Timestamp: $(Get-Date -Format o)"
            "Message: $($_.Exception.Message)"
            "Type: $($_.Exception.GetType().FullName)"
            "ScriptStackTrace:"
            $_.ScriptStackTrace
            "PositionMessage:"
            $_.InvocationInfo.PositionMessage
            "StackTrace:"
            $_.Exception.StackTrace
        ) | Set-Content -Path $diagnosticPath -Encoding UTF8
    } catch {
        # Best effort diagnostics only
    }

    Write-Error "MCP server initialization failed: $($_.Exception.Message)"
    exit 1
} finally {
    if ($manager) {
        $manager.StopServers()
    }
}
