<#
.SYNOPSIS
    Setup Docker secrets for Wiley Widget

.DESCRIPTION
    Interactive script to create and configure Docker secrets files.
    Validates password strength and sets proper file permissions.

.PARAMETER Force
    Overwrite existing secret files

.EXAMPLE
    .\setup-secrets.ps1
    .\setup-secrets.ps1 -Force

.NOTES
    Author: Wiley Widget Team
    Date: November 14, 2025
#>

[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$secretsPath = Join-Path $PSScriptRoot "..\..\secrets"

function Test-PasswordStrength {
    param([string]$Password)
    
    if ($Password.Length -lt 8) {
        return @{ Valid = $false; Message = "Password must be at least 8 characters" }
    }
    
    if ($Password -notmatch "[A-Z]") {
        return @{ Valid = $false; Message = "Password must contain at least one uppercase letter" }
    }
    
    if ($Password -notmatch "[a-z]") {
        return @{ Valid = $false; Message = "Password must contain at least one lowercase letter" }
    }
    
    if ($Password -notmatch "[0-9]") {
        return @{ Valid = $false; Message = "Password must contain at least one number" }
    }
    
    if ($Password -notmatch "[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]") {
        return @{ Valid = $false; Message = "Password must contain at least one special character" }
    }
    
    return @{ Valid = $true; Message = "Password is strong" }
}

function Set-FilePermissions {
    param([string]$FilePath)
    
    try {
        # Windows: Set permissions to current user only
        $acl = Get-Acl $FilePath
        $acl.SetAccessRuleProtection($true, $false)  # Disable inheritance
        $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
            $env:USERNAME,
            "Read",
            "Allow"
        )
        $acl.SetAccessRule($rule)
        Set-Acl $FilePath $acl
        
        Write-Host "  ✓ Set restrictive permissions on $FilePath" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "  ⚠ Could not set permissions: $_" -ForegroundColor Yellow
        return $false
    }
}

# Main execution
try {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Wiley Widget Docker Secrets Setup" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    
    # Ensure secrets directory exists
    if (-not (Test-Path $secretsPath)) {
        New-Item -ItemType Directory -Path $secretsPath -Force | Out-Null
        Write-Host "✓ Created secrets directory" -ForegroundColor Green
    }
    
    # 1. Setup SA Password
    Write-Host "[1/3] SQL Server SA Password" -ForegroundColor Yellow
    Write-Host ""
    
    $saPasswordFile = Join-Path $secretsPath "sa_password.txt"
    
    if ((Test-Path $saPasswordFile) -and -not $Force) {
        Write-Host "  ⚠ SA password file already exists. Use -Force to overwrite." -ForegroundColor Yellow
    } else {
        do {
            $saPassword = Read-Host "  Enter SA password (min 8 chars, complex)" -AsSecureString
            $saPasswordPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
                [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($saPassword)
            )
            
            $validation = Test-PasswordStrength -Password $saPasswordPlain
            
            if (-not $validation.Valid) {
                Write-Host "  ✗ $($validation.Message)" -ForegroundColor Red
            }
        } while (-not $validation.Valid)
        
        Set-Content -Path $saPasswordFile -Value $saPasswordPlain -NoNewline
        Set-FilePermissions -FilePath $saPasswordFile
        Write-Host "  ✓ SA password saved" -ForegroundColor Green
    }
    
    Write-Host ""
    
    # 2. Setup Connection String
    Write-Host "[2/3] Connection String" -ForegroundColor Yellow
    Write-Host ""
    
    $connectionStringFile = Join-Path $secretsPath "connection_string.txt"
    
    if ((Test-Path $connectionStringFile) -and -not $Force) {
        Write-Host "  ⚠ Connection string file already exists. Use -Force to overwrite." -ForegroundColor Yellow
    } else {
        if (-not (Test-Path $saPasswordFile)) {
            Write-Host "  ✗ SA password not configured. Please run setup again." -ForegroundColor Red
            exit 1
        }
        
        $saPasswordPlain = Get-Content $saPasswordFile -Raw
        $connectionString = "Server=db;Database=WileyWidget;User Id=sa;Password=$saPasswordPlain;TrustServerCertificate=true;"
        
        Set-Content -Path $connectionStringFile -Value $connectionString -NoNewline
        Set-FilePermissions -FilePath $connectionStringFile
        Write-Host "  ✓ Connection string saved" -ForegroundColor Green
    }
    
    Write-Host ""
    
    # 3. Setup API Keys (optional)
    Write-Host "[3/3] API Keys (Optional)" -ForegroundColor Yellow
    Write-Host ""
    
    $apiKeysFile = Join-Path $secretsPath "api_keys"
    
    if ((Test-Path $apiKeysFile) -and -not $Force) {
        Write-Host "  ⚠ API keys file already exists. Use -Force to overwrite." -ForegroundColor Yellow
    } else {
        $setupApiKeys = Read-Host "  Configure API keys? (y/N)"
        
        if ($setupApiKeys -eq 'y' -or $setupApiKeys -eq 'Y') {
            Write-Host ""
            $qbClientId = Read-Host "  QuickBooks Client ID"
            $qbClientSecret = Read-Host "  QuickBooks Client Secret" -AsSecureString
            $qbClientSecretPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
                [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($qbClientSecret)
            )
            
            $apiKeysContent = @"
QUICKBOOKS_CLIENT_ID=$qbClientId
QUICKBOOKS_CLIENT_SECRET=$qbClientSecretPlain
"@
            
            Set-Content -Path $apiKeysFile -Value $apiKeysContent -NoNewline
            Set-FilePermissions -FilePath $apiKeysFile
            Write-Host "  ✓ API keys saved" -ForegroundColor Green
        } else {
            Write-Host "  ⊘ Skipped API keys configuration" -ForegroundColor Gray
        }
    }
    
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  Setup Complete!" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Verify secret files in: $secretsPath" -ForegroundColor White
    Write-Host "  2. Run: docker-compose -f docker-compose.yml -f docker-compose.prod-override.yml up" -ForegroundColor White
    Write-Host "  3. Backup secrets to secure location (NOT version control)" -ForegroundColor White
    Write-Host ""
    Write-Host "⚠ SECURITY WARNING: Never commit secret files to git!" -ForegroundColor Red
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "✗ Setup failed: $_" -ForegroundColor Red
    Write-Host ""
    exit 1
}
