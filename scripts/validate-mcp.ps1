Write-Host "MCP Tool Validation Script" -ForegroundColor Cyan
Write-Host "==========================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Testing GitHub CLI authentication..." -ForegroundColor Yellow
try {
    $authStatus = gh auth status 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ GitHub CLI authentication OK" -ForegroundColor Green
    } else {
        Write-Host "❌ GitHub CLI authentication failed" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ GitHub CLI authentication failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host ""

Write-Host "Testing repository access..." -ForegroundColor Yellow
try {
    $repoInfo = gh repo view Bigessfour/Wiley-Widget --json name,visibility 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Repository access OK" -ForegroundColor Green
    } else {
        Write-Host "❌ Repository access failed" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Repository access failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host ""

Write-Host "Testing Bright Data API Key..." -ForegroundColor Yellow
$brightDataApiKey = $env:BRIGHTDATA_API_KEY
if ($brightDataApiKey) {
    Write-Host "✅ Bright Data API Key found" -ForegroundColor Green
} else {
    Write-Host "❌ Bright Data API Key not found in environment variables" -ForegroundColor Red
    Write-Host "   Please set BRIGHTDATA_API_KEY environment variable" -ForegroundColor Yellow
}
Write-Host ""

Write-Host "Testing MCP configuration file..." -ForegroundColor Yellow
$mcpConfigPath = ".vscode\mcp.json"
if (Test-Path $mcpConfigPath) {
    try {
        $mcpConfig = Get-Content $mcpConfigPath -Raw | ConvertFrom-Json
        $servers = $mcpConfig.servers

        if ($servers.github -and $servers.azure -and $servers.'microsoft-docs' -and $servers.brightdata) {
            Write-Host "✅ All MCP servers configured (GitHub, Azure, Microsoft Docs, Bright Data)" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Some MCP servers missing from configuration" -ForegroundColor Yellow
            Write-Host "   Expected: github, azure, microsoft-docs, brightdata" -ForegroundColor Gray
        }
    } catch {
        Write-Host "❌ Error parsing MCP configuration: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "❌ MCP configuration file not found at $mcpConfigPath" -ForegroundColor Red
}
Write-Host ""

Write-Host "Testing MCP tool connectivity..." -ForegroundColor Yellow
Write-Host "Note: MCP tools are validated through direct function calls in your development environment" -ForegroundColor Gray
Write-Host "Manual validation required for MCP tools" -ForegroundColor Gray
Write-Host ""

Write-Host "Validation complete! Check .mcp-config-status.md for detailed status." -ForegroundColor Green
Write-Host "Last verified: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Green
Write-Host ""
Read-Host "Press Enter to continue"
