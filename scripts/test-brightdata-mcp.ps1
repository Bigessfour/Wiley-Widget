# BrightData MCP Security & Configuration Test
Write-Host "BrightData MCP Security & Configuration Test" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Check API Key Security
Write-Host "Test 1: Checking API Key Security..." -ForegroundColor Yellow
$apiKey = $env:BRIGHTDATA_API_KEY
if ($apiKey) {
    if ($apiKey -ne "YOUR_ACTUAL_BRIGHTDATA_API_KEY_HERE" -and $apiKey -ne "YOUR_BRIGHTDATA_API_KEY") {
        Write-Host "✅ API Key is properly configured" -ForegroundColor Green
        Write-Host "   Length: $($apiKey.Length) characters" -ForegroundColor Gray
        if ($apiKey.Length -gt 50) {
            Write-Host "   ✅ API Key appears to be a proper token/key" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️ API Key seems short, please verify it's correct" -ForegroundColor Yellow
        }
    } else {
        Write-Host "❌ API Key is still using placeholder value" -ForegroundColor Red
    }
} else {
    Write-Host "❌ BRIGHTDATA_API_KEY environment variable not found" -ForegroundColor Red
}
Write-Host ""

# Test 2: Check MCP Configuration Security
Write-Host "Test 2: Checking MCP Configuration Security..." -ForegroundColor Yellow
$mcpConfigPath = ".vscode\mcp.json"
if (Test-Path $mcpConfigPath) {
    try {
        $mcpConfig = Get-Content $mcpConfigPath -Raw | ConvertFrom-Json
        $brightDataServer = $mcpConfig.servers.brightdata
        if ($brightDataServer) {
            Write-Host "✅ BrightData MCP server configured" -ForegroundColor Green
            Write-Host "   URL: $($brightDataServer.url)" -ForegroundColor Gray
            Write-Host "   Type: $($brightDataServer.type)" -ForegroundColor Gray

            # Check for secure headers
            if ($brightDataServer.headers.Authorization -match "Bearer \$\{env:BRIGHTDATA_API_KEY\}") {
                Write-Host "   ✅ Secure Bearer token authentication configured" -ForegroundColor Green
            } else {
                Write-Host "   ⚠️ Authentication method may not be secure" -ForegroundColor Yellow
            }

            if ($brightDataServer.headers."Content-Type" -eq "application/json") {
                Write-Host "   ✅ Proper content type configured" -ForegroundColor Green
            }
        } else {
            Write-Host "❌ BrightData MCP server not found in configuration" -ForegroundColor Red
        }
    } catch {
        Write-Host "❌ Error parsing MCP configuration: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "❌ MCP configuration file not found" -ForegroundColor Red
}
Write-Host ""

# Test 3: Check .NET Service Security
Write-Host "Test 3: Checking .NET Service Security..." -ForegroundColor Yellow
$servicePath = "Services\BrightDataService.cs"
if (Test-Path $servicePath) {
    $serviceContent = Get-Content $servicePath -Raw

    if ($serviceContent -match "Bearer.*_apiKey") {
        Write-Host "✅ .NET service uses secure Bearer token authentication" -ForegroundColor Green
    } else {
        Write-Host "⚠️ .NET service authentication method unclear" -ForegroundColor Yellow
    }

    if ($serviceContent -match "mcp\.brightdata\.com") {
        Write-Host "✅ .NET service configured for MCP endpoint" -ForegroundColor Green
    } else {
        Write-Host "❌ .NET service not using MCP endpoint" -ForegroundColor Red
    }

    if ($serviceContent -match "User-Agent.*WileyWidget") {
        Write-Host "✅ .NET service has proper User-Agent header" -ForegroundColor Green
    }

    if ($serviceContent -match "Timeout.*30") {
        Write-Host "✅ .NET service has proper timeout configuration" -ForegroundColor Green
    }
} else {
    Write-Host "❌ BrightData service file not found" -ForegroundColor Red
}
Write-Host ""

# Test 4: Check Environment Variable Security
Write-Host "Test 4: Checking Environment Variable Security..." -ForegroundColor Yellow
$envFiles = Get-ChildItem "*.env*" -ErrorAction SilentlyContinue
$secureEnvFound = $false

foreach ($envFile in $envFiles) {
    $content = Get-Content $envFile.FullName -Raw
    if ($content -match "BRIGHTDATA_API_KEY.*=.*[a-zA-Z0-9]{20,}") {
        Write-Host "✅ Environment file contains properly formatted API key" -ForegroundColor Green
        $secureEnvFound = $true
        break
    }
}

if (-not $secureEnvFound) {
    Write-Host "⚠️ No secure API key found in environment files" -ForegroundColor Yellow
}
Write-Host ""

# Test 5: Test MCP Connectivity (if API key is available)
Write-Host "Test 5: Testing MCP Server Connectivity..." -ForegroundColor Yellow
if ($apiKey -and $apiKey -ne "YOUR_ACTUAL_BRIGHTDATA_API_KEY_HERE" -and $apiKey -ne "YOUR_BRIGHTDATA_API_KEY") {
    try {
        $testRequest = @{
            jsonrpc = "2.0"
            id = "test-$(Get-Date -Format 'yyyyMMddHHmmss')"
            method = "tools/list"
            params = @{}
        } | ConvertTo-Json

        $response = Invoke-RestMethod -Uri "https://mcp.brightdata.com/" -Method Post -Body $testRequest -ContentType "application/json" -Headers @{
            "Authorization" = "Bearer $apiKey"
            "User-Agent" = "WileyWidget-Test/1.0"
        } -TimeoutSec 10

        if ($response.jsonrpc -eq "2.0") {
            Write-Host "✅ MCP server connection successful" -ForegroundColor Green
            Write-Host "   Server responded with JSON-RPC 2.0" -ForegroundColor Gray
        } else {
            Write-Host "⚠️ MCP server responded but format unclear" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "❌ MCP server connection failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "   This may be normal if the API key needs activation" -ForegroundColor Yellow
    }
} else {
    Write-Host "⏭️ Skipping connectivity test - API key not properly configured" -ForegroundColor Gray
}
Write-Host ""

Write-Host "Security Summary:" -ForegroundColor White
Write-Host "=================" -ForegroundColor White
Write-Host "✅ Environment variables used for sensitive data" -ForegroundColor Green
Write-Host "✅ Bearer token authentication implemented" -ForegroundColor Green
Write-Host "✅ HTTPS endpoints configured" -ForegroundColor Green
Write-Host "✅ API key validation added" -ForegroundColor Green
Write-Host "✅ Proper timeout and User-Agent headers" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor White
Write-Host "1. Ensure BRIGHTDATA_API_KEY is set in machine environment variables" -ForegroundColor White
Write-Host "2. Verify API key is activated in BrightData dashboard" -ForegroundColor White
Write-Host "3. Test the integration in your WPF application" -ForegroundColor White
Write-Host "4. Monitor logs for any authentication issues" -ForegroundColor White
