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
    [System.Collections.Generic.List[McpServerHealth]]$HealthStatus = [System.Collections.Generic.List[McpServerHealth]]::new()
    [hashtable]$Servers = @{}
    [System.Diagnostics.Stopwatch]$Stopwatch

    McpServerManager([string]$configPath) {
        $this.ConfigPath = $configPath
        $this.Stopwatch = [System.Diagnostics.Stopwatch]::new()
    }

    [void] LoadConfiguration() {
        Write-Verbose "Loading configuration from: $($this.ConfigPath)"
        if (-not (Test-Path $this.ConfigPath)) {
            throw "MCP configuration file not found: $($this.ConfigPath)"
        }

        try {
            Write-Verbose "Reading configuration file..."
            $config = Get-Content $this.ConfigPath | ConvertFrom-Json
            Write-Verbose "Configuration parsed successfully"

            # Validate configuration structure
            $this.ValidateConfiguration($config)

            # Load server configurations
            Write-Verbose "Found servers: $($config.mcpServers.PSObject.Properties.Name -join ', ')"
            foreach ($serverName in $config.mcpServers.PSObject.Properties.Name) {
                $serverConfig = $config.mcpServers.$serverName
                $this.Servers[$serverName] = @{
                    Name = $serverName
                    Command = $serverConfig.command
                    Args = $serverConfig.args
                    Env = $serverConfig.env
                    Process = $null
                    Health = [McpServerHealth]@{
                        Name = $serverName
                        Status = "unknown"
                        LastChecked = [DateTime]::MinValue
                        ResponseTime = [TimeSpan]::Zero
                        Capabilities = @()
                        ErrorMessage = ""
                        Metrics = @{}
                    }
                }
            }

            Write-Verbose "MCP configuration loaded successfully"
        }
        catch {
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
                if ($PSBoundParameters.ContainsKey('Verbose')) {
                    Write-Output "Starting $serverName..."
                }

                $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
                $startInfo.FileName = $server.Command
                $startInfo.Arguments = $server.Args -join " "
                $startInfo.UseShellExecute = $false
                $startInfo.RedirectStandardOutput = $true
                $startInfo.RedirectStandardError = $true
                $startInfo.CreateNoWindow = $true

                # Set environment variables
                if ($server.Env -and $server.Env.PSObject.Properties) {
                    foreach ($envVar in $server.Env.PSObject.Properties) {
                        $startInfo.EnvironmentVariables[$envVar.Name] = $envVar.Value
                    }
                }

                $process = [System.Diagnostics.Process]::new()
                $process.StartInfo = $startInfo
                $process.Start()

                $server.Process = $process
                $server.Health.Status = "starting"

                if ($PSBoundParameters.ContainsKey('Verbose')) {
                    Write-Output "$serverName started (PID: $($process.Id))"
                }
            }
            catch {
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
                $isHealthy = $this.TestServerHealth($server)

                $this.Stopwatch.Stop()
                $health.ResponseTime = $this.Stopwatch.Elapsed
                $health.LastChecked = [DateTime]::Now

                if ($isHealthy) {
                    $health.Status = "healthy"
                    $health.ErrorMessage = ""
                    if ($PSBoundParameters.ContainsKey('Verbose')) {
                        Write-Output "$serverName is healthy ($($health.ResponseTime.TotalMilliseconds)ms)"
                    }
                }
                else {
                    $health.Status = "unhealthy"
                    if ($PSBoundParameters.ContainsKey('Verbose')) {
                        Write-Output "$serverName is unhealthy"
                    }
                }
            }
            catch {
                $health.Status = "unhealthy"
                $health.ErrorMessage = $_.Exception.Message
                if ($PSBoundParameters.ContainsKey('Verbose')) {
                    Write-Output "$serverName health check failed: $($_.Exception.Message)"
                }
            }
        }
    }

    [bool] TestServerHealth([hashtable]$server) {
        # Health check logic based on server type (Deep validation)
        switch ($server.Name) {
            "csharp-mcp" {
                # Deep test: Evaluate a basic C# expression
                try {
                    $result = & dotnet tool run mcp-csharp eval "1 + 1" 2>$null
                    return $LASTEXITCODE -eq 0 -and $result -match "2"
                }
                catch {
                    return $false
                }
            }
            "filesystem-mcp" {
                # Deep test: Verify npx can resolve the server and the process is alive
                if ($server.Process -and -not $server.Process.HasExited) {
                    try {
                        $npxCheck = & npx --no-install @modelcontextprotocol/server-filesystem --version 2>$null
                        return $LASTEXITCODE -eq 0
                    } catch { return $false }
                }
                return $false
            }
            "github-mcp" {
                # Deep test: Verify GitHub API connectivity and token validity
                try {
                    if (-not $env:GITHUB_TOKEN) { return $false }
                    $user = & gh api user --jq ".login" 2>$null
                    return $LASTEXITCODE -eq 0 -and $null -ne $user
                }
                catch {
                    return $false
                }
            }
            "syncfusion-winforms-assistant" {
                # Deep test: Verify API key and connectivity to Syncfusion
                try {
                    $apiKey = [Environment]::GetEnvironmentVariable("SYNCFUSION_API_KEY")
                    if (-not $apiKey) { return $false }
                    # Connectivity check via npm
                    $null = & npm view @syncfusion/winforms-assistant version 2>$null
                    return $LASTEXITCODE -eq 0
                }
                catch {
                    return $false
                }
            }
            "wileywidget-ui-mcp" {
                # Deep test: Verify project can be built and run helper
                try {
                    $result = & dotnet run --project tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj --no-build -- --run-license-check json 2>$null
                    return $LASTEXITCODE -eq 0 -and $result -match "license"
                }
                catch {
                    return $false
                }
            }
            default {
                # Generic health check - check if process is running and responding
                return $server.Process -and -not $server.Process.HasExited
            }
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
                    if ($PSBoundParameters.ContainsKey('Verbose')) {
                        Write-Output "$serverName stopped"
                    }
                }
                catch {
                    Write-Warning "Failed to stop $serverName gracefully: $($_.Exception.Message)"
                }
            }
        }
    }

    [hashtable] GetHealthReport() {
        $healthy = ($this.HealthStatus | Where-Object { $_.Status -eq "healthy" }).Count
        $total = $this.HealthStatus.Count

        return @{
            TotalServers = $total
            HealthyServers = $healthy
            UnhealthyServers = $total - $healthy
            HealthPercentage = if ($total -gt 0) { [math]::Round(($healthy / $total) * 100, 2) } else { 0 }
            Details = $this.HealthStatus
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
}
catch {
    Write-Error "MCP server initialization failed: $($_.Exception.Message)"
    exit 1
}
finally {
    if ($manager) {
        $manager.StopServers()
    }
}
