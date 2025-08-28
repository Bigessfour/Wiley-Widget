# Wiley Widget Development Environment

This repository contains the complete development environment setup for the Wiley Widget project, following Microsoft PowerShell 7.5.2 and MCP (Model Context Protocol) best practices.

## 📋 Overview

The Wiley Widget project uses a modern development stack with PowerShell 7.5.2, VS Code, and MCP servers for AI-assisted development. This setup ensures consistent, maintainable, and high-quality code across the team.

## 🛠️ Development Tools

### Core Technologies
- **PowerShell 7.5.2** - Primary scripting and automation language
- **.NET 8.0** - Framework for WPF application development
- **VS Code** - Primary IDE with PowerShell and MCP extensions
- **MCP Servers** - GitHub integration for AI-assisted development

### PowerShell Modules
- **PSScriptAnalyzer 1.24.0** - Code analysis and quality assurance
- **Pester 5.5.0** - Testing framework
- **platyPS** - PowerShell help documentation generation
- **PowerShellGet** - Module management and installation

### VS Code Extensions
- **PowerShell Extension** - Language support and debugging
- **GitHub Copilot** - AI-assisted code completion
- **MCP (Model Context Protocol)** - AI server integration
- **C# Extensions** - .NET development support

## 🚀 Quick Start

### Prerequisites
1. **Windows 10/11** with administrator privileges
2. **PowerShell 7.5.2** installed from [Microsoft Store](https://www.microsoft.com/store/productId/9MZ1SNWT0N5D) or [GitHub releases](https://github.com/PowerShell/PowerShell/releases)
3. **VS Code** installed from [official site](https://code.visualstudio.com/)
4. **Node.js 18+** for MCP servers
5. **.NET 8.0 SDK** for WPF development

### Automated Setup

Run the automated setup script:

```powershell
# Navigate to the scripts directory
cd .\scripts

# Run the setup script
.\Setup-DevelopmentEnvironment.ps1
```

This script will:
- ✅ Install all required PowerShell modules
- ✅ Configure VS Code with extensions and settings
- ✅ Set up MCP servers for GitHub integration
- ✅ Configure environment variables
- ✅ Set up PowerShell profile
- ✅ Configure code quality tools (PSScriptAnalyzer, Pester)

### Manual Setup (Alternative)

If you prefer manual setup:

1. **Install PowerShell Modules:**
   ```powershell
   Install-Module -Name PSScriptAnalyzer -MinimumVersion 1.24.0 -Force
   Install-Module -Name Pester -MinimumVersion 5.5.0 -Force
   Install-Module -Name platyPS -Force
   ```

2. **Install VS Code Extensions:**
   ```powershell
   code --install-extension ms-vscode.powershell
   code --install-extension GitHub.copilot
   code --install-extension ms-vscode.vscode-json
   ```

3. **Install MCP Servers:**
   ```powershell
   npm install -g @modelcontextprotocol/server-github
   npm install -g @modelcontextprotocol/server-filesystem
   ```

## 📊 Tool Management

### Validate Development Environment

```powershell
# Validate all tools are properly installed and configured
.\Manage-DevelopmentTools.ps1 -Action Validate
```

### Update Development Tools

```powershell
# Update all tools to latest versions
.\Manage-DevelopmentTools.ps1 -Action Update -Force
```

### Generate Environment Report

```powershell
# Generate comprehensive report of development environment
.\Manage-DevelopmentTools.ps1 -Action Report
```

## 🔧 Configuration Files

### Development Tools Manifest
- **File:** `Development-Tools-Manifest.psd1`
- **Purpose:** Comprehensive inventory of all development tools
- **Usage:** Referenced by management scripts for validation and updates

### PSScriptAnalyzer Settings
- **File:** `scripts/PSScriptAnalyzerSettings.psd1`
- **Purpose:** Code quality rules and formatting standards
- **Usage:** Automatically applied by VS Code and analysis scripts

### Pester Configuration
- **File:** `scripts/PesterConfiguration.psd1`
- **Purpose:** Testing framework configuration
- **Usage:** Used by test execution and CI/CD pipelines

### VS Code Settings
- **File:** `%APPDATA%\Code\User\settings.json`
- **Purpose:** IDE configuration for PowerShell development
- **Usage:** Applied automatically during setup

## 🧪 Testing

### Run All Tests

```powershell
# Run all tests with coverage
Invoke-Pester -Configuration .\scripts\PesterConfiguration.psd1
```

### Run Specific Test Categories

```powershell
# Run only unit tests
Invoke-Pester -Tag 'Unit'

# Run integration tests
Invoke-Pester -Tag 'Integration'
```

### Generate Test Report

```powershell
# Generate detailed test report
Invoke-Pester -Configuration .\scripts\PesterConfiguration.psd1 -PassThru |
    Export-PesterResults -OutputFormat NUnitXml -OutputPath .\TestResults\test-results.xml
```

## 📈 Code Quality

### Run Code Analysis

```powershell
# Analyze all PowerShell files
Invoke-ScriptAnalyzer -Path .\scripts -Settings .\scripts\PSScriptAnalyzerSettings.psd1 -Recurse
```

### Format Code

```powershell
# Format all PowerShell files according to standards
Get-ChildItem -Path .\scripts -Filter *.ps1 -Recurse |
    ForEach-Object {
        Invoke-Formatter -ScriptDefinition (Get-Content $_.FullName -Raw) |
            Out-File $_.FullName -Encoding UTF8
    }
```

## 🔒 Security Best Practices

### Environment Variables
- Store sensitive data in environment variables, not code
- Use `PSCredential` objects for authentication
- Never commit secrets to version control

### Code Security
- Use `PSScriptAnalyzer` to detect security issues
- Follow principle of least privilege
- Validate all inputs and sanitize outputs

## 📚 Documentation

### Generate Module Help

```powershell
# Generate help documentation for PowerShell modules
New-MarkdownHelp -Module WileyWidget -OutputFolder .\docs\help
```

### Update Documentation

```powershell
# Update existing help documentation
Update-MarkdownHelp -Path .\docs\help
```

## 🚦 CI/CD Integration

### GitHub Actions
The project includes GitHub Actions workflows for:
- Automated testing on pull requests
- Code quality analysis
- Documentation generation
- Release automation

### Local CI Simulation

```powershell
# Run complete CI pipeline locally
.\scripts\Run-CI.ps1
```

## 🐛 Troubleshooting

### Common Issues

**PowerShell Module Installation Fails:**
```powershell
# Set execution policy
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

# Update PowerShellGet
Install-Module PowerShellGet -Force -AllowClobber
```

**VS Code Extensions Not Installing:**
```powershell
# Clear VS Code extension cache
code --list-extensions --show-versions
# Reinstall manually if needed
```

**MCP Server Connection Issues:**
```powershell
# Test MCP server connectivity
.\Test-GitHub-MCP.ps1
```

### Getting Help

1. Check the [troubleshooting guide](./docs/troubleshooting.md)
2. Review [Microsoft PowerShell documentation](https://docs.microsoft.com/powershell/)
3. Check [MCP documentation](https://modelcontextprotocol.io/)
4. Open an issue in the project repository

## 📋 Best Practices

### PowerShell Development
- Use PowerShell 7.5.2 features and syntax
- Follow [PowerShell scripting best practices](https://docs.microsoft.com/powershell/scripting/learn/ps101/01-getting-started)
- Use PSScriptAnalyzer for code quality
- Write comprehensive Pester tests

### Version Control
- Use descriptive commit messages
- Follow semantic versioning
- Keep sensitive data out of version control
- Use `.gitignore` for build artifacts

### Documentation
- Keep README files up to date
- Document all public functions with comment-based help
- Use consistent formatting and style
- Include code examples and usage scenarios

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes following the established patterns
4. Add tests for new functionality
5. Run the full test suite and code analysis
6. Submit a pull request

### Development Workflow

```powershell
# 1. Create feature branch
git checkout -b feature/new-feature

# 2. Make changes and test
# ... development work ...

# 3. Run validation
.\Manage-DevelopmentTools.ps1 -Action Validate

# 4. Run tests
Invoke-Pester

# 5. Commit changes
git commit -m "Add new feature"

# 6. Push and create PR
git push origin feature/new-feature
```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🆘 Support

- **Documentation:** [Project Wiki](../../wiki)
- **Issues:** [GitHub Issues](../../issues)
- **Discussions:** [GitHub Discussions](../../discussions)
- **Microsoft PowerShell:** [Official Documentation](https://docs.microsoft.com/powershell/)
- **MCP:** [Model Context Protocol](https://modelcontextprotocol.io/)

---

**Last Updated:** $(Get-Date -Format 'yyyy-MM-dd')
**PowerShell Version:** 7.5.2
**MCP Version:** Latest
**VS Code Version:** Latest
