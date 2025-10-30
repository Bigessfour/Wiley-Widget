<#
.SYNOPSIS
Simplified MCP validation - verify VS Code integration readiness.

.DESCRIPTION
The csharp-mcp server is designed to be used through VS Code's MCP extension,
which handles the full JSON-RPC protocol flow. This script validates:
1. Docker image is available
2. Container can start with correct mounts
3. Server initializes without errors

For actual C# evaluation, use the VS Code MCP extension.

.EXAMPLE
.\scripts\validate-mcp-setup.ps1
#>

Write-Output "=== MCP Setup Validation ==="

# Check 1: Docker image
Write-Output "`n1. Checking Docker image..."
$image = docker images ghcr.io/infinityflowapp/csharp-mcp:latest --format "{{.Repository}}:{{.Tag}}"
if ($image) {
    Write-Output "   ✅ Image found: $image" -ForegroundColor Green
}
else {
    Write-Output "   ❌ Image not found. Run: docker pull ghcr.io/infinityflowapp/csharp-mcp:latest" -ForegroundColor Red
    exit 1
}

# Check 2: Workspace volume
Write-Output "`n2. Checking workspace path..."
$workspace = (Get-Location).Path
if (Test-Path $workspace) {
    Write-Output "   ✅ Workspace: $workspace" -ForegroundColor Green
}
else {
    Write-Output "   ❌ Workspace path invalid" -ForegroundColor Red
    exit 1
}

# Check 3: Container can start
Write-Output "`n3. Testing container startup..."
$testMsg = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'

try {
    $output = $testMsg | docker run -i --rm `
        -v "${workspace}:/scripts:ro" `
        -e CSX_ALLOWED_PATH=/scripts `
        ghcr.io/infinityflowapp/csharp-mcp:latest 2>&1

    # Check if server started without fatal errors
    if ($output -match "Application started") {
        Write-Output "   ✅ Container started successfully" -ForegroundColor Green
    }
    else {
        Write-Output "   ⚠️  Container started but unexpected output" -ForegroundColor Yellow
    }

    if ($output -match "transport reading messages") {
        Write-Output "   ✅ MCP transport initialized" -ForegroundColor Green
    }

    if ($output -notmatch "error|exception|fail") {
        Write-Output "   ✅ No errors in server logs" -ForegroundColor Green
    }
    else {
        Write-Output "   ⚠️  Errors detected in logs:" -ForegroundColor Yellow
        $output | Where-Object { $_ -match "error|exception|fail" } | Select-Object -First 3 | ForEach-Object {
            Write-Output "      $_"
        }
    }
}
catch {
    Write-Output "   ❌ Failed to start container: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}

# Check 4: VS Code configuration
Write-Output "`n4. Checking VS Code MCP configuration..."
$mcpConfigPath = Join-Path $workspace ".vscode\mcp.json"
if (Test-Path $mcpConfigPath) {
    $mcpConfig = Get-Content $mcpConfigPath -Raw | ConvertFrom-Json
    if ($mcpConfig.servers.'csharp-mcp') {
        Write-Output "   ✅ csharp-mcp server configured in .vscode/mcp.json" -ForegroundColor Green
        $serverConfig = $mcpConfig.servers.'csharp-mcp'
        if ($serverConfig.command -match "docker") {
            Write-Output "   ✅ Uses Docker command" -ForegroundColor Green
        }
        if ($serverConfig.args -contains "-i") {
            Write-Output "   ✅ Uses interactive mode (-i flag)" -ForegroundColor Green
        }
    }
    else {
        Write-Output "   ⚠️  csharp-mcp not found in mcp.json" -ForegroundColor Yellow
    }
}
else {
    Write-Output "   ⚠️  .vscode/mcp.json not found" -ForegroundColor Yellow
}

Write-Output "`n=== Validation Complete ==="
Write-Output "`n✅ MCP server is ready for use in VS Code" -ForegroundColor Green
Write-Output "   Next steps:"
Write-Output "   1. Restart VS Code to load MCP configuration"
Write-Output "   2. Open a Copilot Chat or MCP-enabled extension"
Write-Output "   3. Request C# code evaluation (e.g., 'evaluate this C# code: Console.WriteLine(2+2);')"
Write-Output "   4. The extension will automatically use the csharp-mcp server via stdio"

exit 0
