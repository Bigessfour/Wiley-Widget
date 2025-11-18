#Requires -Version 7.5
<#
.SYNOPSIS
    Validate MCP Server Configuration and Health
.DESCRIPTION
    Comprehensive validation of all MCP servers defined in mcp.json:
    - Environment variable checks
    - Docker image availability
    - NPM package versions
    - Path accessibility
    - Security audit (token scopes, file access)
.EXAMPLE
    .\validate-mcp-setup.ps1 -Verbose
.EXAMPLE
    .\validate-mcp-setup.ps1 -FixIssues
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$FixIssues,

    [Parameter()]
    [switch]$UpdateImages
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# ──────────────────────────────────────────────────────────────
# Configuration
# ──────────────────────────────────────────────────────────────
$script:RepoRoot = Split-Path -Parent $PSScriptRoot | Split-Path -Parent
$script:McpConfigPath = "$env:APPDATA\Code\User\globalStorage\saoudrizwan.claude-dev\settings\cline_mcp_settings.json"
$script:Issues = @()
$script:Warnings = @()

# ──────────────────────────────────────────────────────────────
# Helper Functions
# ──────────────────────────────────────────────────────────────
function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Issue {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
    $script:Issues += $Message
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠️  $Message" -ForegroundColor Yellow
    $script:Warnings += $Message
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ️  $Message" -ForegroundColor Cyan
}

# ──────────────────────────────────────────────────────────────
# MCP Server Definitions (from user's config)
# ──────────────────────────────────────────────────────────────
$script:McpServers = @{
    'github' = @{
        Type = 'docker'
        Image = 'mcp/github'
        EnvVars = @('GITHUB_TOKEN', 'GITHUB_PERSONAL_ACCESS_TOKEN')
        Description = 'GitHub API integration for repos, issues, PRs'
    }
    'filesystem' = @{
        Type = 'npx'
        Package = '@modelcontextprotocol/server-filesystem'
        Description = 'Secure filesystem operations'
    }
    'csharp-mcp' = @{
        Type = 'docker'
        Image = 'ghcr.io/infinityflowapp/csharp-mcp:latest'
        EnvVars = @('CSX_ALLOWED_PATH', 'WW_REPO_ROOT', 'WW_LOGS_DIR')
        Volumes = @(
            "${RepoRoot}:/scripts:ro"
            "${RepoRoot}/logs:/logs:rw"
        )
        Description = 'C# script execution via Roslyn'
    }
    'everything' = @{
        Type = 'npx'
        Package = '@modelcontextprotocol/server-everything'
        Description = 'Comprehensive MCP feature testing'
    }
    'sequential-thinking' = @{
        Type = 'npx'
        Package = '@modelcontextprotocol/server-sequential-thinking'
        Description = 'Structured problem-solving and reasoning'
    }
    'mssql' = @{
        Type = 'npx'
        Package = '@modelcontextprotocol/server-mssql'
        EnvVars = @('MSSQL_CONNECTION_STRING')
        Description = 'SQL Server database access'
        Proposed = $true
    }
}

# ──────────────────────────────────────────────────────────────
# Validation Tests
# ──────────────────────────────────────────────────────────────

Write-Host @"

╔═══════════════════════════════════════════════════════════╗
║  Wiley Widget MCP Configuration Validator                ║
╚═══════════════════════════════════════════════════════════╝

"@ -ForegroundColor Cyan

# Test 1: MCP Config File Exists
Write-Info "Checking MCP configuration file..."
if (Test-Path $McpConfigPath) {
    Write-Success "MCP config found: $McpConfigPath"
    $mcpConfig = Get-Content $McpConfigPath -Raw | ConvertFrom-Json
} else {
    Write-Warning "MCP config not found at expected location"
    Write-Info "This is expected if using Docker Desktop MCP Toolkit"
}

# Test 2: Docker Availability
Write-Info "`nValidating Docker environment..."
try {
    $dockerVersion = docker --version
    Write-Success "Docker installed: $dockerVersion"
    
    # Check if Docker daemon is running
    docker ps | Out-Null
    Write-Success "Docker daemon is running"
} catch {
    Write-Issue "Docker is not available or not running"
    if ($FixIssues) {
        Write-Info "Please start Docker Desktop manually"
    }
}

# Test 3: Docker Images
Write-Info "`nChecking Docker-based MCP servers..."
foreach ($serverName in $McpServers.Keys) {
    $server = $McpServers[$serverName]
    
    if ($server.Type -eq 'docker' -and -not $server.Proposed) {
        Write-Host "`n  Server: $serverName" -ForegroundColor White
        Write-Host "  Description: $($server.Description)" -ForegroundColor Gray
        
        try {
            $imageExists = docker images --format "{{.Repository}}:{{.Tag}}" | 
                           Select-String -Pattern $server.Image -Quiet
            
            if ($imageExists) {
                Write-Success "  Image available: $($server.Image)"
                
                # Check image age
                $imageInfo = docker inspect $server.Image 2>$null | ConvertFrom-Json
                if ($imageInfo) {
                    $created = [DateTime]$imageInfo[0].Created
                    $age = (Get-Date) - $created
                    
                    if ($age.Days -gt 30) {
                        Write-Warning "  Image is $($age.Days) days old - consider updating"
                        
                        if ($UpdateImages) {
                            Write-Info "  Pulling latest image..."
                            docker pull $server.Image
                        }
                    } else {
                        Write-Success "  Image is up to date ($($age.Days) days old)"
                    }
                }
            } else {
                Write-Issue "  Image not found: $($server.Image)"
                
                if ($FixIssues) {
                    Write-Info "  Pulling image..."
                    docker pull $server.Image
                }
            }
            
            # Validate environment variables
            if ($server.EnvVars) {
                foreach ($envVar in $server.EnvVars) {
                    if ([Environment]::GetEnvironmentVariable($envVar)) {
                        Write-Success "  Environment variable set: $envVar"
                    } else {
                        Write-Issue "  Missing environment variable: $envVar"
                    }
                }
            }
            
            # Validate volume paths
            if ($server.Volumes) {
                foreach ($volume in $server.Volumes) {
                    $hostPath = $volume -split ':' | Select-Object -First 1
                    $resolvedPath = $ExecutionContext.InvokeCommand.ExpandString($hostPath)
                    
                    if (Test-Path $resolvedPath) {
                        Write-Success "  Volume path exists: $resolvedPath"
                    } else {
                        Write-Warning "  Volume path missing: $resolvedPath"
                        
                        if ($FixIssues -and $resolvedPath -like "*logs*") {
                            New-Item -ItemType Directory -Path $resolvedPath -Force | Out-Null
                            Write-Success "  Created missing directory: $resolvedPath"
                        }
                    }
                }
            }
        } catch {
            Write-Issue "  Error checking $serverName : $_"
        }
    }
}

# Test 4: NPM-based Servers
Write-Info "`nChecking NPM-based MCP servers..."
try {
    $npmVersion = npm --version 2>$null
    Write-Success "NPM installed: v$npmVersion"
    
    foreach ($serverName in $McpServers.Keys) {
        $server = $McpServers[$serverName]
        
        if ($server.Type -eq 'npx' -and -not $server.Proposed) {
            Write-Host "`n  Server: $serverName" -ForegroundColor White
            Write-Host "  Description: $($server.Description)" -ForegroundColor Gray
            
            # Check if package is available via npx
            $packageCheck = npx --yes --package=$($server.Package) --call="exit 0" 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                Write-Success "  Package available: $($server.Package)"
            } else {
                Write-Warning "  Package may need first-time download: $($server.Package)"
            }
        }
    }
} catch {
    Write-Issue "NPM is not available"
}

# Test 5: Security Audit
Write-Info "`nPerforming security audit..."

# Check GitHub token scopes
if ($env:GITHUB_TOKEN) {
    try {
        $tokenInfo = gh auth status 2>&1 | Out-String
        if ($tokenInfo -match 'repo|admin|workflow') {
            Write-Success "GitHub token has appropriate scopes"
        } else {
            Write-Warning "GitHub token may need additional scopes for full MCP functionality"
        }
    } catch {
        Write-Warning "Could not validate GitHub token scopes (gh CLI not available)"
    }
}

# Check filesystem roots
$filesystemRoots = @($RepoRoot)
foreach ($root in $filesystemRoots) {
    if (Test-Path $root) {
        # Check for sensitive files
        $sensitiveFiles = @(
            ".env"
            "secrets/*"
            "*.pfx"
            "*.p12"
        )
        
        foreach ($pattern in $sensitiveFiles) {
            $found = Get-ChildItem -Path $root -Filter $pattern -Recurse -ErrorAction SilentlyContinue
            if ($found) {
                Write-Warning "Sensitive files found in filesystem root: $($found.Count) matches for $pattern"
                Write-Info "  Consider excluding from MCP access"
            }
        }
    }
}

# Test 6: Proposed Enhancements
Write-Info "`nProposed MCP Server Enhancements:"
foreach ($serverName in $McpServers.Keys) {
    $server = $McpServers[$serverName]
    
    if ($server.Proposed) {
        Write-Host "`n  📦 $serverName (NOT INSTALLED)" -ForegroundColor Magenta
        Write-Host "     Description: $($server.Description)" -ForegroundColor Gray
        Write-Host "     Type: $($server.Type)" -ForegroundColor Gray
        Write-Host "     Package: $($server.Package)" -ForegroundColor Gray
        
        if ($FixIssues) {
            Write-Info "     To install: npx $($server.Package)"
        }
    }
}

# ──────────────────────────────────────────────────────────────
# Summary Report
# ──────────────────────────────────────────────────────────────
Write-Host "`n"
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  VALIDATION SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

if ($Issues.Count -eq 0) {
    Write-Success "`nAll MCP servers validated successfully! ✨"
} else {
    Write-Host "`n❌ Issues Found: $($Issues.Count)" -ForegroundColor Red
    foreach ($issue in $Issues) {
        Write-Host "   • $issue" -ForegroundColor Red
    }
}

if ($Warnings.Count -gt 0) {
    Write-Host "`n⚠️  Warnings: $($Warnings.Count)" -ForegroundColor Yellow
    foreach ($warning in $Warnings) {
        Write-Host "   • $warning" -ForegroundColor Yellow
    }
}

# Recommendations
Write-Host "`n💡 Recommendations:" -ForegroundColor Cyan
Write-Host "   1. Add SQL Server MCP for database testing integration" -ForegroundColor White
Write-Host "   2. Update Docker images monthly (use -UpdateImages flag)" -ForegroundColor White
Write-Host "   3. Audit GitHub token scopes regularly" -ForegroundColor White
Write-Host "   4. Consider custom Syncfusion MCP for WinUI validation" -ForegroundColor White
Write-Host "   5. Integrate sequential-thinking with csharp-mcp for debugging" -ForegroundColor White

Write-Host "`n"

# Exit code
if ($Issues.Count -gt 0) {
    exit 1
} else {
    exit 0
}
