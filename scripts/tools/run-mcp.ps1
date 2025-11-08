<#
Runs the workspace C# MCP tool in a safe, reproducible way.

Behavior:
- If a bundled binary exists at ./tools/mcp-csharp.exe it will be used.
- Otherwise the script prints installation instructions and exits 0 so CI/dev tasks don't fail unexpectedly.
- Designed to be safe for developer machines that haven't installed the tool yet.

Usage:
pwsh ./scripts/tools/run-mcp.ps1 -Project "WileyWidget.csproj" -ExtraArgs "--analyze"
#>
param(
    [string]$Project = "WileyWidget.csproj",
    [string]$ExtraArgs = ""
)

Set-StrictMode -Version Latest

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsExe = Join-Path $root "..\tools\mcp-csharp.exe" | Resolve-Path -ErrorAction SilentlyContinue

if ($toolsExe) {
    Write-Output "Using bundled MCP binary at $toolsExe"
    & $toolsExe --project (Resolve-Path $Project) $ExtraArgs
    exit $LASTEXITCODE
}

Write-Output "No bundled MCP binary found."
Write-Output "If you've published MCP as a local dotnet tool, run: dotnet tool restore"
Write-Output "To install MCP locally (recommended, pinned per-repo), run something like:"
Write-Output "  dotnet tool install --local mcp-csharp --version 1.2.3"
Write-Output "Or place a released binary at ./tools/mcp-csharp.exe and re-run this script."

# Exit successfully so CI or task runs don't fail when the tool is intentionally not installed yet.
exit 0

# NOTE: Docker-based runner moved out of this wrapper for simplicity.
# If you need a container-based MCP, create scripts/tools/run-mcp-docker.ps1 with
# the desired image and invocation. This wrapper focuses on running a local
# or bundled MCP binary and providing clear install instructions.
