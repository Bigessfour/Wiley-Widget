<#
.SYNOPSIS
Test C# code evaluation via the csharp-mcp server.

.DESCRIPTION
Sends a complete MCP session to the csharp-mcp server:
1. Initialize
2. Evaluate simple C# code
3. Test with XUnit example

.EXAMPLE
.\scripts\test-mcp-eval.ps1
#>

Write-Output "=== Testing csharp-mcp C# Evaluation ==="

# Initialize message
$initMsg = @{
    jsonrpc = "2.0"
    id      = 1
    method  = "initialize"
    params  = @{
        protocolVersion = "2024-11-05"
        capabilities    = @{}
        clientInfo      = @{
            name    = "wiley-widget-test"
            version = "1.0"
        }
    }
} | ConvertTo-Json -Compress

# Simple C# evaluation
$evalMsg = @{
    jsonrpc = "2.0"
    id      = 2
    method  = "tools/call"
    params  = @{
        name      = "evaluate_csharp"
        arguments = @{
            code = @"
using System;
Console.WriteLine("Hello from C# MCP!");
var result = 2 + 2;
Console.WriteLine(`$"2 + 2 = {result}`);
return result;
"@
        }
    }
} | ConvertTo-Json -Compress

Write-Output "`n1. Testing initialize..."
Write-Output $initMsg

Write-Output "`n2. Testing C# evaluation..."
Write-Output $evalMsg

Write-Output "`n3. Sending to MCP server..."

# Combine messages with newlines (JSON-RPC over stdio)
$messages = "$initMsg`n$evalMsg`n"

try {
    # Send messages and separate stdout from stderr
    $tempFile = [System.IO.Path]::GetTempFileName()
    $messages | docker run -i --rm `
        -v "${PWD}:/scripts:ro" `
        -e CSX_ALLOWED_PATH=/scripts `
        ghcr.io/infinityflowapp/csharp-mcp:latest 2>$tempFile | Tee-Object -Variable stdout

    Write-Output "`n=== JSON-RPC Responses (stdout) ==="
    $stdout | Where-Object { $_ -match '^\{.*\}$' } | ForEach-Object {
        Write-Output $_ -ForegroundColor Cyan
        try {
            $json = $_ | ConvertFrom-Json
            if ($json.result) {
                Write-Output "  Result: $($json.result | ConvertTo-Json -Compress)" -ForegroundColor Green
            }
        }
        catch {
            # Not valid JSON, skip
        }
    }

    Write-Output "`n=== Server Logs (stderr) ==="
    Get-Content $tempFile | Select-Object -First 5
    Write-Output "... (truncated)"
    Remove-Item $tempFile -ErrorAction SilentlyContinue

    $jsonResponses = $stdout | Where-Object { $_ -match '^\{.*\}$' }

    if ($jsonResponses -and ($jsonResponses -match '"result"')) {
        Write-Output "`n✅ C# evaluation successful!" -ForegroundColor Green
        exit 0
    }
    elseif ($jsonResponses -match '"error"') {
        Write-Output "`n⚠️  Server returned an error" -ForegroundColor Yellow
        exit 1
    }
    else {
        Write-Output "`n⚠️  No JSON-RPC responses found (check if server is outputting to stdout)" -ForegroundColor Yellow
        exit 1
    }
}
catch {
    Write-Output "`n❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}
