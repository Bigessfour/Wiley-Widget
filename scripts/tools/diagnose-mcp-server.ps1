<#
.SYNOPSIS
    Diagnose and troubleshoot C# MCP Server Docker configuration for Wiley Widget.

.DESCRIPTION
    Comprehensive diagnostic script that:
    - Checks Docker installation and status
    - Verifies MCP server image availability
    - Tests container startup and health
    - Validates VS Code MCP configuration
    - Tests stdio communication
    - Provides step-by-step fixes

.EXAMPLE
    .\scripts\diagnose-mcp-server.ps1
    .\scripts\diagnose-mcp-server.ps1 -Verbose
    .\scripts\diagnose-mcp-server.ps1 -FixIssues

.NOTES
    Designed for Wiley Widget's Prism-based WPF application with .csx E2E tests.
#>

[CmdletBinding()]
param(
    [switch]$FixIssues,
    [switch]$TestOnly,
    [int]$Timeout = 30
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

#region Helper Functions

function Write-DiagnosticHeader {
    param([string]$Title)
    Write-Host "`n═══════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host " $Title" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════`n" -ForegroundColor Cyan
}

function Write-Check {
    param([string]$Message, [bool]$Success, [string]$Details = "")

    if ($Success) {
        Write-Host "✓ " -NoNewline -ForegroundColor Green
        Write-Host $Message -ForegroundColor White
        if ($Details) { Write-Host "  └─ $Details" -ForegroundColor Gray }
    } else {
        Write-Host "✗ " -NoNewline -ForegroundColor Red
        Write-Host $Message -ForegroundColor White
        if ($Details) { Write-Host "  └─ $Details" -ForegroundColor Yellow }
    }
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ $Message" -ForegroundColor Cyan
}

function Write-Fix {
    param([string]$Message)
    Write-Host "🔧 FIX: $Message" -ForegroundColor Yellow
}

#endregion

#region Diagnostic Checks

function Test-DockerInstalled {
    Write-DiagnosticHeader "Docker Installation Check"

    try {
        $dockerVersion = docker --version 2>&1
        Write-Check "Docker is installed" $true $dockerVersion

        $dockerInfo = docker info 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Check "Docker daemon is running" $true
            return $true
        } else {
            Write-Check "Docker daemon is NOT running" $false "Start Docker Desktop"
            return $false
        }
    } catch {
        Write-Check "Docker is NOT installed" $false "Install Docker Desktop"
        return $false
    }
}

function Test-McpImageAvailable {
    Write-DiagnosticHeader "MCP Server Image Check"

    $imageName = "ghcr.io/infinityflowapp/csharp-mcp:latest"

    try {
        $images = docker images $imageName --format "{{.Repository}}:{{.Tag}}" 2>&1

        if ($images -match "csharp-mcp") {
            Write-Check "MCP image is available locally" $true $imageName

            # Check image details
            $imageInfo = docker inspect $imageName 2>&1 | ConvertFrom-Json
            $created = $imageInfo[0].Created
            Write-Info "Image created: $created"

            return $true
        } else {
            Write-Check "MCP image NOT found locally" $false
            Write-Fix "Pull the image: docker pull $imageName"

            if ($FixIssues) {
                Write-Host "`nPulling MCP image..." -ForegroundColor Yellow
                docker pull $imageName

                if ($LASTEXITCODE -eq 0) {
                    Write-Check "Image pulled successfully" $true
                    return $true
                }
            }

            return $false
        }
    } catch {
        Write-Check "Error checking MCP image" $false $_.Exception.Message
        return $false
    }
}

function Test-McpContainerRunning {
    Write-DiagnosticHeader "MCP Container Status Check"

    try {
        $containers = docker ps -a --filter "name=csharp-mcp" --format "{{.Names}}|{{.Status}}|{{.Ports}}" 2>&1

        if ($containers) {
            $containerData = $containers -split '\|'
            $name = $containerData[0]
            $status = $containerData[1]
            $ports = $containerData[2]

            if ($status -match "Up") {
                Write-Check "MCP container is running" $true "$name - $status"
                if ($ports) {
                    Write-Info "Ports: $ports"
                } else {
                    Write-Check "No ports exposed" $false "Container may not be accessible"
                }
                return $true
            } else {
                Write-Check "MCP container exists but not running" $false "$status"
                Write-Fix "Start container: docker start csharp-mcp"

                # Check logs for exit reason
                $logs = docker logs csharp-mcp --tail 50 2>&1
                Write-Host "`nLast 50 lines of container logs:" -ForegroundColor Yellow
                Write-Host $logs -ForegroundColor Gray

                return $false
            }
        } else {
            Write-Check "No MCP container found" $false "Container needs to be created"
            return $false
        }
    } catch {
        Write-Check "Error checking container status" $false $_.Exception.Message
        return $false
    }
}

function Test-VsCodeConfiguration {
    Write-DiagnosticHeader "VS Code MCP Configuration Check"

    $workspaceRoot = $PWD.Path
    $mcpJsonPath = Join-Path $workspaceRoot ".vscode\mcp.json"
    $settingsJsonPath = Join-Path $workspaceRoot ".vscode\settings.json"

    # Check mcp.json
    if (Test-Path $mcpJsonPath) {
        Write-Check "mcp.json exists" $true $mcpJsonPath

        try {
            $mcpConfig = Get-Content $mcpJsonPath -Raw | ConvertFrom-Json

            if ($mcpConfig.servers.'csharp-mcp') {
                Write-Check "csharp-mcp server configured" $true

                $serverConfig = $mcpConfig.servers.'csharp-mcp'
                Write-Info "Command: $($serverConfig.command)"
                Write-Info "Args: $($serverConfig.args -join ' ')"

                return $true
            } else {
                Write-Check "csharp-mcp server NOT configured in mcp.json" $false
                return $false
            }
        } catch {
            Write-Check "Invalid mcp.json format" $false $_.Exception.Message
            return $false
        }
    } else {
        Write-Check "mcp.json NOT found" $false "File doesn't exist"
        Write-Fix "Create .vscode/mcp.json with MCP server configuration"

        if ($FixIssues) {
            Write-Host "`nCreating mcp.json..." -ForegroundColor Yellow
            # This will be handled by the main script
            return $false
        }

        return $false
    }
}

function Test-McpServerStdio {
    Write-DiagnosticHeader "MCP Server stdio Communication Test"

    $workspaceRoot = $PWD.Path
    $imageName = "ghcr.io/infinityflowapp/csharp-mcp:latest"

    Write-Info "Testing stdio communication with MCP server..."

    try {
        # Create test JSON-RPC initialize request
        $initRequest = @{
            jsonrpc = "2.0"
            id = 1
            method = "initialize"
            params = @{
                protocolVersion = "2024-11-05"
                capabilities = @{}
                clientInfo = @{
                    name = "diagnostic-test"
                    version = "1.0"
                }
            }
        } | ConvertTo-Json -Depth 10 -Compress

        Write-Verbose "Sending initialize request: $initRequest"

        # Start container in interactive mode and send request
        # Capture stdout and stderr separately
        # Use a different name to avoid conflicts with VS Code's container
        $tempFile = [System.IO.Path]::GetTempFileName()
        $response = $initRequest | docker run -i --rm `
            --name "csharp-mcp-diagnostic-test" `
            -v "${workspaceRoot}:/scripts:ro" `
            -e CSX_ALLOWED_PATH=/scripts `
            -e WW_REPO_ROOT=/scripts `
            -e WW_LOGS_DIR=/scripts/logs `
            -e ASPNETCORE_ENVIRONMENT=Production `
            -e Logging__LogLevel__Default=Warning `
            $imageName 2>$tempFile

        $stderr = Get-Content $tempFile -Raw -ErrorAction SilentlyContinue
        Remove-Item $tempFile -Force -ErrorAction SilentlyContinue

        Write-Verbose "Raw response: $response"
        Write-Verbose "Stderr: $stderr"

        # Look for JSON-RPC response in stdout
        $jsonLine = $response -split "`n" | Where-Object { $_ -match '^\s*\{.*"jsonrpc".*\}' } | Select-Object -First 1

        if ($jsonLine) {
            Write-Check "MCP server responds to stdio requests" $true "Received JSON-RPC response"

            try {
                $jsonResponse = $jsonLine | ConvertFrom-Json
                if ($jsonResponse.result) {
                    Write-Info "Server capabilities: $($jsonResponse.result.capabilities | ConvertTo-Json -Compress)"
                    if ($jsonResponse.result.serverInfo) {
                        Write-Info "Server info: $($jsonResponse.result.serverInfo.name) v$($jsonResponse.result.serverInfo.version)"
                    }
                }
            } catch {
                Write-Verbose "Could not parse response as JSON: $_"
            }

            return $true
        } else {
            Write-Check "MCP server did NOT respond properly" $false "No JSON-RPC response received"
            Write-Host "`nStdout:" -ForegroundColor Yellow
            Write-Host $response -ForegroundColor Gray
            if ($stderr) {
                Write-Host "`nStderr (logs):" -ForegroundColor Yellow
                Write-Host $stderr -ForegroundColor Gray
            }
            return $false
        }
    } catch {
        Write-Check "Error testing stdio communication" $false $_.Exception.Message
        return $false
    }
}

function Test-CsxExecution {
    Write-DiagnosticHeader "C# Script (.csx) Execution Test"

    $workspaceRoot = $PWD.Path
    $testScript = Join-Path $workspaceRoot "scripts\examples\csharp\01-basic-test.csx"

    if (-not (Test-Path $testScript)) {
        Write-Check "Test script NOT found" $false $testScript
        return $false
    }

    Write-Check "Test script exists" $true $testScript

    try {
        # Test using dotnet-script via Docker
        $imageName = "wiley-widget/csx-mcp:local"

        # Check if local csx-tests image exists
        $localImage = docker images $imageName --format "{{.Repository}}:{{.Tag}}" 2>&1

        if (-not ($localImage -match "csx-mcp")) {
            Write-Check "Local csx-tests image NOT built" $false
            Write-Fix "Build image: docker build -t $imageName -f docker/Dockerfile.csx-tests ."
            return $false
        }

        Write-Check "Local csx-tests image available" $true

        Write-Info "Executing 01-basic-test.csx..."

        $output = docker run --rm `
            -v "${workspaceRoot}/scripts/examples/csharp:/app:ro" `
            -v "${workspaceRoot}/logs:/logs" `
            -e WW_REPO_ROOT=/app `
            -e WW_LOGS_DIR=/logs `
            $imageName 01-basic-test.csx 2>&1

        Write-Host "`nScript output:" -ForegroundColor Yellow
        Write-Host $output -ForegroundColor Gray

        if ($LASTEXITCODE -eq 0) {
            Write-Check "Script executed successfully" $true
            return $true
        } else {
            Write-Check "Script execution FAILED" $false "Exit code: $LASTEXITCODE"
            return $false
        }
    } catch {
        Write-Check "Error executing script" $false $_.Exception.Message
        return $false
    }
}

#endregion

#region Main Execution

function Invoke-CompleteDiagnostic {
    Write-Host @"
╔════════════════════════════════════════════════════════════════════════════╗
║                                                                            ║
║              Wiley Widget - C# MCP Server Diagnostic Tool                 ║
║                                                                            ║
║  Comprehensive diagnostics for Docker-based MCP server integration        ║
║  Used for E2E testing of Prism WPF modules with .csx scripts             ║
║                                                                            ║
╚════════════════════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

    $results = @{}

    # Run all diagnostic checks
    $results['Docker'] = Test-DockerInstalled

    if ($results['Docker']) {
        $results['Image'] = Test-McpImageAvailable
        $results['Container'] = Test-McpContainerRunning
        $results['VsCode'] = Test-VsCodeConfiguration
        $results['Stdio'] = Test-McpServerStdio

        if (-not $TestOnly) {
            $results['CsxExecution'] = Test-CsxExecution
        }
    }

    # Summary
    Write-DiagnosticHeader "Diagnostic Summary"

    $passCount = ($results.Values | Where-Object { $_ -eq $true }).Count
    $totalCount = $results.Count

    Write-Host "`nResults: $passCount/$totalCount checks passed`n" -ForegroundColor $(if ($passCount -eq $totalCount) { 'Green' } else { 'Yellow' })

    foreach ($check in $results.GetEnumerator()) {
        $icon = if ($check.Value) { "✓" } else { "✗" }
        $color = if ($check.Value) { "Green" } else { "Red" }
        Write-Host "$icon $($check.Key)" -ForegroundColor $color
    }

    # Recommendations
    if ($passCount -lt $totalCount) {
        Write-Host "`n" -NoNewline
        Write-DiagnosticHeader "Recommended Actions"

        if (-not $results['Docker']) {
            Write-Fix "Install Docker Desktop from https://www.docker.com/products/docker-desktop/"
        }

        if ($results['Docker'] -and -not $results['Image']) {
            Write-Fix "Pull MCP image: docker pull ghcr.io/infinityflowapp/csharp-mcp:latest"
        }

        if (-not $results['VsCode']) {
            Write-Fix "Run with -FixIssues flag to create VS Code MCP configuration"
        }

        if (-not $results['Stdio']) {
            Write-Fix "Check Docker logs: docker logs csharp-mcp"
            Write-Fix "Verify port bindings and environment variables"
        }

        if (-not $results['CsxExecution']) {
            Write-Fix "Build local csx-tests image: docker build -t wiley-widget/csx-mcp:local -f docker/Dockerfile.csx-tests ."
        }

        Write-Host ""
    } else {
        Write-Host "`n✓ All checks passed! MCP server is properly configured.`n" -ForegroundColor Green
    }

    return $results
}

# Execute diagnostics
$diagnosticResults = Invoke-CompleteDiagnostic

# Exit with appropriate code
$allPassed = ($diagnosticResults.Values | Where-Object { $_ -eq $false }).Count -eq 0
exit $(if ($allPassed) { 0 } else { 1 })

#endregion
