<#
.SYNOPSIS
Test the csharp-mcp server via stdio to validate it's working.

.DESCRIPTION
Launches the csharp-mcp Docker container with interactive stdin,
sends a simple MCP initialize request, and captures the response.

.EXAMPLE
.\scripts\test-mcp-stdio.ps1
#>

Write-Output "Testing csharp-mcp server (stdio mode)..."

# Create a test JSON-RPC initialize message
$initMessage = @{
    jsonrpc = "2.0"
    id      = 1
    method  = "initialize"
    params  = @{
        protocolVersion = "2024-11-05"
        capabilities    = @{}
        clientInfo      = @{
            name    = "test-harness"
            version = "1.0"
        }
    }
} | ConvertTo-Json -Compress

Write-Output "Sending initialize request..."

# Run Docker with stdin and send the message
try {
    $response = $initMessage | docker run -i --rm `
        -v "${PWD}:/scripts:ro" `
        -e CSX_ALLOWED_PATH=/scripts `
        ghcr.io/infinityflowapp/csharp-mcp:latest 2>&1

    Write-Output "`nServer response:"
    Write-Output $response

    if ($response -match '"result"') {
        Write-Output "`n✅ MCP server responded successfully (stdio mode working)" -ForegroundColor Green
        exit 0
    }
    else {
        Write-Output "`n⚠️  Server responded but format unexpected" -ForegroundColor Yellow
        exit 1
    }
}
catch {
    Write-Output "`n❌ Failed to communicate with MCP server: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}
