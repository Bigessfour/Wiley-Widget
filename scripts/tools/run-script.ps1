<#
.SYNOPSIS
    Intelligent script runner that automatically detects file type and routes to appropriate interpreter.

.DESCRIPTION
    Universal script execution wrapper that:
    - Detects script type by extension (.py, .ps1, .csx, .cs)
    - Finds and validates the appropriate interpreter (Python 3.11, PowerShell 7.5+, dotnet script)
    - Sets up the correct environment variables and paths
    - Provides consistent error handling and logging
    - Supports both interactive and background execution

.PARAMETER ScriptPath
    Path to the script file to execute. Can be relative or absolute.

.PARAMETER Arguments
    Optional arguments to pass to the script.

.PARAMETER Background
    If specified, runs the script in the background without waiting for completion.

.PARAMETER Verbose
    Enables verbose output for debugging.

.EXAMPLE
    .\run-script.ps1 -ScriptPath "tests\test_startup_validator.py"

.EXAMPLE
    .\run-script.ps1 -ScriptPath "scripts\maintenance\cleanup-dotnet-processes.ps1" -Verbose

.EXAMPLE
    .\run-script.ps1 -ScriptPath "scripts\examples\csharp\sample-test.csx" -Arguments "--verbose"

.NOTES
    Author: Wiley Widget Development Team
    Date: 2025-11-11
    Supports: Python 3.11+, PowerShell 7.5+, .NET 9.0 C# scripts
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$ScriptPath,

    [Parameter(Mandatory = $false, Position = 1)]
    [string[]]$Arguments = @(),

    [Parameter(Mandatory = $false)]
    [switch]$Background,

    [Parameter(Mandatory = $false)]
    [switch]$ShowInterpreterInfo
)

$ErrorActionPreference = "Stop"
$VerbosePreference = if ($PSCmdlet.MyInvocation.BoundParameters["Verbose"]) { "Continue" } else { "SilentlyContinue" }

# Script metadata
$ScriptVersion = "1.0.0"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

Write-Verbose "========================================"
Write-Verbose "Intelligent Script Runner v$ScriptVersion"
Write-Verbose "========================================"
Write-Verbose "Repo Root: $RepoRoot"

#region Helper Functions

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$ForegroundColor = "White"
    )

    $originalColor = $Host.UI.RawUI.ForegroundColor
    $Host.UI.RawUI.ForegroundColor = $ForegroundColor
    Write-Host $Message
    $Host.UI.RawUI.ForegroundColor = $originalColor
}

function Get-PythonExecutable {
    <#
    .SYNOPSIS
        Finds Python 3.11+ executable with priority order.
    #>

    Write-Verbose "Searching for Python 3.11+ executable..."

    # Priority 1: Python 3.11 from Windows Store
    $candidates = @(
        "C:\Users\$env:USERNAME\AppData\Local\Microsoft\WindowsApps\python3.11.exe",
        "C:\Users\$env:USERNAME\AppData\Local\Microsoft\WindowsApps\python.exe",
        "C:\Python311\python.exe",
        "C:\Program Files\Python311\python.exe",
        "python3.11",
        "python3",
        "python"
    )

    foreach ($candidate in $candidates) {
        try {
            $pythonPath = if (Test-Path $candidate -ErrorAction SilentlyContinue) {
                $candidate
            } else {
                (Get-Command $candidate -ErrorAction SilentlyContinue).Source
            }

            if ($pythonPath) {
                # Verify version
                $versionOutput = & $pythonPath --version 2>&1
                if ($versionOutput -match "Python (\d+)\.(\d+)\.(\d+)") {
                    $major = [int]$matches[1]
                    $minor = [int]$matches[2]

                    if ($major -eq 3 -and $minor -ge 11) {
                        Write-Verbose "✓ Found Python $major.$minor at: $pythonPath"
                        return $pythonPath
                    } else {
                        Write-Verbose "  Skipping Python $major.$minor (requires 3.11+)"
                    }
                }
            }
        } catch {
            Write-Verbose "  Candidate failed: $candidate"
        }
    }

    throw "Python 3.11+ not found. Install from: https://www.python.org/downloads/ or Microsoft Store"
}

function Get-PowerShellExecutable {
    <#
    .SYNOPSIS
        Finds PowerShell 7.5+ executable.
    #>

    Write-Verbose "Searching for PowerShell 7.5+ executable..."

    $candidates = @(
        "C:\Program Files\PowerShell\7\pwsh.exe",
        "C:\Program Files\PowerShell\7-preview\pwsh.exe",
        "pwsh"
    )

    foreach ($candidate in $candidates) {
        try {
            $pwshPath = if (Test-Path $candidate -ErrorAction SilentlyContinue) {
                $candidate
            } else {
                (Get-Command $candidate -ErrorAction SilentlyContinue).Source
            }

            if ($pwshPath) {
                $versionOutput = & $pwshPath -NoProfile -Command '$PSVersionTable.PSVersion.ToString()' 2>&1
                if ($versionOutput -match "(\d+)\.(\d+)\.(\d+)") {
                    $major = [int]$matches[1]
                    $minor = [int]$matches[2]

                    if ($major -ge 7 -and $minor -ge 5) {
                        Write-Verbose "✓ Found PowerShell $major.$minor at: $pwshPath"
                        return $pwshPath
                    } else {
                        Write-Verbose "  Skipping PowerShell $major.$minor (requires 7.5+)"
                    }
                }
            }
        } catch {
            Write-Verbose "  Candidate failed: $candidate"
        }
    }

    # Fallback to current PowerShell
    Write-Warning "PowerShell 7.5+ not found, using current session: $PSHOME\pwsh.exe"
    return "$PSHOME\pwsh.exe"
}

function Get-DotNetScriptExecutable {
    <#
    .SYNOPSIS
        Finds dotnet-script or fallback to docker CSX runner.
    #>

    Write-Verbose "Searching for C# script executor..."

    # Check for dotnet-script global tool
    try {
        $dotnetScriptPath = (Get-Command dotnet-script -ErrorAction SilentlyContinue).Source
        if ($dotnetScriptPath) {
            Write-Verbose "✓ Found dotnet-script: $dotnetScriptPath"
            return @{
                Type = "dotnet-script"
                Path = $dotnetScriptPath
            }
        }
    } catch {
        Write-Verbose "  dotnet-script not found"
    }

    # Check for Docker (fallback for CSX tests)
    try {
        $dockerPath = (Get-Command docker -ErrorAction SilentlyContinue).Source
        if ($dockerPath) {
            Write-Verbose "✓ Docker available for CSX execution"
            return @{
                Type = "docker"
                Path = $dockerPath
            }
        }
    } catch {
        Write-Verbose "  Docker not found"
    }

    throw "No C# script executor found. Install dotnet-script: dotnet tool install -g dotnet-script"
}

function Resolve-ScriptPath {
    param([string]$Path)

    # If absolute path exists, use it
    if ([System.IO.Path]::IsPathRooted($Path) -and (Test-Path $Path)) {
        return (Resolve-Path $Path).Path
    }

    # Try relative to repo root
    $repoPath = Join-Path $RepoRoot $Path
    if (Test-Path $repoPath) {
        return (Resolve-Path $repoPath).Path
    }

    # Try relative to current directory
    if (Test-Path $Path) {
        return (Resolve-Path $Path).Path
    }

    throw "Script not found: $Path (searched: absolute, repo root, current directory)"
}

#endregion

#region Main Execution

try {
    # Resolve script path
    $resolvedScript = Resolve-ScriptPath -Path $ScriptPath
    $extension = [System.IO.Path]::GetExtension($resolvedScript).ToLower()
    $scriptName = [System.IO.Path]::GetFileName($resolvedScript)

    Write-ColorOutput "🚀 Executing: $scriptName" -ForegroundColor Cyan
    Write-Verbose "   Full path: $resolvedScript"
    Write-Verbose "   Extension: $extension"

    # Determine execution strategy based on file extension
    switch ($extension) {
        ".py" {
            Write-Verbose "Detected Python script"
            $pythonExe = Get-PythonExecutable

            if ($ShowInterpreterInfo) {
                Write-ColorOutput "🐍 Python Interpreter Info:" -ForegroundColor Green
                & $pythonExe --version
                & $pythonExe -c "import sys; print(f'Executable: {sys.executable}')"
                & $pythonExe -c "import sys; print(f'Path: {sys.path[0]}')"
            }

            # Set Python environment
            $env:PYTHONPATH = $RepoRoot
            $env:PYTHONUNBUFFERED = "1"  # Disable output buffering

            Write-Verbose "Environment: PYTHONPATH=$env:PYTHONPATH"

            # Execute Python script
            $arguments = @($resolvedScript) + $Arguments

            if ($Background) {
                Write-ColorOutput "⏳ Running in background..." -ForegroundColor Yellow
                $job = Start-Job -ScriptBlock {
                    param($exe, $args)
                    & $exe @args
                } -ArgumentList $pythonExe, $arguments

                Write-ColorOutput "✓ Background job started (ID: $($job.Id))" -ForegroundColor Green
                return $job
            } else {
                & $pythonExe @arguments
                $exitCode = $LASTEXITCODE

                if ($exitCode -eq 0) {
                    Write-ColorOutput "✓ Script completed successfully" -ForegroundColor Green
                } else {
                    Write-ColorOutput "✗ Script failed with exit code: $exitCode" -ForegroundColor Red
                    exit $exitCode
                }
            }
        }

        ".ps1" {
            Write-Verbose "Detected PowerShell script"
            $pwshExe = Get-PowerShellExecutable

            if ($ShowInterpreterInfo) {
                Write-ColorOutput "⚡ PowerShell Interpreter Info:" -ForegroundColor Green
                & $pwshExe -NoProfile -Command '$PSVersionTable'
            }

            # Execute PowerShell script
            $pwshArgs = @(
                "-NoProfile",
                "-ExecutionPolicy", "Bypass",
                "-File", $resolvedScript
            ) + $Arguments

            if ($Background) {
                Write-ColorOutput "⏳ Running in background..." -ForegroundColor Yellow
                $job = Start-Job -ScriptBlock {
                    param($exe, $args)
                    & $exe @args
                } -ArgumentList $pwshExe, $pwshArgs

                Write-ColorOutput "✓ Background job started (ID: $($job.Id))" -ForegroundColor Green
                return $job
            } else {
                & $pwshExe @pwshArgs
                $exitCode = $LASTEXITCODE

                if ($exitCode -eq 0) {
                    Write-ColorOutput "✓ Script completed successfully" -ForegroundColor Green
                } else {
                    Write-ColorOutput "✗ Script failed with exit code: $exitCode" -ForegroundColor Red
                    exit $exitCode
                }
            }
        }

        { $_ -in ".csx", ".cs" } {
            Write-Verbose "Detected C# script"
            $csxExecutor = Get-DotNetScriptExecutable

            if ($csxExecutor.Type -eq "dotnet-script") {
                if ($ShowInterpreterInfo) {
                    Write-ColorOutput "🔷 C# Script Executor Info:" -ForegroundColor Green
                    & $csxExecutor.Path --version
                }

                # Execute with dotnet-script
                $csxArgs = @($resolvedScript) + $Arguments

                if ($Background) {
                    Write-ColorOutput "⏳ Running in background..." -ForegroundColor Yellow
                    $job = Start-Job -ScriptBlock {
                        param($exe, $args)
                        & $exe @args
                    } -ArgumentList $csxExecutor.Path, $csxArgs

                    Write-ColorOutput "✓ Background job started (ID: $($job.Id))" -ForegroundColor Green
                    return $job
                } else {
                    & $csxExecutor.Path @csxArgs
                    $exitCode = $LASTEXITCODE

                    if ($exitCode -eq 0) {
                        Write-ColorOutput "✓ Script completed successfully" -ForegroundColor Green
                    } else {
                        Write-ColorOutput "✗ Script failed with exit code: $exitCode" -ForegroundColor Red
                        exit $exitCode
                    }
                }
            } elseif ($csxExecutor.Type -eq "docker") {
                Write-ColorOutput "🐳 Using Docker for CSX execution (dotnet-script not installed)" -ForegroundColor Yellow

                # Build Docker image if needed
                $dockerfilePath = Join-Path $RepoRoot "docker\Dockerfile.csx-tests"
                if (-not (Test-Path $dockerfilePath)) {
                    throw "Docker fallback requires: docker/Dockerfile.csx-tests"
                }

                # Check if image exists
                $imageExists = docker images -q "wiley-widget/csx-mcp:local" 2>$null
                if (-not $imageExists) {
                    Write-ColorOutput "📦 Building Docker image (first time only)..." -ForegroundColor Yellow
                    docker build -t "wiley-widget/csx-mcp:local" -f $dockerfilePath $RepoRoot
                }

                # Run in Docker
                $dockerArgs = @(
                    "run", "--rm",
                    "-v", "$($RepoRoot):/app:ro",
                    "-v", "$($RepoRoot)\logs:/logs:rw",
                    "-e", "WW_REPO_ROOT=/app",
                    "-e", "WW_LOGS_DIR=/logs",
                    "wiley-widget/csx-mcp:local",
                    $resolvedScript.Replace($RepoRoot, "/app").Replace("\", "/")
                ) + $Arguments

                & docker @dockerArgs
                $exitCode = $LASTEXITCODE

                if ($exitCode -eq 0) {
                    Write-ColorOutput "✓ Script completed successfully" -ForegroundColor Green
                } else {
                    Write-ColorOutput "✗ Script failed with exit code: $exitCode" -ForegroundColor Red
                    exit $exitCode
                }
            }
        }

        default {
            throw "Unsupported script type: $extension (supported: .py, .ps1, .csx, .cs)"
        }
    }

} catch {
    Write-ColorOutput "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Verbose "Stack trace: $($_.ScriptStackTrace)"
    exit 1
}

#endregion
