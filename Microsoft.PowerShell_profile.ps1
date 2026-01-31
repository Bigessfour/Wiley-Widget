#region Profile Header
<#
.SYNOPSIS
    Wiley-Widget Development Environment Profile
.DESCRIPTION
    Configures the PowerShell environment for Wiley-Widget development
    - Project-specific aliases and functions
    - Development environment setup
    - Enhanced prompt with git/project info
    - PSReadLine customization
    - Quick project navigation
#>
#endregion

#region Early Setup
$WidgetRoot = 'C:\Users\biges\Desktop\Wiley-Widget'
$ProfileStartTime = Get-Date

# Create $PSStyle for older PowerShell versions
if ($PSVersionTable.PSVersion -lt '7.2.0') {
    $esc = [char]0x1b
    $PSStyle = [pscustomobject]@{
        Foreground = @{
            BrightCyan    = "${esc}[96m"
            BrightGreen   = "${esc}[92m"
            BrightYellow  = "${esc}[93m"
            BrightRed     = "${esc}[91m"
            BrightBlue    = "${esc}[94m"
            Magenta       = "${esc}[35m"
            Gray          = "${esc}[37m"
        }
        Background = @{
            BrightBlack = "${esc}[100m"
        }
        Reset = "${esc}[0m"
    }
}
#endregion

#region Display Environment Banner
function Show-WidgetEnvironmentBanner {
    <#
    .SYNOPSIS
        Display Wiley-Widget development environment information
    #>
    $cyan = $PSStyle.Foreground.BrightCyan
    $green = $PSStyle.Foreground.BrightGreen
    $yellow = $PSStyle.Foreground.BrightYellow
    $reset = $PSStyle.Reset

    Write-Host ""
    Write-Host "${cyan}╔═══════════════════════════════════════════════════════════════╗${reset}"
    Write-Host "${cyan}║${reset}  ${green}Wiley-Widget Development Environment${reset}${cyan}                    ║${reset}"
    Write-Host "${cyan}╠═══════════════════════════════════════════════════════════════╣${reset}"
    
    # PowerShell Version
    Write-Host "${cyan}║${reset}  ${yellow}PowerShell${reset}: $($PSVersionTable.PSVersion.ToString())${cyan}                                 ║${reset}"
    
    # .NET Version
    $dotnetVersion = dotnet --version 2>$null
    if ($dotnetVersion) {
        Write-Host "${cyan}║${reset}  ${yellow}.NET${reset}: $dotnetVersion${cyan}                                       ║${reset}"
    }
    
    # Git Status
    if (Get-Command git -ErrorAction SilentlyContinue) {
        $gitBranch = git -C $WidgetRoot rev-parse --abbrev-ref HEAD 2>$null
        if ($gitBranch) {
            Write-Host "${cyan}║${reset}  ${yellow}Git Branch${reset}: $gitBranch${cyan}                                   ║${reset}"
        }
    }
    
    # Project Status
    Write-Host "${cyan}║${reset}  ${yellow}Project Root${reset}: $WidgetRoot${cyan}          ║${reset}"
    Write-Host "${cyan}╠═══════════════════════════════════════════════════════════════╣${reset}"
    Write-Host "${cyan}║${reset}  ${green}Quick Commands${reset}:                                      ${cyan}║${reset}"
    Write-Host "${cyan}║${reset}    ${yellow}w${reset}           - Go to workspace root                     ${cyan}║${reset}"
    Write-Host "${cyan}║${reset}    ${yellow}b${reset}           - Build the solution                       ${cyan}║${reset}"
    Write-Host "${cyan}║${reset}    ${yellow}t${reset}           - Run tests                                 ${cyan}║${reset}"
    Write-Host "${cyan}║${reset}    ${yellow}r${reset}           - Run the application                       ${cyan}║${reset}"
    Write-Host "${cyan}║${reset}    ${yellow}clean${reset}       - Clean build artifacts                     ${cyan}║${reset}"
    Write-Host "${cyan}║${reset}    ${yellow}kill-tests${reset}  - Kill hanging test processes              ${cyan}║${reset}"
    Write-Host "${cyan}╚═══════════════════════════════════════════════════════════════╝${reset}"
    Write-Host ""
}

# Show banner on profile load
Show-WidgetEnvironmentBanner
#endregion

#region Project Navigation
function w {
    <#
    .SYNOPSIS
        Navigate to Wiley-Widget workspace root
    #>
    Set-Location $WidgetRoot
    Write-Host "📍 Changed directory to: $WidgetRoot" -ForegroundColor Green
    Get-Item . | Get-ChildItem -Directory | Select-Object -First 5 | ForEach-Object {
        Write-Host "  📁 $_" -ForegroundColor Cyan
    }
}

function ws {
    <#
    .SYNOPSIS
        Navigate to src folder
    #>
    Set-Location "$WidgetRoot\src"
    Write-Host "📍 Changed directory to: src/" -ForegroundColor Green
}

function wt {
    <#
    .SYNOPSIS
        Navigate to tests folder
    #>
    Set-Location "$WidgetRoot\tests"
    Write-Host "📍 Changed directory to: tests/" -ForegroundColor Green
}
#endregion

#region Build & Test Commands
function b {
    <#
    .SYNOPSIS
        Build the Wiley-Widget solution
    #>
    Write-Host "🔨 Building Wiley-Widget.sln..." -ForegroundColor Yellow
    dotnet build "$WidgetRoot\WileyWidget.sln" -nologo
}

function bf {
    <#
    .SYNOPSIS
        Build with fast settings (no analyzers)
    #>
    Write-Host "⚡ Fast build (analyzers disabled)..." -ForegroundColor Yellow
    dotnet build "$WidgetRoot\WileyWidget.sln" -p:RunAnalyzers=false -nologo
}

function t {
    <#
    .SYNOPSIS
        Run all tests
    #>
    Write-Host "🧪 Running tests..." -ForegroundColor Yellow
    dotnet test "$WidgetRoot\WileyWidget.sln" --no-build --nologo
}

function r {
    <#
    .SYNOPSIS
        Run the WileyWidget.WinForms application
    #>
    Write-Host "▶️  Starting WileyWidget.WinForms..." -ForegroundColor Green
    dotnet run --project "$WidgetRoot\src\WileyWidget.WinForms\WileyWidget.WinForms.csproj" --no-build
}

function clean {
    <#
    .SYNOPSIS
        Clean build artifacts
    #>
    Write-Host "🧹 Cleaning build artifacts..." -ForegroundColor Yellow
    dotnet clean "$WidgetRoot\WileyWidget.sln" -nologo
    Write-Host "✅ Clean complete" -ForegroundColor Green
}

function kill-tests {
    <#
    .SYNOPSIS
        Kill hanging testhost and dotnet processes
    #>
    Write-Host "🔪 Killing test processes..." -ForegroundColor Yellow
    & "$WidgetRoot\scripts\maintenance\kill-test-processes.ps1"
}
#endregion

#region Development Utilities
function Get-WidgetStats {
    <#
    .SYNOPSIS
        Display Wiley-Widget project statistics
    #>
    Write-Host "📊 Wiley-Widget Project Statistics" -ForegroundColor Cyan
    Write-Host ""
    
    # Count C# files
    $csFiles = Get-ChildItem -Path "$WidgetRoot\src" -Filter "*.cs" -Recurse | Measure-Object
    Write-Host "  C# Files: $($csFiles.Count)" -ForegroundColor Yellow
    
    # Count test files
    $testFiles = Get-ChildItem -Path "$WidgetRoot\tests" -Filter "*Tests.cs" -Recurse | Measure-Object
    Write-Host "  Test Files: $($testFiles.Count)" -ForegroundColor Yellow
    
    # Count project files
    $projFiles = Get-ChildItem -Path "$WidgetRoot\src" -Filter "*.csproj" -Recurse | Measure-Object
    Write-Host "  Projects: $($projFiles.Count)" -ForegroundColor Yellow
    
    # Disk usage
    $diskUsage = Get-ChildItem -Path $WidgetRoot -Recurse -ErrorAction SilentlyContinue | 
                 Measure-Object -Property Length -Sum | 
                 Select-Object @{Name='SizeMB';Expression={[Math]::Round($_.Sum / 1MB, 2)}}
    Write-Host "  Disk Usage: $($diskUsage.SizeMB) MB" -ForegroundColor Yellow
    
    Write-Host ""
}

function Open-WidgetDocs {
    <#
    .SYNOPSIS
        Open Wiley-Widget documentation in VS Code
    #>
    code "$WidgetRoot\docs"
}

function Sync-WidgetRepo {
    <#
    .SYNOPSIS
        Sync git repository (pull latest changes)
    #>
    Write-Host "🔄 Syncing repository..." -ForegroundColor Cyan
    Push-Location $WidgetRoot
    git pull --rebase
    Pop-Location
    Write-Host "✅ Sync complete" -ForegroundColor Green
}
#endregion

#region PSReadLine Configuration
if (Get-Module -Name PSReadLine) {
    $PSReadLineOptions = @{
        ContinuationPrompt        = '  '
        HistorySearchCursorMovesToEnd = $true
        HistorySaveStyle          = 'SaveIncrementally'
        MaximumHistoryCount       = 10000
        Colors                    = @{
            Operator             = $PSStyle.Foreground.Magenta
            Parameter            = $PSStyle.Foreground.Magenta
            Selection            = $PSStyle.Background.BrightBlack
            InLinePrediction     = $PSStyle.Foreground.BrightYellow
            Command              = $PSStyle.Foreground.BrightGreen
        }
    }
    
    Set-PSReadLineOption @PSReadLineOptions
    
    # Key bindings
    Set-PSReadLineKeyHandler -Chord 'Ctrl+f' -Function ForwardWord
    Set-PSReadLineKeyHandler -Chord 'Ctrl+d' -Function DeleteWord
    Set-PSReadLineKeyHandler -Chord 'Alt+d' -Function DeleteWord
    
    # Predictors
    if (Get-Command Set-PSReadLineKeyHandler -ParameterName 'Chord' -ErrorAction SilentlyContinue) {
        Set-PSReadLineKeyHandler -Chord 'UpArrow' -Function HistorySearchBackward
        Set-PSReadLineKeyHandler -Chord 'DownArrow' -Function HistorySearchForward
    }
}
#endregion

#region Custom Prompt
function prompt {
    <#
    .SYNOPSIS
        Custom prompt with git status and admin indicator
    #>
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal] $identity
    $adminRole = [Security.Principal.WindowsBuiltInRole]::Administrator
    
    # Determine prefix (admin, debug, etc.)
    $prefix = ''
    $debugPrefix = if (Test-Path Variable:/PSDebugContext) { '[DBG]: ' } else { '' }
    if ($principal.IsInRole($adminRole)) {
        $prefix = "$($PSStyle.Foreground.BrightRed)[ADMIN]$($PSStyle.Reset):$debugPrefix"
    } elseif ($debugPrefix) {
        $prefix = "$($PSStyle.Foreground.BrightYellow)$debugPrefix$($PSStyle.Reset)"
    }
    
    # Git branch if available
    $gitBranch = ''
    if (Get-Command git -ErrorAction SilentlyContinue) {
        try {
            $gitBranch = git rev-parse --abbrev-ref HEAD 2>$null
            if ($gitBranch) {
                $gitBranch = " $($PSStyle.Foreground.BrightCyan)[$gitBranch]$($PSStyle.Reset)"
            }
        } catch { }
    }
    
    # Current directory
    $path = $PWD.Path
    if ($path.StartsWith($WidgetRoot)) {
        $path = $path.Replace($WidgetRoot, '📁 ~')
    }
    
    # Construct prompt
    $suffix = $(if ($NestedPromptLevel -ge 1) { '>>' }) + '> '
    "${prefix}PS $path${gitBranch} $($PSStyle.Foreground.BrightGreen)$suffix$($PSStyle.Reset)"
}
#endregion

#region Aliases
Set-Alias -Name ll -Value 'Get-ChildItem -Force' -Force
Set-Alias -Name la -Value 'Get-ChildItem -Force -Recurse' -Force
Set-Alias -Name cls -Value 'Clear-Host' -Force
Set-Alias -Name which -Value 'Get-Command' -Force
Set-Alias -Name stats -Value 'Get-WidgetStats' -Force
Set-Alias -Name docs -Value 'Open-WidgetDocs' -Force
Set-Alias -Name sync -Value 'Sync-WidgetRepo' -Force
#endregion

#region Environment Variables
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
#endregion

#region Profile Info
$profileLoadTime = ((Get-Date) - $ProfileStartTime).TotalMilliseconds
Write-Host "✅ Profile loaded in $([Math]::Round($profileLoadTime, 0))ms" -ForegroundColor Gray
#endregion
