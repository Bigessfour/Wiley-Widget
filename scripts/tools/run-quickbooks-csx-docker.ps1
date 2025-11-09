<#
.SYNOPSIS
Builds the CSX Docker image (using the workspace task) and runs the QuickBooks CSX script in a container
with QBO environment variables set so the container doesn't hit the missing-secret error.

.DESCRIPTION
This script invokes the `csx:build-image` task (via docker build) and then runs the `quickbooks-service.csx`
found under `tools/explore` inside the container. It passes QBO_CLIENT_ID and QBO_CLIENT_SECRET as env vars.

.PARAMETER ClientId
QBO client id to pass into the container. Default: test-client-id

.PARAMETER ClientSecret
QBO client secret to pass into the container. Default: test-client-secret

.PARAMETER ImageTag
Docker image tag to use. Default: wiley-widget/csx-mcp:local

.EXAMPLE
    pwsh -NoProfile -ExecutionPolicy Bypass -File .\run-quickbooks-csx-docker.ps1 -ClientId 'abc' -ClientSecret 'xyz'
#>

param(
    [string]$ClientId = "test-client-id",
    [string]$ClientSecret = "test-client-secret",
    [string]$ImageTag = "wiley-widget/csx-mcp:local"
)

function Write-Log { param($m) Write-Output "[run-quickbooks-csx] $m" }

try {
    # Use $PSScriptRoot which is the directory containing this script
    $scriptDir = $PSScriptRoot
    # The workspace root is two levels up from scripts/tools
    $rootResolved = Resolve-Path (Join-Path $scriptDir "..\..")
    $rootPath = $rootResolved.ProviderPath

    Write-Log "Workspace root: $rootPath"

    # Build image using docker (mirror of existing task behavior)
    Write-Log "Building Docker image: $ImageTag"
    docker build -t $ImageTag -f "$rootPath/docker/Dockerfile.csx-tests" $rootPath

    if ($LASTEXITCODE -ne 0) { throw "Docker build failed with exit code $LASTEXITCODE" }

    # Prepare log dir
    $logsDir = Join-Path $rootPath "logs"
    if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Path $logsDir -Force | Out-Null }

    # Compose run command. Mount workspace read-only at /app and logs as writable.
    $cmd = @(
        'run', '--rm', '-w', '/app',
        '-v', "${rootPath}:/app:ro",
        '-v', "${logsDir}:/logs:rw",
        '-e', 'WW_REPO_ROOT=/app',
        '-e', 'WW_LOGS_DIR=/logs',
        '-e', "QBO_CLIENT_ID=${ClientId}",
        '-e', "QBO_CLIENT_SECRET=${ClientSecret}",
        $ImageTag,
        'dotnet', 'script', '/app/tools/explore/quickbooks-service.csx'
    )

    Write-Log "Running container (this will stream logs)."
    docker @cmd
    $exit = $LASTEXITCODE
    Write-Log "Container exited with code $exit"
    exit $exit
} catch {
    Write-Error "Run failed: $_"
    exit 2
}
