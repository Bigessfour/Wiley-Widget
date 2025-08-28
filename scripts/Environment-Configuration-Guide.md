# Wiley Widget Environment Configuration Guide

## Overview

This guide explains how to use the comprehensive environment configuration system for Wiley Widget development, built following Microsoft PowerShell 7.5.2 best practices and approved verbs.

## Environment Variables (.env File)

The `.env` file contains all common variables organized by category:

### Project Configuration
- `WILEY_WIDGET_ROOT` - Project root directory
- `WILEY_WIDGET_CONFIG` - Build configuration (Release/Debug)
- `WILEY_WIDGET_VERSION` - Project version
- `WILEY_WIDGET_FRAMEWORK` - Target framework

### PowerShell 7.5.2 Configuration
- `POWERSHELL_*` - PowerShell-specific settings
- `POWERSHELL_PROFILE_*` - Profile path configurations
- `POWERSHELL_TELEMETRY_OPTOUT` - Telemetry settings

### .NET Development
- `DOTNET_*` - .NET SDK and runtime settings
- `NUGET_*` - NuGet package management
- `MSBUILDDEBUGPATH` - MSBuild debugging output

### Build and Testing
- `BUILD_*` - Build configuration
- `TEST_*` - Test settings and coverage
- `RUN_UI_TESTS` - UI test execution control

### Development Tools
- `PATH` - Extended PATH for tools
- `GIT_*` - Git configuration
- `VSCODE_*` - Visual Studio Code settings

## PowerShell Profile Functions

### Environment Management
```powershell
# Load environment configuration
Set-WileyWidgetEnvironment

# Load with validation
Set-WileyWidgetEnvironment -ValidateEnvironment

# Force reload
Set-WileyWidgetEnvironment -Force

# Validate environment
Test-WileyWidgetEnvironment
Test-WileyWidgetEnvironment -Detailed
```

### Build and Test
```powershell
# Build project
Invoke-WileyWidgetBuild
Invoke-WileyWidgetBuild -Configuration Debug

# Run tests
Invoke-WileyWidgetTest
Invoke-WileyWidgetTest -IncludeUITests

# Start application
Start-WileyWidgetApplication
```

### Information and Diagnostics
```powershell
# Show project information
Get-WileyWidgetProjectInfo

# Check Syncfusion license
Show-WileyWidgetLicenseStatus

# Edit environment file
Edit-WileyWidgetEnvironment
```

## Quick Commands (Aliases)

The profile provides convenient aliases:

```powershell
ww-build     # Build the project
ww-test      # Run tests
ww-run       # Start the application
ww-info      # Show project information
ww-license   # Check Syncfusion license
ww-env       # Set environment
ww-edit-env  # Edit environment configuration
ww-reset     # Reset development environment
```

## Configuration Categories

### 1. Project Configuration
Core project settings and paths.

### 2. PowerShell 7.5.2 Configuration
PowerShell-specific settings following Microsoft best practices.

### 3. .NET Development Environment
.NET SDK, NuGet, and MSBuild configuration.

### 4. Build and Testing Configuration
Build settings, test configuration, and coverage requirements.

### 5. Development Tools and Paths
Tool paths, Git configuration, and editor settings.

### 6. Syncfusion Configuration
Syncfusion package versions and licensing.

### 7. Logging and Diagnostics
Logging configuration and diagnostic settings.

### 8. Security and Compliance
Security settings and compliance requirements.

### 9. Performance and Optimization
Performance tuning and optimization settings.

### 10. Integration and Deployment
CI/CD, cloud, and deployment configurations.

## Best Practices

### Environment Variable Naming
- Use UPPER_CASE with underscores
- Prefix with `WILEY_WIDGET_` for project-specific variables
- Follow Microsoft naming conventions

### PowerShell Functions
- Use approved verbs (Get, Set, Test, Invoke, etc.)
- Use camelCase for parameter names
- Include comprehensive help documentation
- Use CmdletBinding for advanced functions

### Error Handling
- Use try/catch blocks for error-prone operations
- Provide meaningful error messages
- Use Write-Verbose for detailed information
- Use Write-Warning for non-critical issues

### Validation
- Validate environment on load
- Check for required tools and versions
- Provide automatic fixes when possible
- Report validation results clearly

## Customization

### Adding New Environment Variables
1. Add to `.env` file with appropriate category
2. Update profile functions if needed
3. Document in this guide
4. Test with validation function

### Creating New Functions
1. Use approved PowerShell verbs
2. Follow camelCase naming
3. Include comprehensive help
4. Add to aliases if appropriate
5. Update this documentation

## Troubleshooting

### Environment Not Loading
```powershell
# Check if .env file exists
Test-Path .env

# Manually load environment
Set-WileyWidgetEnvironment -Force

# Validate environment
Test-WileyWidgetEnvironment -Detailed
```

### Profile Not Working
```powershell
# Check PowerShell version
$PSVersionTable.PSVersion

# Reload profile
. .\WileyWidget.Profile.ps1

# Check for errors
Get-Error
```

### Build Issues
```powershell
# Check environment variables
Get-ChildItem Env:WILEY_WIDGET_*

# Validate build environment
Test-WileyWidgetEnvironment

# Check build paths
Get-WileyWidgetProjectInfo
```

## Microsoft Docs MCP Compliance

This configuration follows Microsoft PowerShell 7.5.2 best practices:

- ✅ Approved verbs and camelCase naming
- ✅ Comprehensive error handling
- ✅ Detailed help documentation
- ✅ Environment variable expansion
- ✅ Proper profile organization
- ✅ Validation and health checks
- ✅ Security and telemetry considerations
- ✅ Module-like export structure

## Version Information

- Profile Version: 1.0.0
- PowerShell Version: 7.5.2
- .NET SDK: 9.0.x
- Last Updated: Based on Microsoft Docs MCP recommendations
