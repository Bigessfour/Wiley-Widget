# Test GitHub MCP Server Connection
# This script verifies that the GitHub MCP server can connect and authenticate

param(
    [switch]$Verbose,
    [switch]$TestAPI
)

Write-Output "🔍 Testing GitHub MCP Server Setup for WileyWidget"
Write-Output "=" * 60

# Check environment variables
Write-Output "`n📋 Checking Environment Variables..."

$githubToken = $env:GITHUB_PERSONAL_ACCESS_TOKEN
$githubRepo = $env:GITHUB_REPOSITORY
$githubApiUrl = $env:GITHUB_API_URL

if ($githubToken) {
    Write-Output "✅ GITHUB_PERSONAL_ACCESS_TOKEN: Set"
    if ($Verbose) {
        Write-Output "   Token starts with: $($githubToken.Substring(0, 8))..."
    }
} else {
    Write-Output "❌ GITHUB_PERSONAL_ACCESS_TOKEN: Not set"
}

if ($githubRepo) {
    Write-Output "✅ GITHUB_REPOSITORY: $githubRepo"
} else {
    Write-Output "❌ GITHUB_REPOSITORY: Not set"
}

if ($githubApiUrl) {
    Write-Output "✅ GITHUB_API_URL: $githubApiUrl"
} else {
    Write-Output "⚠️  GITHUB_API_URL: Using default (https://api.github.com)"
}

# Check Node.js and npm
Write-Output "`n🔧 Checking Development Tools..."

try {
    $nodeVersion = & node --version 2>$null
    Write-Output "✅ Node.js: $nodeVersion"
} catch {
    Write-Output "❌ Node.js: Not found"
}

try {
    $npmVersion = & npm --version 2>$null
    Write-Output "✅ npm: $npmVersion"
} catch {
    Write-Output "❌ npm: Not found"
}

# Test GitHub MCP server package
Write-Output "`n🚀 Testing GitHub MCP Server Package..."

try {
    $mcpHelp = & npx @modelcontextprotocol/server-github --help 2>$null
    Write-Host "✅ GitHub MCP Server package: Available" -ForegroundColor Green
} catch {
    Write-Host "❌ GitHub MCP Server package: Not available" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test API connection if requested
if ($TestAPI -and $githubToken) {
    Write-Host "`n🌐 Testing GitHub API Connection..." -ForegroundColor Green

    try {
        $headers = @{
            "Authorization" = "Bearer $githubToken"
            "Accept" = "application/vnd.github.v3+json"
        }

        $response = Invoke-RestMethod -Uri "$githubApiUrl/user" -Headers $headers -Method Get

        Write-Host "✅ GitHub API Connection: Successful" -ForegroundColor Green
        Write-Host "   Authenticated as: $($response.login)" -ForegroundColor Cyan

        if ($githubRepo) {
            # Test repository access
            $repoResponse = Invoke-RestMethod -Uri "$githubApiUrl/repos/$githubRepo" -Headers $headers -Method Get
            Write-Host "✅ Repository Access: $githubRepo" -ForegroundColor Green
        }

    } catch {
        Write-Host "❌ GitHub API Connection: Failed" -ForegroundColor Red
        Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Summary
Write-Host "`n📊 Summary" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Yellow

$allGood = $true

if (-not $githubToken) { $allGood = $false }
if (-not $githubRepo) { $allGood = $false }

if ($allGood) {
    Write-Host "✅ GitHub MCP Server setup appears to be complete!" -ForegroundColor Green
    Write-Host "   The server should work with VS Code MCP integration." -ForegroundColor Green
} else {
    Write-Host "⚠️  Some configuration issues detected." -ForegroundColor Yellow
    Write-Host "   Please check the errors above and fix them." -ForegroundColor Yellow
}

Write-Host "`n💡 Next Steps:" -ForegroundColor Cyan
Write-Host "   1. Restart VS Code to reload MCP configuration" -ForegroundColor White
Write-Host "   2. Check VS Code Output panel for MCP server logs" -ForegroundColor White
Write-Host "   3. Test MCP tools in your development workflow" -ForegroundColor White

Write-Host "`n🔗 Useful Links:" -ForegroundColor Cyan
Write-Host "   - GitHub MCP Server: https://github.com/modelcontextprotocol/server-github" -ForegroundColor White
Write-Host "   - MCP Documentation: https://modelcontextprotocol.io" -ForegroundColor White
