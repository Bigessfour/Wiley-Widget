# Development Environment Setup Script
# Following Microsoft PowerShell 7.5.2 and MCP best practices
# Initializes complete development environment for Wiley Widget project

param(
    [Parameter(Mandatory = $false)]
    [switch]$SkipConfirmation,

    [Parameter(Mandatory = $false)]
    [switch]$Force,

    [Parameter(Mandatory = $false)]
    [switch]$Verbose
)

#Requires -Version 7.5

<#
.SYNOPSIS
    Sets up the complete development environment for Wiley Widget.

.DESCRIPTION
    This script installs and configures all necessary development tools
    following Microsoft PowerShell 7.5.2 and MCP best practices.

.PARAMETER SkipConfirmation
    Skip confirmation prompts.

.PARAMETER Force
    Force reinstallation of existing tools.

.PARAMETER Verbose
    Enable verbose output.

.EXAMPLE
    .\Setup-DevelopmentEnvironment.ps1

.EXAMPLE
    .\Setup-DevelopmentEnvironment.ps1 -Force -Verbose
#>

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Import the tools manifest
$manifestPath = Join-Path $PSScriptRoot 'Development-Tools-Manifest.psd1'
if (-not (Test-Path $manifestPath)) {
    Write-Error "Development tools manifest not found: $manifestPath"
    exit 1
}

try {
    $tools = Import-PowerShellDataFile -Path $manifestPath
    Write-Verbose "Loaded development tools manifest version $($tools.Project.Version)"
} catch {
    Write-Error "Failed to load tools manifest: $_"
    exit 1
}

function Write-SetupHeader {
    param([string]$Title)

    Write-Host "`n" -NoNewline
    Write-Host "╔" + ("═" * 60) + "╗" -ForegroundColor Cyan
    Write-Host "║" + $Title.PadLeft(30 + ($Title.Length / 2)).PadRight(60) + "║" -ForegroundColor Cyan
    Write-Host "╚" + ("═" * 60) + "╝" -ForegroundColor Cyan
}

function Test-AdministratorPrivileges {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Install-PowerShellModules {
    Write-SetupHeader "PowerShell Modules"

    $modulesToInstall = $tools.CoreTools.PowerShell.Modules | Where-Object { $_.Required }

    foreach ($module in $modulesToInstall) {
        Write-Host "📦 Installing $($module.Name) v$($module.Version)..." -ForegroundColor White

        try {
            # Check if module is already installed
            $existingModule = Get-Module -Name $module.Name -ListAvailable | Select-Object -First 1

            if ($existingModule -and -not $Force) {
                if ($existingModule.Version -ge [version]$module.Version) {
                    Write-Host "✅ $($module.Name) v$($existingModule.Version) already installed" -ForegroundColor Green
                    continue
                } else {
                    Write-Host "🔄 Updating $($module.Name) from v$($existingModule.Version) to v$($module.Version)" -ForegroundColor Yellow
                }
            }

            # Install or update module
            $installParams = @{
                Name = $module.Name
                MinimumVersion = $module.Version
                Force = $true
                AllowClobber = $true
                SkipPublisherCheck = $true
            }

            if ($module.Repository) {
                $installParams.Repository = $module.Repository
            }

            Install-Module @installParams

            Write-Host "✅ $($module.Name) installed successfully" -ForegroundColor Green

        } catch {
            Write-Host "❌ Failed to install $($module.Name): $_" -ForegroundColor Red
            throw
        }
    }
}

function Setup-VSCodeEnvironment {
    Write-SetupHeader "VS Code Environment"

    # Check if VS Code is installed
    try {
        $vscodeVersion = & code --version 2>$null | Select-Object -First 1
        Write-Host "✅ VS Code v$($vscodeVersion.Trim()) detected" -ForegroundColor Green
    } catch {
        Write-Host "❌ VS Code not found. Please install VS Code first." -ForegroundColor Red
        Write-Host "   Download: https://code.visualstudio.com/" -ForegroundColor Yellow
        throw "VS Code is required"
    }

    # Install required extensions
    Write-Host "`n🔌 Installing VS Code Extensions..." -ForegroundColor White

    foreach ($extension in $tools.IDE.VSCode.Extensions) {
        if ($extension.Required) {
            Write-Host "Installing $($extension.Name)..." -ForegroundColor White

            try {
                $installArgs = @('--install-extension', $extension.Name)
                if ($Force) {
                    $installArgs += '--force'
                }

                & code $installArgs

                Write-Host "✅ $($extension.Name) installed" -ForegroundColor Green

            } catch {
                Write-Host "❌ Failed to install $($extension.Name): $_" -ForegroundColor Red
            }
        }
    }

    # Configure VS Code settings
    Write-Host "`n⚙️  Configuring VS Code Settings..." -ForegroundColor White

    $vscodeSettingsPath = Join-Path $env:APPDATA 'Code\User\settings.json'
    $vscodeDir = Split-Path $vscodeSettingsPath -Parent

    if (-not (Test-Path $vscodeDir)) {
        New-Item -ItemType Directory -Path $vscodeDir -Force | Out-Null
    }

    # Load existing settings or create new
    $vscodeSettings = @{}
    if (Test-Path $vscodeSettingsPath) {
        try {
            $vscodeSettings = Get-Content $vscodeSettingsPath -Raw | ConvertFrom-Json -AsHashtable
        } catch {
            Write-Host "⚠️  Could not parse existing VS Code settings" -ForegroundColor Yellow
        }
    }

    # Apply Wiley Widget specific settings
    $widgetSettings = @{
        'powershell.powerShellDefaultVersion' = 'PowerShell 7.5.2'
        'powershell.enableProfileLoading' = $true
        'powershell.integratedConsole.showOnStartup' = $false
        'powershell.codeFormatting.preset' = 'OTBS'
        'powershell.scriptAnalysis.enable' = $true
        'powershell.scriptAnalysis.settingsPath' = 'PSScriptAnalyzerSettings.psd1'
        'files.associations' = @{
            '*.psd1' = 'powershell'
            '*.psm1' = 'powershell'
            '*.ps1xml' = 'xml'
        }
        'editor.formatOnSave' = $true
        'editor.formatOnType' = $true
        'files.trimTrailingWhitespace' = $true
        'files.insertFinalNewline' = $true
    }

    # Merge settings
    foreach ($key in $widgetSettings.Keys) {
        $vscodeSettings[$key] = $widgetSettings[$key]
    }

    # Save settings
    $vscodeSettings | ConvertTo-Json -Depth 10 | Out-File $vscodeSettingsPath -Encoding UTF8
    Write-Host "✅ VS Code settings configured" -ForegroundColor Green
}

function Setup-MCPEnvironment {
    Write-SetupHeader "Model Context Protocol (MCP)"

    # Check Node.js
    try {
        $nodeVersion = & node --version 2>$null
        Write-Host "✅ Node.js $nodeVersion detected" -ForegroundColor Green
    } catch {
        Write-Host "❌ Node.js not found. Please install Node.js first." -ForegroundColor Red
        Write-Host "   Download: https://nodejs.org/" -ForegroundColor Yellow
        throw "Node.js is required for MCP"
    }

    # Install MCP servers
    Write-Host "`n🔧 Installing MCP Servers..." -ForegroundColor White

    foreach ($server in $tools.MCP.Servers) {
        Write-Host "Installing $($server.Name)..." -ForegroundColor White

        try {
            # Install via npm
            & npm install -g $server.Package

            Write-Host "✅ $($server.Name) installed" -ForegroundColor Green

        } catch {
            Write-Host "❌ Failed to install $($server.Name): $_" -ForegroundColor Red
        }
    }

    # Configure MCP settings in VS Code
    Write-Host "`n⚙️  Configuring MCP in VS Code..." -ForegroundColor White

    $mcpConfig = @{
        'modelContextProtocol.servers' = @{}
    }

    foreach ($server in $tools.MCP.Servers) {
        $mcpConfig.'modelContextProtocol.servers'[$server.Name] = @{
            command = $server.Command
            args = $server.Args
            env = $server.Env
        }
    }

    # Update VS Code settings with MCP configuration
    $vscodeSettingsPath = Join-Path $env:APPDATA 'Code\User\settings.json'
    if (Test-Path $vscodeSettingsPath) {
        $vscodeSettings = Get-Content $vscodeSettingsPath -Raw | ConvertFrom-Json -AsHashtable

        foreach ($key in $mcpConfig.Keys) {
            $vscodeSettings[$key] = $mcpConfig[$key]
        }

        $vscodeSettings | ConvertTo-Json -Depth 10 | Out-File $vscodeSettingsPath -Encoding UTF8
        Write-Host "✅ MCP configuration added to VS Code" -ForegroundColor Green
    }
}

function Setup-EnvironmentVariables {
    Write-SetupHeader "Environment Variables"

    Write-Host "🔧 Setting up environment variables..." -ForegroundColor White

    foreach ($envVar in $tools.Environment.Variables) {
        $currentValue = [Environment]::GetEnvironmentVariable($envVar.Name, 'User')

        if (-not $currentValue -or $Force) {
            if ($envVar.Sensitive) {
                Write-Host "⚠️  Sensitive variable '$($envVar.Name)' requires manual setup" -ForegroundColor Yellow
                Write-Host "   Please set this variable manually for security" -ForegroundColor White
            } else {
                try {
                    [Environment]::SetEnvironmentVariable($envVar.Name, $envVar.DefaultValue, 'User')
                    Write-Host "✅ $($envVar.Name) set to $($envVar.DefaultValue)" -ForegroundColor Green
                } catch {
                    Write-Host "❌ Failed to set $($envVar.Name): $_" -ForegroundColor Red
                }
            }
        } else {
            Write-Host "✅ $($envVar.Name) already set" -ForegroundColor Green
        }
    }
}

function Setup-PowerShellProfile {
    Write-SetupHeader "PowerShell Profile"

    Write-Host "🔧 Configuring PowerShell profile..." -ForegroundColor White

    $profilePath = $PROFILE.CurrentUserAllHosts

    # Ensure profile directory exists
    $profileDir = Split-Path $profilePath -Parent
    if (-not (Test-Path $profileDir)) {
        New-Item -ItemType Directory -Path $profileDir -Force | Out-Null
    }

    # Load existing profile or create new
    $profileContent = @()
    if (Test-Path $profilePath) {
        $profileContent = Get-Content $profilePath
    }

    # Add Wiley Widget specific configuration
    $widgetProfile = @"

# Wiley Widget Development Environment
# Auto-generated by Setup-DevelopmentEnvironment.ps1

# Set execution policy for development
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force

# Import required modules
`$modules = @('PSScriptAnalyzer', 'Pester', 'platyPS', 'PowerShellGet')
foreach (`$module in `$modules) {
    if (Get-Module -Name `$module -ListAvailable) {
        Import-Module `$module -ErrorAction SilentlyContinue
    }
}

# Set default parameter values
`$PSDefaultParameterValues['*:Verbose'] = `$false
`$PSDefaultParameterValues['*:ErrorAction'] = 'Stop'

# Custom prompt for Wiley Widget development
function prompt {
    `$location = Get-Location
    Write-Host "WileyWidget " -NoNewline -ForegroundColor Cyan
    Write-Host "PS " -NoNewline -ForegroundColor White
    Write-Host "`$location" -NoNewline -ForegroundColor Yellow
    Write-Host ">" -NoNewline -ForegroundColor White
    return " "
}

# Load project-specific functions
`$projectRoot = Split-Path `$PSScriptRoot -Parent
`$functionsPath = Join-Path `$projectRoot 'scripts' 'WileyWidget-Profile.ps1'
if (Test-Path `$functionsPath) {
    . `$functionsPath
}

"@

    # Check if profile already contains Wiley Widget configuration
    if ($profileContent -notcontains "# Wiley Widget Development Environment") {
        $profileContent += $widgetProfile
        $profileContent | Out-File $profilePath -Encoding UTF8
        Write-Host "✅ PowerShell profile configured" -ForegroundColor Green
    } else {
        Write-Host "✅ PowerShell profile already configured" -ForegroundColor Green
    }
}

function Setup-QualityTools {
    Write-SetupHeader "Code Quality Tools"

    Write-Host "🔧 Setting up code quality tools..." -ForegroundColor White

    # Create PSScriptAnalyzer settings
    $analyzerSettingsPath = Join-Path $PSScriptRoot 'PSScriptAnalyzerSettings.psd1'

    $analyzerSettings = @{
        Severity = @('Error', 'Warning')
        IncludeRules = @(
            'PSAvoidUsingCmdletAliases',
            'PSAvoidUsingWMICmdlet',
            'PSAvoidUsingPositionalParameters',
            'PSAvoidUsingInvokeExpression',
            'PSAvoidUsingPlainTextForPassword',
            'PSAvoidUsingComputerNameHardcoded',
            'PSAvoidUsingConvertToSecureStringWithPlainText',
            'PSAvoidUsingUserNameAndPasswordParams',
            'PSAvoidUsingClearTextPassword',
            'PSUseDeclaredVarsMoreThanAssignments',
            'PSUsePSCredentialType',
            'PSUseShouldProcessForStateChangingFunctions',
            'PSUseSingularNouns',
            'PSAvoidGlobalVars',
            'PSAvoidUsingWriteHost'
        )
        ExcludeRules = @('PSAvoidUsingWriteHost')  # Allow Write-Host for setup scripts
        Rules = @{
            PSUseConsistentWhitespace = @{
                Enable = $true
                CheckInnerBrace = $true
                CheckOpenBrace = $true
                CheckOpenParen = $true
                CheckOperator = $true
                CheckPipe = $true
                CheckSeparator = $true
            }
            PSUseConsistentIndentation = @{
                Enable = $true
                IndentationSize = 4
                PipelineIndentation = 'IncreaseIndentationForFirstPipeline'
                Kind = 'space'
            }
        }
    }

    $analyzerSettings | ConvertTo-Json -Depth 10 | Out-File $analyzerSettingsPath -Encoding UTF8
    Write-Host "✅ PSScriptAnalyzer settings created" -ForegroundColor Green

    # Setup Pester configuration
    $pesterConfigPath = Join-Path $PSScriptRoot 'PesterConfiguration.psd1'

    $pesterConfig = @{
        Run = @{
            Path = './tests'
            PassThru = $true
            Quiet = $false
        }
        CodeCoverage = @{
            Enabled = $true
            OutputFormat = 'JaCoCo'
            OutputPath = './TestResults/coverage.xml'
            Path = @(
                './WileyWidget/*.ps1',
                './WileyWidget/*.psm1',
                './scripts/*.ps1'
            )
        }
        TestResult = @{
            Enabled = $true
            OutputFormat = 'NUnitXml'
            OutputPath = './TestResults/test-results.xml'
        }
        Output = @{
            Verbosity = 'Detailed'
        }
    }

    $pesterConfig | ConvertTo-Json -Depth 10 | Out-File $pesterConfigPath -Encoding UTF8
    Write-Host "✅ Pester configuration created" -ForegroundColor Green
}

function Show-SetupSummary {
    Write-SetupHeader "Setup Complete"

    Write-Host "🎉 Development environment setup completed!" -ForegroundColor Green
    Write-Host "`n📋 Next Steps:" -ForegroundColor Cyan
    Write-Host "1. Restart VS Code to apply all settings" -ForegroundColor White
    Write-Host "2. Run '.\Manage-DevelopmentTools.ps1 -Action Validate' to verify setup" -ForegroundColor White
    Write-Host "3. Set sensitive environment variables manually (see manifest)" -ForegroundColor White
    Write-Host "4. Test MCP servers with 'Test-GitHub-MCP.ps1'" -ForegroundColor White

    Write-Host "`n📚 Useful Commands:" -ForegroundColor Cyan
    Write-Host "• Validate tools: .\Manage-DevelopmentTools.ps1 -Action Validate" -ForegroundColor White
    Write-Host "• Update tools: .\Manage-DevelopmentTools.ps1 -Action Update" -ForegroundColor White
    Write-Host "• Generate report: .\Manage-DevelopmentTools.ps1 -Action Report" -ForegroundColor White

    Write-Host "`n🔗 Resources:" -ForegroundColor Cyan
    Write-Host "• PowerShell 7.5.2 Docs: https://docs.microsoft.com/powershell/" -ForegroundColor White
    Write-Host "• MCP Documentation: https://modelcontextprotocol.io/" -ForegroundColor White
    Write-Host "• VS Code PowerShell: https://code.visualstudio.com/docs/languages/powershell" -ForegroundColor White
}

# Main setup execution
function Invoke-DevelopmentSetup {
    Write-Host "🚀 Wiley Widget Development Environment Setup" -ForegroundColor Cyan
    Write-Host "Following Microsoft PowerShell 7.5.2 and MCP Best Practices" -ForegroundColor White
    Write-Host "Version: $($tools.Project.Version)" -ForegroundColor White

    if (-not $SkipConfirmation) {
        Write-Host "`n⚠️  This will install and configure development tools." -ForegroundColor Yellow
        $confirmation = Read-Host "Continue? (y/N)"
        if ($confirmation -ne 'y') {
            Write-Host "Setup cancelled." -ForegroundColor Yellow
            return
        }
    }

    # Check administrator privileges for some operations
    $isAdmin = Test-AdministratorPrivileges
    if (-not $isAdmin) {
        Write-Host "⚠️  Some operations may require administrator privileges." -ForegroundColor Yellow
        Write-Host "   Consider running as administrator for complete setup." -ForegroundColor White
    }

    try {
        Install-PowerShellModules
        Setup-VSCodeEnvironment
        Setup-MCPEnvironment
        Setup-EnvironmentVariables
        Setup-PowerShellProfile
        Setup-QualityTools

        Show-SetupSummary

    } catch {
        Write-Host "`n❌ Setup failed: $_" -ForegroundColor Red
        Write-Host "Please check the error messages above and try again." -ForegroundColor Yellow
        throw
    }
}

# Execute setup
Invoke-DevelopmentSetup
