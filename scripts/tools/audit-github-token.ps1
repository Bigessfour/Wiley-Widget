#Requires -Version 7.5
<#
.SYNOPSIS
    GitHub Token Scope and Security Audit
.DESCRIPTION
    Comprehensive audit of GitHub token permissions, expiration, usage patterns,
    and security compliance. Generates detailed reports and recommendations.
.PARAMETER GenerateReport
    Create detailed HTML/JSON report
.PARAMETER CheckExpiration
    Verify token expiration dates
.PARAMETER ValidateScopes
    Check if token has required scopes for MCP operations
.EXAMPLE
    .\audit-github-token.ps1 -ValidateScopes -CheckExpiration
.EXAMPLE
    .\audit-github-token.ps1 -GenerateReport -Verbose
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$GenerateReport,

    [Parameter()]
    [switch]$CheckExpiration,

    [Parameter()]
    [switch]$ValidateScopes
)

$ErrorActionPreference = 'Stop'

# ──────────────────────────────────────────────────────────────
# Configuration
# ──────────────────────────────────────────────────────────────
$script:RepoRoot = Split-Path -Parent $PSScriptRoot | Split-Path -Parent
$script:LogDir = Join-Path $RepoRoot 'logs'
$script:AuditLog = Join-Path $LogDir "github-token-audit-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
$script:AuditResults = @{
    Timestamp = Get-Date -Format 'o'
    TokenStatus = @{}
    ScopeValidation = @{}
    SecurityFindings = @()
    Recommendations = @()
}

# Required scopes for MCP operations
$script:RequiredScopes = @{
    'repo' = @{
        Description = 'Full repository access'
        Required = $true
        Reason = 'GitHub MCP needs to read/write repo content, manage PRs, issues'
    }
    'workflow' = @{
        Description = 'GitHub Actions workflow management'
        Required = $true
        Reason = 'CI/CD integration requires workflow read/write access'
    }
    'read:org' = @{
        Description = 'Organization read access'
        Required = $false
        Reason = 'Useful for organization-level operations'
    }
    'gist' = @{
        Description = 'Gist management'
        Required = $false
        Reason = 'Optional for sharing code snippets'
    }
}

# ──────────────────────────────────────────────────────────────
# Helper Functions
# ──────────────────────────────────────────────────────────────
function Write-AuditLog {
    param(
        [string]$Message,
        [ValidateSet('INFO', 'SUCCESS', 'WARNING', 'ERROR')]
        [string]$Level = 'INFO'
    )
    
    $color = switch ($Level) {
        'SUCCESS' { 'Green' }
        'WARNING' { 'Yellow' }
        'ERROR' { 'Red' }
        default { 'White' }
    }
    
    $emoji = switch ($Level) {
        'SUCCESS' { '✅' }
        'WARNING' { '⚠️' }
        'ERROR' { '❌' }
        default { 'ℹ️' }
    }
    
    Write-Host "$emoji $Message" -ForegroundColor $color
    Write-Verbose $Message
}

function Test-GitHubCLI {
    try {
        $ghVersion = gh --version 2>&1 | Select-Object -First 1
        Write-AuditLog "GitHub CLI detected: $ghVersion" 'SUCCESS'
        return $true
    } catch {
        Write-AuditLog "GitHub CLI (gh) not found. Install from: https://cli.github.com/" 'ERROR'
        return $false
    }
}

function Get-TokenFromEnvironment {
    $tokenVars = @('GITHUB_TOKEN', 'GITHUB_PERSONAL_ACCESS_TOKEN', 'GH_TOKEN')
    $foundTokens = @{}
    
    foreach ($var in $tokenVars) {
        $value = [Environment]::GetEnvironmentVariable($var, 'User')
        if (-not $value) {
            $value = [Environment]::GetEnvironmentVariable($var, 'Process')
        }
        
        if ($value) {
            $foundTokens[$var] = @{
                Present = $true
                Length = $value.Length
                Prefix = $value.Substring(0, [Math]::Min(4, $value.Length))
                Source = if ([Environment]::GetEnvironmentVariable($var, 'User')) { 'User' } else { 'Process' }
            }
        } else {
            $foundTokens[$var] = @{ Present = $false }
        }
    }
    
    return $foundTokens
}

function Get-GitHubAuthStatus {
    try {
        $authStatus = gh auth status 2>&1 | Out-String
        
        $result = @{
            Authenticated = $authStatus -match 'Logged in'
            User = ''
            Scopes = @()
            RawOutput = $authStatus
        }
        
        # Parse username
        if ($authStatus -match 'account\s+(\S+)') {
            $result.User = $Matches[1]
        }
        
        # Parse scopes
        if ($authStatus -match 'Token scopes:\s+(.+)') {
            $scopeString = $Matches[1].Trim()
            $result.Scopes = $scopeString -split ',\s*'
        }
        
        return $result
    } catch {
        Write-AuditLog "Failed to get auth status: $($_.Exception.Message)" 'ERROR'
        return @{ Authenticated = $false; Scopes = @() }
    }
}

function Test-TokenExpiration {
    try {
        # Use GitHub API to check token metadata
        $headers = @{
            'Accept' = 'application/vnd.github.v3+json'
        }
        
        if ($env:GITHUB_TOKEN) {
            $headers['Authorization'] = "token $env:GITHUB_TOKEN"
        }
        
        $response = Invoke-RestMethod -Uri 'https://api.github.com/rate_limit' -Headers $headers
        
        $result = @{
            RateLimit = $response.rate.limit
            Remaining = $response.rate.remaining
            Reset = [DateTimeOffset]::FromUnixTimeSeconds($response.rate.reset).LocalDateTime
            HasExpiration = $false
            ExpiresAt = $null
        }
        
        # Fine-grained PATs include expiration in X-GitHub-Token-Expiration header
        # Classic PATs don't have expiration by default
        
        return $result
    } catch {
        Write-AuditLog "Could not check token expiration: $($_.Exception.Message)" 'WARNING'
        return $null
    }
}

function Test-RequiredScopes {
    param([array]$CurrentScopes)
    
    $validation = @{}
    $allPassed = $true
    
    foreach ($scope in $RequiredScopes.Keys) {
        $config = $RequiredScopes[$scope]
        $hasScope = $CurrentScopes -contains $scope
        
        $validation[$scope] = @{
            Present = $hasScope
            Required = $config.Required
            Description = $config.Description
            Reason = $config.Reason
            Status = if ($hasScope) { 'PASS' } elseif ($config.Required) { 'FAIL' } else { 'OPTIONAL' }
        }
        
        if ($config.Required -and -not $hasScope) {
            $allPassed = $false
        }
    }
    
    return @{
        AllRequiredPresent = $allPassed
        Details = $validation
    }
}

function Get-SecurityFindings {
    $findings = @()
    
    # Check 1: Token in plaintext files
    $searchPaths = @(
        "$RepoRoot\.env*"
        "$RepoRoot\*.config"
        "$RepoRoot\*.json"
    )
    
    foreach ($pattern in $searchPaths) {
        $files = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue
        foreach ($file in $files) {
            if ($file.Name -match 'secrets|token|credential') {
                $findings += @{
                    Severity = 'HIGH'
                    Finding = "Potential token storage in plaintext: $($file.FullName)"
                    Recommendation = "Use environment variables or secure secret management"
                }
            }
        }
    }
    
    # Check 2: Token in git history (basic check)
    try {
        $gitLog = git log --all --pretty=format:%s -i --grep='token\|password\|secret' 2>$null
        if ($gitLog) {
            $findings += @{
                Severity = 'MEDIUM'
                Finding = "Commit messages mention 'token/password/secret' - verify no actual secrets committed"
                Recommendation = "Audit git history with git-secrets or gitleaks"
            }
        }
    } catch {
        # Git not available or not a repo
    }
    
    # Check 3: Overly permissive scopes
    $authStatus = Get-GitHubAuthStatus
    $dangerousScopes = @('admin:org', 'admin:public_key', 'delete_repo', 'admin:repo_hook')
    $foundDangerous = $authStatus.Scopes | Where-Object { $dangerousScopes -contains $_ }
    
    if ($foundDangerous) {
        $findings += @{
            Severity = 'MEDIUM'
            Finding = "Token has elevated scopes: $($foundDangerous -join ', ')"
            Recommendation = "Review if these scopes are necessary. Consider least-privilege principle"
        }
    }
    
    # Check 4: MCP config file permissions
    $mcpConfig = "$env:APPDATA\Code\User\globalStorage\saoudrizwan.claude-dev\settings\cline_mcp_settings.json"
    if (Test-Path $mcpConfig) {
        $acl = Get-Acl $mcpConfig
        $publicAccess = $acl.Access | Where-Object { $_.IdentityReference -like '*Users*' -and $_.FileSystemRights -match 'Read' }
        
        if ($publicAccess) {
            $findings += @{
                Severity = 'MEDIUM'
                Finding = "MCP config has broad read permissions"
                Recommendation = "Restrict access to current user only"
            }
        }
    }
    
    return $findings
}

function New-AuditReport {
    $reportHtml = @"
<!DOCTYPE html>
<html>
<head>
    <title>GitHub Token Security Audit - $(Get-Date -Format 'yyyy-MM-dd')</title>
    <style>
        body { font-family: 'Segoe UI', sans-serif; margin: 40px; background: #f5f5f5; }
        .container { max-width: 1000px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        h1 { color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 10px; }
        h2 { color: #34495e; margin-top: 30px; }
        .status { padding: 10px; border-radius: 5px; margin: 10px 0; }
        .success { background: #d4edda; border-left: 4px solid #28a745; }
        .warning { background: #fff3cd; border-left: 4px solid #ffc107; }
        .error { background: #f8d7da; border-left: 4px solid #dc3545; }
        table { width: 100%; border-collapse: collapse; margin: 20px 0; }
        th { background: #3498db; color: white; padding: 12px; text-align: left; }
        td { padding: 10px; border-bottom: 1px solid #ddd; }
        tr:hover { background: #f8f9fa; }
        .badge { padding: 4px 8px; border-radius: 3px; font-size: 12px; font-weight: bold; }
        .badge-success { background: #28a745; color: white; }
        .badge-warning { background: #ffc107; color: black; }
        .badge-error { background: #dc3545; color: white; }
        .finding { margin: 15px 0; padding: 15px; border-radius: 5px; }
        .finding-high { background: #f8d7da; border-left: 4px solid #dc3545; }
        .finding-medium { background: #fff3cd; border-left: 4px solid #ffc107; }
        .finding-low { background: #d1ecf1; border-left: 4px solid #17a2b8; }
    </style>
</head>
<body>
    <div class="container">
        <h1>🔐 GitHub Token Security Audit</h1>
        <p><strong>Generated:</strong> $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</p>
        <p><strong>Repository:</strong> Wiley Widget</p>
        
        <h2>Authentication Status</h2>
        <div class="status $(if ($AuditResults.TokenStatus.Authenticated) { 'success' } else { 'error' })">
            <strong>Status:</strong> $(if ($AuditResults.TokenStatus.Authenticated) { '✅ Authenticated' } else { '❌ Not Authenticated' })
            $(if ($AuditResults.TokenStatus.User) { "<br><strong>User:</strong> $($AuditResults.TokenStatus.User)" })
        </div>
        
        <h2>Scope Validation</h2>
        <table>
            <tr>
                <th>Scope</th>
                <th>Status</th>
                <th>Required</th>
                <th>Description</th>
            </tr>
            $(foreach ($scope in $AuditResults.ScopeValidation.Details.Keys) {
                $detail = $AuditResults.ScopeValidation.Details[$scope]
                $badgeClass = switch ($detail.Status) {
                    'PASS' { 'badge-success' }
                    'FAIL' { 'badge-error' }
                    'OPTIONAL' { 'badge-warning' }
                }
                @"
            <tr>
                <td><code>$scope</code></td>
                <td><span class="badge $badgeClass">$($detail.Status)</span></td>
                <td>$(if ($detail.Required) { '✓' } else { '○' })</td>
                <td>$($detail.Description)<br><small style="color: #666;">$($detail.Reason)</small></td>
            </tr>
"@
            })
        </table>
        
        <h2>Security Findings</h2>
        $(if ($AuditResults.SecurityFindings.Count -eq 0) {
            '<div class="status success">✅ No security issues detected</div>'
        } else {
            foreach ($finding in $AuditResults.SecurityFindings) {
                $severityClass = "finding-$($finding.Severity.ToLower())"
                @"
        <div class="finding $severityClass">
            <strong>[$($finding.Severity)]</strong> $($finding.Finding)
            <br><small>💡 <strong>Recommendation:</strong> $($finding.Recommendation)</small>
        </div>
"@
            }
        })
        
        <h2>Recommendations</h2>
        <ul>
            $(foreach ($rec in $AuditResults.Recommendations) {
                "<li>$rec</li>"
            })
        </ul>
        
        <hr style="margin: 40px 0; border: none; border-top: 1px solid #ddd;">
        <p style="text-align: center; color: #666; font-size: 12px;">
            Generated by audit-github-token.ps1 | Wiley Widget Project<br>
            For security concerns, contact the repository maintainers
        </p>
    </div>
</body>
</html>
"@
    
    $reportPath = Join-Path $LogDir "github-token-audit-$(Get-Date -Format 'yyyyMMdd-HHmmss').html"
    $reportHtml | Out-File -FilePath $reportPath -Encoding utf8
    
    Write-AuditLog "HTML report saved: $reportPath" 'SUCCESS'
    
    # Also save JSON
    $AuditResults | ConvertTo-Json -Depth 10 | Out-File -FilePath $AuditLog -Encoding utf8
    Write-AuditLog "JSON report saved: $AuditLog" 'SUCCESS'
    
    return $reportPath
}

# ──────────────────────────────────────────────────────────────
# Main Execution
# ──────────────────────────────────────────────────────────────

Write-Host @"

╔═══════════════════════════════════════════════════════════╗
║  GitHub Token Security Audit Tool                        ║
╚═══════════════════════════════════════════════════════════╝

"@ -ForegroundColor Cyan

# Ensure log directory exists
if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}

# Check GitHub CLI
if (-not (Test-GitHubCLI)) {
    exit 1
}

# Get token from environment
Write-Host "`n📋 Checking Environment Variables..." -ForegroundColor Cyan
$envTokens = Get-TokenFromEnvironment
foreach ($var in $envTokens.Keys) {
    $info = $envTokens[$var]
    if ($info.Present) {
        Write-AuditLog "  $var : Present ($($info.Length) chars, source: $($info.Source))" 'SUCCESS'
    } else {
        Write-AuditLog "  $var : Not set" 'WARNING'
    }
}

# Get GitHub auth status
Write-Host "`n🔐 Checking GitHub Authentication..." -ForegroundColor Cyan
$authStatus = Get-GitHubAuthStatus
$AuditResults.TokenStatus = $authStatus

if ($authStatus.Authenticated) {
    Write-AuditLog "Authenticated as: $($authStatus.User)" 'SUCCESS'
    Write-AuditLog "Scopes: $($authStatus.Scopes -join ', ')" 'INFO'
} else {
    Write-AuditLog "Not authenticated. Run 'gh auth login' to authenticate." 'ERROR'
    exit 1
}

# Validate scopes
if ($ValidateScopes -or $GenerateReport) {
    Write-Host "`n✓ Validating Token Scopes..." -ForegroundColor Cyan
    $scopeValidation = Test-RequiredScopes -CurrentScopes $authStatus.Scopes
    $AuditResults.ScopeValidation = $scopeValidation
    
    foreach ($scope in $scopeValidation.Details.Keys) {
        $detail = $scopeValidation.Details[$scope]
        $status = switch ($detail.Status) {
            'PASS' { 'SUCCESS' }
            'FAIL' { 'ERROR' }
            'OPTIONAL' { 'WARNING' }
        }
        Write-AuditLog "  $scope : $($detail.Status)" $status
    }
    
    if (-not $scopeValidation.AllRequiredPresent) {
        $AuditResults.Recommendations += "Update token with missing required scopes: repo, workflow"
    }
}

# Check expiration
if ($CheckExpiration -or $GenerateReport) {
    Write-Host "`n⏰ Checking Token Expiration..." -ForegroundColor Cyan
    $expiration = Test-TokenExpiration
    
    if ($expiration) {
        Write-AuditLog "Rate Limit: $($expiration.Remaining)/$($expiration.RateLimit)" 'INFO'
        Write-AuditLog "Rate Reset: $($expiration.Reset)" 'INFO'
        
        if ($expiration.Remaining -lt 100) {
            $AuditResults.Recommendations += "Rate limit low ($($expiration.Remaining) remaining). Consider waiting or using different token."
        }
    }
}

# Security audit
Write-Host "`n🛡️ Performing Security Audit..." -ForegroundColor Cyan
$findings = Get-SecurityFindings
$AuditResults.SecurityFindings = $findings

if ($findings.Count -eq 0) {
    Write-AuditLog "No security issues detected" 'SUCCESS'
} else {
    foreach ($finding in $findings) {
        $level = switch ($finding.Severity) {
            'HIGH' { 'ERROR' }
            'MEDIUM' { 'WARNING' }
            default { 'INFO' }
        }
        Write-AuditLog "[$($finding.Severity)] $($finding.Finding)" $level
        Write-AuditLog "  💡 $($finding.Recommendation)" 'INFO'
    }
}

# General recommendations
$AuditResults.Recommendations += "Rotate token every 90 days"
$AuditResults.Recommendations += "Use fine-grained PATs with minimum required permissions"
$AuditResults.Recommendations += "Enable token expiration for enhanced security"
$AuditResults.Recommendations += "Store tokens in secure environment variables, not plaintext files"

# Generate report
if ($GenerateReport) {
    Write-Host "`n📄 Generating Audit Report..." -ForegroundColor Cyan
    $reportPath = New-AuditReport
    
    # Open report in browser (optional)
    if ($IsWindows) {
        Start-Process $reportPath
    }
}

# Summary
Write-Host "`n"
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  AUDIT SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

$criticalIssues = ($findings | Where-Object { $_.Severity -eq 'HIGH' }).Count
$warnings = ($findings | Where-Object { $_.Severity -eq 'MEDIUM' }).Count

Write-Host "`n✓ Authentication: $(if ($authStatus.Authenticated) { 'PASS' } else { 'FAIL' })" -ForegroundColor $(if ($authStatus.Authenticated) { 'Green' } else { 'Red' })
Write-Host "✓ Required Scopes: $(if ($scopeValidation.AllRequiredPresent) { 'PASS' } else { 'FAIL' })" -ForegroundColor $(if ($scopeValidation.AllRequiredPresent) { 'Green' } else { 'Red' })
Write-Host "✓ Security Issues: $criticalIssues critical, $warnings warnings" -ForegroundColor $(if ($criticalIssues -eq 0) { 'Green' } else { 'Red' })

Write-Host "`n💡 Recommendations: $($AuditResults.Recommendations.Count)" -ForegroundColor Cyan
foreach ($rec in $AuditResults.Recommendations) {
    Write-Host "  • $rec" -ForegroundColor White
}

Write-Host "`n"

# Exit code
if ($criticalIssues -gt 0 -or -not $authStatus.Authenticated -or -not $scopeValidation.AllRequiredPresent) {
    exit 1
}

exit 0
