<#
.SYNOPSIS
    Report GitHub Actions run metadata to an MCP-compatible endpoint (optional, safe/opt-in).

.DESCRIPTION
    This small helper posts a compact JSON summary for a workflow run to an MCP-style
    endpoint so a local or remote MCP server (like the Wiley GitHub MCP) can collect and
    surface CI insights. The script is purposely tolerant when the endpoint/token are not
    configured so workflows using this remain portable.

.PARAMETER Endpoint
    The full HTTP(S) URL of the MCP endpoint (e.g. https://mcp.example.com/actions/report).
    If omitted, the script exits with a 0 code to avoid failing CI.

.PARAMETER Token
    Optional bearer token used to authenticate against the endpoint. If provided, the
    Authorization header will be set to "Bearer <token>".

.PARAMETER PayloadFile
    Optional path to a JSON file to send. If omitted, the script will construct a small
    JSON payload from environment variables populated by GitHub Actions.

.EXAMPLE
    # Use environment secrets in actions:
    .\report-to-mcp.ps1 -Endpoint $env:MCP_ENDPOINT -Token $env:MCP_TOKEN
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory=$false)] [string]$Endpoint,
    [Parameter(Mandatory=$false)] [string]$Token,
    [Parameter(Mandatory=$false)] [string]$PayloadFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Endpoint) {
    Write-Host "MCP endpoint not set — skipping MCP reporting (this is expected in many environments)."
    exit 0
}

if ($PayloadFile -and (Test-Path $PayloadFile)) {
    $json = Get-Content -Raw -Path $PayloadFile
} else {
    # Construct a compact payload using common GitHub Actions environment variables
    $payload = [ordered]@{
        repository   = $env:GITHUB_REPOSITORY
        run_id       = $env:GITHUB_RUN_ID
        run_number   = $env:GITHUB_RUN_NUMBER
        run_url      = $env:GITHUB_SERVER_URL.TrimEnd('/') + "/" + $env:GITHUB_REPOSITORY + "/actions/runs/" + $env:GITHUB_RUN_ID
        event_name   = $env:GITHUB_EVENT_NAME
        workflow     = $env:GITHUB_WORKFLOW
        ref          = $env:GITHUB_REF
        sha          = $env:GITHUB_SHA
        actor        = $env:GITHUB_ACTOR
        status       = $env:CI_JOB_STATUS  # CI_JOB_STATUS may be empty — that's OK
        timestamp    = (Get-Date).ToString('o')
    }

    $json = $payload | ConvertTo-Json -Depth 5
}

Write-Host "Posting CI summary to MCP endpoint: $Endpoint"

$headers = @{}
if ($Token) { $headers['Authorization'] = "Bearer $Token" }

try {
    $result = Invoke-RestMethod -Uri $Endpoint -Method Post -Body $json -ContentType 'application/json' -Headers $headers -TimeoutSec 30
    Write-Host "MCP reported successfully. Server response:"
    Write-Host ($result | ConvertTo-Json -Depth 3)
    exit 0
} catch {
    Write-Warning "Failed to report to MCP endpoint: $($_.Exception.Message)"
    # Non-fatal: allow CI to continue if the reporting endpoint is unreachable
    exit 0
}
