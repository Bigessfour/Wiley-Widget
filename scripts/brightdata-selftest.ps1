param(
    [string]$Mode = 'HOSTED',  # Use HOSTED instead of local MCP
    [switch]$ConfigCheck = $false
)
Write-Host '== BrightData Diagnostics MCP Test =='

# Use machine-scoped or user-scoped environment variable
$machineApiKey = [System.Environment]::GetEnvironmentVariable('BRIGHTDATA_API_KEY', 'Machine')
$userApiKey = [System.Environment]::GetEnvironmentVariable('BRIGHTDATA_API_KEY', 'User')
$processApiKey = $env:BRIGHTDATA_API_KEY

if ($machineApiKey) {
    Write-Host "Using machine-scoped API key (length: $($machineApiKey.Length))"
    $env:BRIGHTDATA_API_KEY = $machineApiKey
} elseif ($userApiKey) {
    Write-Host "Using user-scoped API key (length: $($userApiKey.Length))"
    $env:BRIGHTDATA_API_KEY = $userApiKey
} elseif ($processApiKey -and $processApiKey -ne 'REPLACE_WITH_REAL_KEY') {
    Write-Host "Using process-scoped API key (length: $($processApiKey.Length))"
} else {
    Write-Host "No API key found in machine/user/process scope, using fallback"
    $env:BRIGHTDATA_API_KEY = 'DUMMY'
}

$env:BRIGHTDATA_MODE = $Mode
$env:BRIGHTDATA_MCP_DEBUG = '1'
$env:BRIGHTDATA_MCP_TIMEOUT = '15000'
$env:BRIGHTDATA_QUERIES = 'WPF application exits immediately'

# Add fallback to direct node execution if npx not available
$env:BRIGHTDATA_MCP_CMD = 'node'
$env:BRIGHTDATA_MCP_ARGS = '["node_modules/@brightdata/mcp/server.js"]'

Write-Host "Testing mode: $Mode"

if ($ConfigCheck) {
    Write-Host "Running MCP configuration check..."
    $env:BRIGHTDATA_CONFIG_CHECK = '1'
} else {
    Write-Host "Running normal diagnostics..."
}

node .\brightdata-startup-diagnostics.js
