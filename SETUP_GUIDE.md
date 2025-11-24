# WileyWidget - Complete Setup Guide

> **Last Updated**: November 23, 2025  
> **Version**: 1.0.0  
> **Target Framework**: .NET 9.0 + WinUI 3  
> **Status**: ✅ Build Verified | ⚠️ Requires Database + Secrets Setup

---

## 📋 Table of Contents

1. [Quick Start (5 Minutes)](#quick-start-5-minutes)
2. [Full Setup (45 Minutes)](#full-setup-45-minutes)
3. [Database Setup](#database-setup)
4. [Secrets Configuration](#secrets-configuration)
5. [QuickBooks Integration](#quickbooks-integration)
6. [AI Services Setup](#ai-services-setup)
7. [Testing](#testing)
8. [Troubleshooting](#troubleshooting)
9. [Production Deployment](#production-deployment)

---

## ✅ Pre-Setup Verification

### System Requirements

- **OS**: Windows 10 (1809+) or Windows 11
- **.NET SDK**: 9.0.306 or later ([Download](https://dot.net))
- **SQL Server**: Express 2019+ or Developer Edition ([Download](https://go.microsoft.com/fwlink/?linkid=866658))
- **PowerShell**: 7.4+ recommended ([Download](https://github.com/PowerShell/PowerShell/releases))
- **RAM**: 4GB minimum (8GB recommended)
- **Disk**: 2GB free space

### Verify Prerequisites

```powershell
# Check .NET SDK version
dotnet --version
# Expected: 9.0.306 or higher

# Check PowerShell version
$PSVersionTable.PSVersion
# Expected: 7.4.0 or higher

# Check SQL Server Express
Get-Service MSSQL$SQLEXPRESS
# Expected: Running (or Stopped - we'll start it)

# Check available memory
[Math]::Round((Get-CimInstance -ClassName Win32_OperatingSystem).FreePhysicalMemory / 1MB, 2)
# Expected: > 2GB free
```

---

## 🚀 Quick Start (5 Minutes)

This gets the application running in **demo mode** with stub services (no database required).

### Step 1: Build & Run

```powershell
# Navigate to project root
cd C:\Users\biges\Desktop\Wiley-Widget

# Clean build
dotnet clean WileyWidget.sln

# Build release
dotnet build WileyWidget.sln --configuration Release

# Run application
dotnet run --project src/WileyWidget.WinUI --configuration Release --no-build
```

**Expected Result**: Main window opens with dashboard layout. Data grids show placeholders (database not connected yet).

---

## 🔧 Full Setup (45 Minutes)

Complete setup with database, secrets, and external API integrations.

---

## 💾 Database Setup

### Option A: Automated Setup (Recommended)

```powershell
# Run automated setup script
.\scripts\setup-database.ps1

# Expected output:
# ✓ SQL Server Express service started
# ✓ Database 'WileyWidgetDev' created
# ✓ Connection verified
```

### Option B: Manual Setup

#### Step 1: Install SQL Server Express

```powershell
# Download SQL Server Express 2022
Start-Process "https://go.microsoft.com/fwlink/?linkid=866658"

# During installation:
# - Choose "Basic" installation
# - Accept default instance name: SQLEXPRESS
# - Enable Mixed Mode Authentication (optional)
```

#### Step 2: Verify SQL Server Installation

```powershell
# Check SQL Server service
$service = Get-Service -Name 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue

if ($service) {
    Write-Host "✓ SQL Server Express installed" -ForegroundColor Green
    Write-Host "Status: $($service.Status)" -ForegroundColor Cyan

    if ($service.Status -ne 'Running') {
        Start-Service 'MSSQL$SQLEXPRESS'
        Write-Host "✓ SQL Server Express started" -ForegroundColor Green
    }
} else {
    Write-Host "✗ SQL Server Express not found - install required" -ForegroundColor Red
}
```

#### Step 3: Test Database Connection

```powershell
# Test connection with sqlcmd (included with SQL Server)
sqlcmd -S .\SQLEXPRESS -Q "SELECT @@VERSION"

# Expected: Microsoft SQL Server 2019/2022 version info
```

#### Step 4: Apply EF Core Migrations

```powershell
# Navigate to project root
cd C:\Users\biges\Desktop\Wiley-Widget

# Install EF Core tools (if not already installed)
dotnet tool install --global dotnet-ef --version 9.0.0

# Apply migrations to create database schema
dotnet ef database update --project src/WileyWidget.Data --startup-project src/WileyWidget.WinUI

# Expected output:
# Applying migration '20251028174500_AddBudgetPeriodSeed'
# Applying migration '20251116_AddMunicipalAccountsSeed'
# ... (30+ migrations)
# Done.
```

#### Step 5: Verify Database Creation

```powershell
# Query database to verify seed data
sqlcmd -S .\SQLEXPRESS -d WileyWidgetDev -Q "SELECT COUNT(*) as TableCount FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'"

# Expected: 15+ tables

# Verify seed data
sqlcmd -S .\SQLEXPRESS -d WileyWidgetDev -Q "SELECT COUNT(*) FROM Departments"
# Expected: 8 departments

sqlcmd -S .\SQLEXPRESS -d WileyWidgetDev -Q "SELECT COUNT(*) FROM BudgetEntries"
# Expected: 20 budget entries
```

### Database Seed Data Summary

After migrations, your database contains:

| Table                   | Records | Purpose                                   |
| ----------------------- | ------- | ----------------------------------------- |
| **BudgetPeriods**       | 2       | FY2025 Adopted, FY2026 Proposed           |
| **Departments**         | 8       | Admin, Public Works, Utilities, etc.      |
| **Funds**               | 6       | General, Enterprise, Utility, etc.        |
| **Vendors**             | 3       | Sample vendors for testing                |
| **MunicipalAccounts**   | 31      | Conservation Trust Fund chart of accounts |
| **BudgetEntries**       | 20      | FY2026 General Fund revenues              |
| **TaxRevenueSummaries** | 7       | Property tax revenue projections          |
| **AppSettings**         | 1       | Default application settings              |

---

## 🔐 Secrets Configuration

WileyWidget uses **DPAPI-encrypted secret storage** for production-grade security.

### Architecture Overview

```
Environment Variables (Migration Source)
           ↓
    Encrypted Vault
    (%APPDATA%\WileyWidget\Secrets\)
           ↓
    Application Services
```

### Option A: Interactive Setup (Recommended)

```powershell
# Run interactive secrets setup wizard
.\scripts\maintenance\setup-secrets.ps1

# Follow prompts to configure:
# 1. SQL Server SA password (if using SQL auth)
# 2. API keys (QuickBooks, AI services)
# 3. License keys (Syncfusion, Bold Reports)
```

### Option B: Environment Variables (Development)

```powershell
# QuickBooks Integration (Optional)
[System.Environment]::SetEnvironmentVariable('QBO_CLIENT_ID', 'YOUR_CLIENT_ID', 'User')
[System.Environment]::SetEnvironmentVariable('QBO_CLIENT_SECRET', 'YOUR_CLIENT_SECRET', 'User')
[System.Environment]::SetEnvironmentVariable('QBO_REDIRECT_URI', 'http://localhost:8080/callback', 'User')
[System.Environment]::SetEnvironmentVariable('QBO_ENVIRONMENT', 'sandbox', 'User')

# AI Services (Optional)
[System.Environment]::SetEnvironmentVariable('XAI_API_KEY', 'xai-YOUR_KEY', 'User')
[System.Environment]::SetEnvironmentVariable('OPENAI_API_KEY', 'sk-YOUR_KEY', 'User')

# CRITICAL: Restart terminal/application after setting variables
```

### Secret Migration Process

On first launch, the application automatically migrates environment variables to encrypted storage:

1. **Detection**: Scans for whitelisted environment variables
2. **Encryption**: Uses DPAPI with SHA-256 filename hashing
3. **Storage**: Saves to `%APPDATA%\WileyWidget\Secrets\`
4. **Validation**: Verifies entropy file integrity

### Verify Secrets Setup

```powershell
# Check encrypted secrets directory
Test-Path "$env:APPDATA\WileyWidget\Secrets"
# Expected: True

# List encrypted secret files (filenames are hashed)
Get-ChildItem "$env:APPDATA\WileyWidget\Secrets" | Select-Object Name, Length, LastWriteTime

# Example output:
# Name                                                Length LastWriteTime
# ----                                                ------ -------------
# .entropy                                            128    11/23/2025 10:30 AM
# [hashed secret files with .secret extension]
```

### Secrets Security Features

- ✅ **DPAPI Encryption**: User-scoped, machine-bound AES-256 encryption
- ✅ **SHA-256 Hashing**: Obfuscated secret filenames
- ✅ **Entropy Protection**: Machine-bound entropy file with tampering detection
- ✅ **Auto-Migration**: Automatic environment variable migration on first run
- ✅ **Audit Logging**: All secret operations logged securely

### Backup Entropy File (CRITICAL for Production)

```powershell
# Backup entropy file to secure location
Copy-Item "$env:APPDATA\WileyWidget\Secrets\.entropy" -Destination "C:\SecureBackup\wiley-entropy-$(Get-Date -Format 'yyyyMMdd').bak"

# WARNING: Without this file, you cannot decrypt existing secrets after system reinstall
```

---

## 💼 QuickBooks Integration

### Prerequisites

1. **Intuit Developer Account**: https://developer.intuit.com/
2. **Sandbox Company**: Create at https://developer.intuit.com/app/developer/myapps

### Setup Process

#### Step 1: Create QuickBooks App

1. Login to https://developer.intuit.com/app/developer/dashboard
2. Click **Create an app** → **QuickBooks Online**
3. Configure:
   - **App Name**: WileyWidget Development
   - **Redirect URIs**:
     - `https://developer.intuit.com/v2/OAuth2Playground/RedirectUrl`
     - `http://localhost:8080/callback`
   - **Scopes**: `com.intuit.quickbooks.accounting`

4. Copy credentials:
   - **Client ID**: `ABWlf3T7raiKwVV8ILahdlGP7E5pblC6pH1i6lXZQoU6wloEOm` (example)
   - **Client Secret**: (keep secure)

#### Step 2: Configure Environment

```powershell
# Set QuickBooks credentials
[System.Environment]::SetEnvironmentVariable('QBO_CLIENT_ID', 'YOUR_CLIENT_ID', 'User')
[System.Environment]::SetEnvironmentVariable('QBO_CLIENT_SECRET', 'YOUR_CLIENT_SECRET', 'User')
[System.Environment]::SetEnvironmentVariable('QBO_REDIRECT_URI', 'https://developer.intuit.com/v2/OAuth2Playground/RedirectUrl', 'User')
[System.Environment]::SetEnvironmentVariable('QBO_ENVIRONMENT', 'sandbox', 'User')
```

#### Step 3: Run OAuth Setup Script

```powershell
# Automated OAuth flow
.\scripts\quickbooks\setup-oauth.ps1

# Expected output:
# [1/4] Building authorization URL...
# [2/4] Opening browser for authorization...
# [3/4] Waiting for authorization code...
# [4/4] Exchanging code for tokens...
# ✓ QuickBooks connected successfully!
# ✓ Tokens saved to settings
# ✓ Realm ID: 9341455168020461
```

#### Step 4: Test Connection

```powershell
# Test QuickBooks API connectivity
.\scripts\quickbooks\test-quickbooks-connection.ps1

# Expected output:
# ✓ Access token valid (expires: 2025-11-23 11:30:00)
# ✓ Company info retrieved: Town of Wiley Sandbox
# ✓ Customers count: 5
# ✓ Invoices count: 10
```

### QuickBooks Token Management

- **Access Token Lifetime**: 1 hour
- **Refresh Token Lifetime**: ~100 days
- **Auto-Refresh**: Enabled (automatic before expiry)
- **Token Storage**: `%APPDATA%\WileyWidget\settings.json`

### Token Refresh Process

```powershell
# Manual token refresh (if needed)
# Launch app and navigate to QuickBooks view → Click "Refresh Token"

# Or via script:
.\scripts\quickbooks\refresh-token.ps1
```

---

## 🤖 AI Services Setup

### XAI (Grok) Integration

#### Step 1: Get API Key

1. Visit: https://x.ai/api
2. Sign up and create API key
3. Copy key (format: `xai-...`)

#### Step 2: Configure Environment

```powershell
[System.Environment]::SetEnvironmentVariable('XAI_API_KEY', 'xai-YOUR_KEY', 'User')
[System.Environment]::SetEnvironmentVariable('XAI_BASE_URL', 'https://api.x.ai/v1/', 'User')
```

#### Step 3: Test Integration

```powershell
# Test XAI connectivity
.\scripts\test-ai-integration.ps1 -Provider XAI

# Expected output:
# ✓ XAI API key configured
# ✓ Model: grok-4-0709
# ✓ Test query: "Calculate 2+2"
# ✓ Response: "The sum of 2 + 2 is 4."
```

### OpenAI Integration (Alternative)

```powershell
# Configure OpenAI instead of XAI
[System.Environment]::SetEnvironmentVariable('OPENAI_API_KEY', 'sk-YOUR_KEY', 'User')

# Test OpenAI connectivity
.\scripts\test-ai-integration.ps1 -Provider OpenAI
```

### AI Service Fallback Chain

```
Primary: XAI (Grok-4) → Fallback: OpenAI (GPT-4) → Final Fallback: NullAIService (Mock)
```

---

## ✅ Complete Setup Verification

After completing all setup steps, run this comprehensive verification:

```powershell
# Run setup verification script
.\scripts\verify-setup.ps1

# Expected output:
# [✓] .NET SDK 9.0.306 installed
# [✓] SQL Server Express running
# [✓] Database 'WileyWidgetDev' exists (15 tables)
# [✓] Syncfusion license configured
# [✓] Secrets vault initialized (5 secrets)
# [✓] QuickBooks tokens valid
# [✓] XAI API key configured
# [✓] Build successful (0 errors)
#
# Setup Status: COMPLETE ✓
```

### Launch Application

```powershell
# Start development environment with cleanup
python scripts\setup\dev-start.py

# OR manual launch
dotnet run --project src/WileyWidget.WinUI --configuration Release --no-build
```

### Expected Application State

| Feature          | Status        | Verification                          |
| ---------------- | ------------- | ------------------------------------- |
| **Main Window**  | ✅ Loads      | Window title shows "Wiley Widget"     |
| **Dashboard**    | ✅ Visible    | Budget overview panels populate       |
| **Navigation**   | ✅ Works      | Sidebar navigation switches views     |
| **Database**     | ✅ Connected  | Budget entries visible in DataGrid    |
| **QuickBooks**   | ✅ Integrated | Sync button shows "Connected" status  |
| **AI Assistant** | ✅ Active     | Chat input shows Grok model name      |
| **Reports**      | ✅ Available  | Report menu shows data-driven reports |

---

## 🧪 Testing

### Run Unit Tests

```powershell
# All unit tests
dotnet test WileyWidget.sln --filter "Category=Unit" --logger "console;verbosity=detailed"

# Expected: 100+ tests passing
# Coverage: 70%+
```

### Run Integration Tests

```powershell
# Integration tests (requires database)
dotnet test WileyWidget.sln --filter "Category=Integration" --collect:"XPlat Code Coverage"

# Expected: 30+ tests passing
```

### Run UI Tests

```powershell
# UI smoke tests with cleanup
.\scripts\test-stafact-with-cleanup.ps1

# Expected: FlaUI tests pass, orphaned processes cleaned
```

### Docker-Based Robust Testing

```powershell
# Run xUnit tests in Docker container
docker run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:9.0 `
  bash -c "dotnet test --collect:'XPlat Code Coverage' --results-directory:/src/coverage"

# Generate coverage report
reportgenerator -reports:coverage/**/coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:Html
start CoverageReport/index.html
```

---

## 🔧 Troubleshooting

### Database Issues

#### Error: "Cannot connect to .\SQLEXPRESS"

**Cause**: SQL Server service not running

**Fix**:

```powershell
Start-Service 'MSSQL$SQLEXPRESS'
Get-Service 'MSSQL$SQLEXPRESS' | Select-Object Status, StartType
```

#### Error: "Database 'WileyWidgetDev' does not exist"

**Cause**: EF Core migrations not applied

**Fix**:

```powershell
dotnet ef database update --project src/WileyWidget.Data --startup-project src/WileyWidget.WinUI
```

#### Error: "Login failed for user"

**Cause**: Windows Authentication issue

**Fix**:

```powershell
# Verify current Windows user
whoami

# Test SQL Server authentication
sqlcmd -S .\SQLEXPRESS -E -Q "SELECT SUSER_NAME()"

# If fails, check SQL Server configuration:
# - Open SQL Server Configuration Manager
# - Enable TCP/IP protocol
# - Restart SQL Server service
```

### Secrets Issues

#### Error: "Entropy validation failed"

**Cause**: Corrupted or missing entropy file

**Fix**:

```powershell
# Delete corrupted entropy file
Remove-Item "$env:APPDATA\WileyWidget\Secrets\.entropy" -Force

# Restart application (new entropy generated automatically)
```

#### Error: "Secret not found"

**Cause**: Environment variable not migrated

**Fix**:

```powershell
# Re-set environment variable
[System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'YOUR_KEY', 'User')

# Delete settings to force re-migration
Remove-Item "$env:APPDATA\WileyWidget\settings.json" -Force

# Restart application
```

### QuickBooks Issues

#### Error: "redirect_uri is invalid"

**Cause**: Redirect URI mismatch in Intuit Developer Portal

**Fix**:

1. Check exact redirect URI in setup script output
2. Add to Intuit Developer Portal → Keys & OAuth → Redirect URIs
3. Ensure exact match (including trailing slash)

#### Error: "Access token invalid"

**Cause**: Token expired or revoked

**Fix**:

```powershell
# Delete tokens to force re-authorization
Remove-Item "$env:APPDATA\WileyWidget\settings.json" -Force

# Re-run OAuth setup
.\scripts\quickbooks\setup-oauth.ps1
```

### Build Issues

#### Error: "MSB3026: File is locked by another process"

**Cause**: Orphaned processes holding file locks

**Fix**:

```powershell
# Kill all WileyWidget processes
Get-Process | Where-Object {$_.ProcessName -match 'WileyWidget|testhost|vstest'} | Stop-Process -Force

# Clean build directories
dotnet clean WileyWidget.sln
Remove-Item -Recurse -Force bin, obj -ErrorAction SilentlyContinue

# Rebuild
dotnet build WileyWidget.sln --configuration Release
```

#### Error: "Package restore failed"

**Cause**: NuGet cache corruption

**Fix**:

```powershell
# Clear NuGet caches
dotnet nuget locals all --clear

# Restore packages
dotnet restore WileyWidget.sln --force
```

### Runtime Issues

#### Error: "FileNotFoundException: System.Text.Json"

**Cause**: Assembly binding issue

**Fix**: Already handled by `App.xaml.cs` assembly resolution. If still occurs:

```powershell
# Verify CopyLocalLockFileAssemblies
grep -r "CopyLocalLockFileAssemblies" src/WileyWidget.WinUI/WileyWidget.WinUI.csproj

# Should be: <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
```

#### Application crashes on startup

**Cause**: Theme not applied before region adapter registration

**Fix**: Already resolved in `App.Lifecycle.cs` (Phase 1 theme application). If still occurs:

```powershell
# Check logs for theme errors
Get-Content "$env:APPDATA\WileyWidget\logs\startup-*.txt" | Select-String "theme|region"
```

---

## 🚀 Production Deployment

### Build Release Package

```powershell
# Self-contained executable (no .NET runtime required on target)
dotnet publish src/WileyWidget.WinUI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Output location:
# src/WileyWidget.WinUI/bin/Release/net8.0-windows10.0.19041.0/win10-x64/publish/WileyWidget.WinUI.exe
```

### MSIX Packaging (Microsoft Store / Sideloading)

```powershell
# Build MSIX package
msbuild src/WileyWidget.WinUI/WileyWidget.WinUI.csproj `
  /t:Publish `
  /p:Configuration=Release `
  /p:Platform=x64 `
  /p:AppxBundle=Always `
  /p:AppxBundlePlatforms=x64 `
  /p:UapAppxPackageBuildMode=SideloadOnly

# Output: src/WileyWidget.WinUI/AppPackages/
```

### Production Secrets Configuration

```powershell
# Production environment variables (set on target machine)
[System.Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Production', 'Machine')

# Use secure secret storage (Azure Key Vault recommended)
# Configure in config/production/appsettings.Production.json
```

### Production Checklist

- [ ] SQL Server connection string uses production database
- [ ] All environment variables set at **Machine** scope (not User)
- [ ] Entropy file backed up to secure location
- [ ] QuickBooks using **production** environment (not sandbox)
- [ ] AI API keys configured for production quotas
- [ ] Telemetry enabled (Application Insights or SigNoz)
- [ ] Logging configured for production (reduced verbosity)
- [ ] HTTPS certificate configured (if using web APIs)
- [ ] Code signing certificate applied to executable
- [ ] Auto-updater configured (optional)

---

## 📚 Additional Resources

### Documentation

- **Project Plan**: `.vscode/project-plan.md`
- **Architecture**: `docs/ARCHITECTURE.md`
- **Database Schema**: `docs/database-setup.md`
- **Testing Strategy**: `docs/TESTING.md`
- **CI/CD Pipeline**: `docs/cicd-management-guide.md`

### Scripts Reference

| Script                                  | Purpose                          |
| --------------------------------------- | -------------------------------- |
| `scripts/setup-database.ps1`            | Automated database setup         |
| `scripts/maintenance/setup-secrets.ps1` | Secrets configuration wizard     |
| `scripts/quickbooks/setup-oauth.ps1`    | QuickBooks OAuth setup           |
| `scripts/verify-setup.ps1`              | Comprehensive setup verification |
| `scripts/setup/dev-start.py`            | Development environment launcher |
| `scripts/test-stafact-with-cleanup.ps1` | UI tests with cleanup            |

### Support

- **Issues**: https://github.com/Bigessfour/Wiley-Widget/issues
- **Discussions**: https://github.com/Bigessfour/Wiley-Widget/discussions
- **License**: MIT License (see LICENSE file)

---

## 🎯 Next Steps

After completing this setup:

1. **Explore Features**: Navigate through Dashboard, Budget Management, Reports
2. **Import Data**: Use Excel Import feature to load real budget data
3. **Setup QuickBooks**: Configure QuickBooks integration for financial sync
4. **Customize Settings**: Configure themes, fiscal year, preferences
5. **Run Reports**: Generate budget variance and financial reports
6. **Test AI Assistant**: Use Grok-powered financial insights

---

**Setup Complete! 🎉**

Your WileyWidget application is now fully configured and ready for production use.
