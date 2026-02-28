# Wiley Widget Development Scripts (PowerShell Local)

This directory contains **PowerShell-based development scripts** for local desktop development. All scripts are designed for Windows environments with .NET/SQL Express, no cloud dependencies.

## 🪟 PowerShell Script Ecosystem

### Core Development Workflow

The PowerShell scripts handle local development tasks:

#### `build.ps1` - Build Script

**Purpose**: Compile and package the application

- **Clean Build**: `dotnet clean` then `dotnet build`
- **Release Mode**: Supports debug/release configurations
- **Self-Contained**: Packages as single EXE for distribution

#### `test.ps1` - Test Runner

**Purpose**: Execute unit and integration tests

- **xUnit Execution**: Runs all test projects
- **Coverage**: Optional coverage reporting
- **Filter Support**: Run specific test categories

#### `setup-database.ps1` - Database Setup

**Purpose**: Initialize local SQL Express database

- **DB Creation**: Creates WileyWidget database
- **Migrations**: Applies EF Core migrations
- **Seed Data**: Populates initial data

#### `quickbooks/setup-oauth.ps1` - QuickBooks OAuth Setup

**Purpose**: Configure desktop OAuth2 for QuickBooks sync

- **Browser Auth**: Launches OAuth flow in default browser
- **Token Storage**: Saves encrypted tokens to %APPDATA%
- **DPAPI Encryption**: Uses Windows Data Protection API

#### `cleanup.ps1` - Process Cleanup

**Purpose**: Clean up development artifacts

- **Orphan Processes**: Kills hanging .NET processes
- **Temp Files**: Removes build artifacts
- **Logs**: Clears old log files

## 📊 PowerShell Best Practices

### ✅ Windows Standards Compliance

- **Native Windows**: Uses PowerShell 7+ idioms and cmdlets
- **Maintainable**: Functions with param blocks and validation
- **Error Handling**: Try/catch with proper error actions
- **Documented**: Comment-based help and examples
- **PSScriptAnalyzer**: Passes linting rules

### ✅ Local Desktop Alignment

- **PowerShell Usage**: Local automation (dotnet, sqlcmd, browser launch)
- **Security**: DPAPI for secrets, no cloud exposure
- **Resource Management**: Process cleanup and file management
- **Performance**: Efficient .NET CLI calls
- **Monitoring**: Write-Host for status, structured logging

### ✅ Desktop Focus Benefits

**Approach**: PowerShell-first for Windows desktop development
- No cross-platform needs (desktop-only app)
- Direct .NET/SQL integration
- Simple, maintainable scripts
- VS Code tasks integration

## 🧹 Script Organization

### Active Scripts

- `build.ps1` - Build and package
- `test.ps1` - Run tests
- `setup-database.ps1` - DB initialization
- `quickbooks/setup-oauth.ps1` - OAuth setup
- `cleanup.ps1` - Maintenance

### VS Code Integration

Scripts integrate with VS Code tasks (see .vscode/tasks.json):

- `WileyWidget: Build` → `build.ps1`
- `test` → `test.ps1`
- Custom tasks for DB setup and cleanup

## 🧪 Testing Scripts

Scripts include basic validation but rely on xUnit for app testing.

### Validation Examples

```powershell
# build.ps1 validation
if (-not (Test-Path "src/WileyWidget.WinForms/WileyWidget.WinForms.csproj")) {
    throw "Project file not found"
}
```

### Running Scripts

```powershell
# Build release
.\scripts\build.ps1 -Configuration Release

# Run tests
.\scripts\test.ps1 -Filter "QuickBooks*"

# Setup DB
.\scripts\setup-database.ps1
```

## 📈 PowerShell Benefits

| Aspect           | PowerShell Scripts  | Benefits              |
| ---------------- | ------------------- | --------------------- |
| Platform Support | Windows-native      | Optimized for desktop |
| Maintainability  | PSScriptAnalyzer    | Linting and standards |
| Testing          | Pester framework    | PowerShell testing    |
| Error Handling   | Try/catch           | Structured exceptions |
| Documentation    | Comment-based help  | Get-Help integration  |
| Performance      | Direct .NET calls   | No overhead           |
| Security         | DPAPI integration   | Windows security      |
| Monitoring       | Write-Host/Verbose  | Console output        |

## 🔧 Development Guidelines

- **PowerShell First**: All scripts use PowerShell 7+ idioms
- **Modular Design**: Functions with param validation
- **Error Handling**: Use try/catch with proper exit codes
- **Logging**: Write-Verbose for debug, Write-Host for status
- **Testing**: Manual validation; app tests via xUnit
- **Documentation**: Update this README for any new scripts

## 📚 Related Documentation

- [.NET CLI Docs](https://learn.microsoft.com/dotnet/core/tools/)
- [PowerShell Docs](https://learn.microsoft.com/powershell/)
- [Syncfusion Licensing](https://help.syncfusion.com/windowsforms/licensing/)

