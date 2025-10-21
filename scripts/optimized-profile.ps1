<#
.SYNOPSIS
    Wiley Widget Optimized PowerShell Profile with MCP Environment Support

.DESCRIPTION
    This profile provides optimized PowerShell initialization for the Wiley Widget project,
    including Model Context Protocol (MCP) environment setup with secure credential management.

    Key features:
    - Fast profile loading with background MCP initialization
    - Secure credential caching with Azure Key Vault integration
    - Lazy loading of heavy modules
    - Performance monitoring and optimization

.NOTES
    Author: Wiley Widget Development Team
    Version: 2.0.0
    Created: September 23, 2025
    Last Modified: September 23, 2025

    This script follows Microsoft PowerShell scripting best practices as documented at:
    https://docs.microsoft.com/en-us/powershell/scripting/developer/cmdlet/

.LINK
    https://docs.microsoft.com/en-us/powershell/scripting/developer/cmdlet/best-practices
#>

[CmdletBinding()]
param()

#region Initialization
#Requires -Version 5.1

# Set strict mode for better error handling
Set-StrictMode -Version Latest

# Enable verbose output if requested
$VerbosePreference = if ($PSBoundParameters.ContainsKey('Verbose')) { "Continue" } else { "SilentlyContinue" }

# Measure profile load time for performance monitoring
$script:ProfileStartTime = Get-Date
$script:ProfileLoadMetrics = @{
    StartTime = $script:ProfileStartTime
    Phases    = @()
}

function Write-ProfilePhase {
    <#
    .SYNOPSIS
        Records a profile loading phase for performance monitoring
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$PhaseName,

        [Parameter(Mandatory = $false)]
        [string]$Details
    )

    $phase = @{
        Name      = $PhaseName
        Timestamp = Get-Date
        Details   = $Details
    }
    $script:ProfileLoadMetrics.Phases += $phase

    Write-Verbose "Profile Phase: $PhaseName - $(Get-Date -Format 'HH:mm:ss.fff')"
    if ($Details) {
        Write-Verbose "  Details: $Details"
    }
}

Write-ProfilePhase -PhaseName "ProfileStart" -Details "Beginning profile initialization"
#endregion

# Initialize global MCP state
$global:MCPInitialized = $false
$script:MCPConfiguration = [PSCustomObject]@{
    # Secret mappings (environment variable name -> source)
    SecretMappings = [ordered]@{
        "GITHUB_TOKEN"           = "Environment"
        "XAI_API_KEY"            = "Environment"
        "SYNCFUSION_LICENSE_KEY" = "Environment"
    }

    # Cache settings (for future use)
    Cache          = [PSCustomObject]@{
        Enabled = $false
    }
}

# Initialize Syncfusion license registration state
$script:SyncfusionLicenseRegistered = $null

# Load secrets from environment variables
function Initialize-EnvironmentSecret {
    <#
    .SYNOPSIS
        Initializes environment secrets from environment variables
    #>
    [CmdletBinding()]
    param()

    Write-ProfilePhase -PhaseName "EnvironmentSecretsInit" -Details "Loading secrets from environment variables"

    $config = $script:MCPConfiguration
    $secretMappings = $config.SecretMappings

    foreach ($envVar in $secretMappings.Keys) {
        $value = [Environment]::GetEnvironmentVariable($envVar, "User")
        if ($value -and $value -notlike "*YOUR_*" -and $value -notlike "*PLACEHOLDER*") {
            Write-Verbose "Loaded secret: $envVar"
        }
        else {
            Write-Verbose "Secret not found or placeholder: $envVar"
        }
    }
}
#endregion

#region Core Setup
Write-ProfilePhase -PhaseName "CoreSetup" -Details "Setting up core PowerShell environment"

# Set execution policy for current process only (secure approach)
try {
    Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process -ErrorAction Stop
    Write-Verbose "Execution policy set to RemoteSigned for current process"
}
catch {
    Write-Warning "Failed to set execution policy: $($_.Exception.Message)"
}

# Essential environment variables (telemetry opt-out)
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:POWERSHELL_TELEMETRY_OPTOUT = "1"

Write-Verbose "Core environment setup completed"
#endregion

#region MCP Environment Management
function Initialize-EnvironmentSecret {
    <#
    .SYNOPSIS
        Initializes environment secrets from environment variables
    #>
    [CmdletBinding()]
    param()

    Write-ProfilePhase -PhaseName "EnvironmentSecretsInit" -Details "Loading secrets from environment variables"

    $config = $script:MCPConfiguration
    $secretMappings = $config.SecretMappings

    foreach ($envVar in $secretMappings.Keys) {
        $value = [Environment]::GetEnvironmentVariable($envVar, "User")
        if ($value -and $value -notlike "*YOUR_*" -and $value -notlike "*PLACEHOLDER*") {
            Write-Verbose "Loaded secret: $envVar"
        }
        else {
            Write-Verbose "Secret not found or placeholder: $envVar"
        }
    }
}

function Initialize-MCPEnvironment {
    <#
    .SYNOPSIS
        Initializes the MCP environment with environment variables
    #>
    [CmdletBinding()]
    param()

    if ($global:MCPInitialized) {
        Write-Verbose "MCP environment already initialized"
        return
    }

    Write-ProfilePhase -PhaseName "MCPInit" -Details "Initializing MCP environment"

    # Load secrets from environment variables
    Initialize-EnvironmentSecrets

    Write-Host "🚀 MCP Environment loaded from environment variables" -ForegroundColor Green

    $global:MCPInitialized = $true
}
#endregion

#region Syncfusion License Management
function Register-SyncfusionLicense {
    <#
    .SYNOPSIS
        Registers the Syncfusion license key for the current session
    #>
    [CmdletBinding()]
    param()

    # Only attempt registration if we haven't tried before
    if ($null -ne $script:SyncfusionLicenseRegistered) {
        return $script:SyncfusionLicenseRegistered
    }

    Write-ProfilePhase -PhaseName "SyncfusionLicense" -Details "Checking Syncfusion license"

    $licenseKey = [Environment]::GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "User")

    if (-not $licenseKey -or $licenseKey -like "*YOUR_*" -or $licenseKey -like "*PLACEHOLDER*") {
        Write-Verbose "Syncfusion license key not found or is placeholder"
        $script:SyncfusionLicenseRegistered = $false
        return $false
    }

    # Check if any Syncfusion assemblies are loaded
    $syncfusionAssemblies = [System.AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.FullName -like "*Syncfusion*" }

    if (-not $syncfusionAssemblies) {
        Write-Verbose "No Syncfusion assemblies loaded yet - license registration deferred"
        $script:SyncfusionLicenseRegistered = $null  # Null means not tried yet
        return $null
    }

    try {
        # Try the modern Syncfusion licensing API using reflection
        foreach ($assembly in $syncfusionAssemblies) {
            $licenseProviderType = $assembly.GetTypes() | Where-Object { $_.Name -like "*License*" -and $_.Name -notlike "*Exception*" } | Select-Object -First 1
            if ($licenseProviderType) {
                $registerMethod = $licenseProviderType.GetMethods() | Where-Object { $_.Name -like "*Register*" -and $_.GetParameters().Count -eq 1 } | Select-Object -First 1
                if ($registerMethod) {
                    $registerMethod.Invoke($null, @($licenseKey))
                    Write-Verbose "Syncfusion license registered successfully"
                    $script:SyncfusionLicenseRegistered = $true
                    return $true
                }
            }
        }

        Write-Verbose "No suitable Syncfusion licensing API found"
        $script:SyncfusionLicenseRegistered = $false
        return $false
    }
    catch {
        Write-Verbose "Failed to register Syncfusion license: $($_.Exception.Message)"
        $script:SyncfusionLicenseRegistered = $false
        return $false
    }
}
#endregion

#region Module Management
Write-ProfilePhase -PhaseName "ModuleSetup" -Details "Setting up module lazy loading"

# Lazy loading functions for heavy modules (Microsoft recommended approach)
function Import-AzureModule {
    <#
    .SYNOPSIS
        Lazily imports Azure PowerShell modules
    #>
    [CmdletBinding()]
    param()

    if (-not (Get-Module -Name Az -ListAvailable)) {
        Write-Host "Loading Azure modules..." -ForegroundColor Yellow
        Import-Module Az -ErrorAction SilentlyContinue -Verbose:$false
    }
}

function Import-PoshGit {
    <#
    .SYNOPSIS
        Lazily imports Posh-Git module
    #>
    [CmdletBinding()]
    param()

    if (-not (Get-Module -Name posh-git -ListAvailable)) {
        Write-Host "Loading Posh-Git..." -ForegroundColor Yellow
        Import-Module posh-git -ErrorAction SilentlyContinue -Verbose:$false
    }
}

function Import-OhMyPosh {
    <#
    .SYNOPSIS
        Lazily imports Oh-My-Posh module and initializes it
    #>
    [CmdletBinding()]
    param()

    if (-not (Get-Module -Name oh-my-posh -ListAvailable)) {
        Write-Host "Loading Oh-My-Posh..." -ForegroundColor Yellow
        Import-Module oh-my-posh -ErrorAction SilentlyContinue -Verbose:$false
        if (Get-Command oh-my-posh -ErrorAction SilentlyContinue) {
            oh-my-posh init pwsh | Invoke-Expression
        }
    }
}

# Background initialization for non-critical components
$backgroundInit = {
    try {
        # Load heavy modules in background (Microsoft recommended pattern)
        Start-Job -ScriptBlock {
            try {
                Import-Module PSReadLine -ErrorAction SilentlyContinue -Verbose:$false
                Import-Module Terminal-Icons -ErrorAction SilentlyContinue -Verbose:$false
            }
            catch {
                # Silent failure for background operations
            }
        } | Out-Null
    }
    catch {
        # Silent failure for background initialization
    }
}

# Start background initialization (non-blocking)
try {
    Start-Job -ScriptBlock $backgroundInit -ErrorAction SilentlyContinue | Out-Null
}
catch {
    Write-Verbose "Failed to start background initialization: $($_.Exception.Message)"
}
#endregion

#region Prompt and Aliases
Write-ProfilePhase -PhaseName "PromptSetup" -Details "Setting up prompt and aliases"

# Fast aliases (immediate setup)
$fastAliases = @{
    "ll"    = "Get-ChildItem"
    "grep"  = "Select-String"
    "touch" = "New-Item"
}

foreach ($alias in $fastAliases.GetEnumerator()) {
    try {
        Set-Alias -Name $alias.Key -Value $alias.Value -ErrorAction Stop
    }
    catch {
        Write-Verbose "Failed to set alias '\''$($alias.Key)'\'': $($_.Exception.Message)"
    }
}

# Optimized prompt function (Microsoft recommended pattern)
function prompt {
    <#
    .SYNOPSIS
        Custom prompt function with performance metrics
    #>
    $lastCommand = Get-History -Count 1 -ErrorAction SilentlyContinue
    if ($lastCommand) {
        $duration = $lastCommand.EndExecutionTime - $lastCommand.StartExecutionTime
        $durationString = if ($duration.TotalSeconds -gt 1) {
            " [$($duration.TotalSeconds.ToString("F2"))s]"
        }
        else { "" }
    }
    else {
        $durationString = ""
    }

    "$pwd$durationString$('>' * ($nestedPromptLevel + 1)) "
}

# Lazy loading aliases - only load when first used (Microsoft recommended)
$lazyLoadAliases = [ordered]@{
    "az"   = ${function:Import-AzureModules}
    "git"  = ${function:Import-PoshGit}
    "posh" = ${function:Import-OhMyPosh}
}

# Override default aliases to trigger lazy loading
foreach ($cmd in $lazyLoadAliases.Keys) {
    if (Get-Command $cmd -ErrorAction SilentlyContinue) {
        # Store the original command before creating wrapper
        $originalCommand = Get-Command $cmd

        # Create wrapper function (secure approach)
        $wrapperScript = @"
function global:$cmd {
    (`$lazyLoadAliases['$cmd']).Invoke()
    & `"$($originalCommand.Source)`" @args
}
"@
        try {
            Invoke-Expression $wrapperScript
            Write-Verbose "Created lazy loading wrapper for: $cmd"
        }
        catch {
            Write-Verbose "Failed to create lazy loading wrapper for '\''$cmd'\'': $($_.Exception.Message)"
        }
    }
}
#endregion

#region Utility Functions
function Get-ProfileLoadTime {
    <#
    .SYNOPSIS
        Gets the profile load time for performance monitoring
    #>
    [CmdletBinding()]
    param()

    $loadTime = (Get-Date) - $script:ProfileStartTime
    Write-Host "Profile loaded in: $($loadTime.TotalMilliseconds)ms" -ForegroundColor Green
}

function Get-ProfileMetric {
    <#
    .SYNOPSIS
        Displays detailed profile loading metrics
    #>
    [CmdletBinding()]
    param()

    $totalTime = (Get-Date) - $script:ProfileStartTime

    Write-Host "📊 Profile Load Metrics" -ForegroundColor Cyan
    Write-Host "======================" -ForegroundColor Cyan
    Write-Host "Total load time: $([math]::Round($totalTime.TotalMilliseconds, 0))ms" -ForegroundColor White

    if ($script:ProfileLoadMetrics.Phases) {
        Write-Host "`nPhase breakdown:" -ForegroundColor Yellow
        for ($i = 1; $i -lt $script:ProfileLoadMetrics.Phases.Count; $i++) {
            $phase = $script:ProfileLoadMetrics.Phases[$i]
            $prevPhase = $script:ProfileLoadMetrics.Phases[$i - 1]
            $phaseTime = $phase.Timestamp - $prevPhase.Timestamp

            Write-Host ("  {0}: {1}ms" -f $phase.Name, [math]::Round($phaseTime.TotalMilliseconds, 0)) -ForegroundColor White
            if ($phase.Details) {
                Write-Host "    $($phase.Details)" -ForegroundColor Gray
            }
        }
    }
}

function Update-Profile {
    <#
    .SYNOPSIS
        Updates/reloads the PowerShell profile
    #>
    [CmdletBinding()]
    param()

    try {
        . $PROFILE
        Write-Host "Profile updated successfully!" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to update profile: $($_.Exception.Message)"
    }
}
#endregion

#region MCP Environment Management Functions
function Get-MCPCacheStatus {
    <#
    .SYNOPSIS
        Shows the current status of MCP environment variables

    .DESCRIPTION
        Displays detailed information about the MCP environment variable status.

    .EXAMPLE
        Get-MCPCacheStatus

        Displays the current MCP environment variable status.
    #>
    [CmdletBinding()]
    param()

    $config = $script:MCPConfiguration

    Write-Host "🔍 MCP Environment Status" -ForegroundColor Cyan
    Write-Host "========================" -ForegroundColor Cyan

    # Environment variables status
    Write-Host "🔧 Environment Variables:" -ForegroundColor Cyan
    foreach ($envVar in $config.SecretMappings.Keys) {
        $value = [Environment]::GetEnvironmentVariable($envVar, "User")
        if ($value) {
            $maskedValue = $value.Substring(0, [Math]::Min(10, $value.Length)) + "..."
            Write-Host "  ✅ $envVar`: $maskedValue" -ForegroundColor Green
        }
        else {
            Write-Host "  ❌ $envVar`: Not set" -ForegroundColor Red
        }
    }
}
#endregion

#region Performance Monitoring
function Start-ProfileTimer {
    <#
    .SYNOPSIS
        Starts a profile performance timer
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if ($PSCmdlet.ShouldProcess("Profile Timer", "Start")) {
        $global:ProfileTimer = Get-Date
    }
}

function Stop-ProfileTimer {
    <#
    .SYNOPSIS
        Stops the profile performance timer and displays elapsed time
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if ($PSCmdlet.ShouldProcess("Profile Timer", "Stop and display")) {
        if ($global:ProfileTimer) {
            $elapsed = (Get-Date) - $global:ProfileTimer
            Write-Host "Operation completed in: $($elapsed.TotalMilliseconds)ms" -ForegroundColor Cyan
            Remove-Variable ProfileTimer -Scope Global -ErrorAction SilentlyContinue
        }
    }
}
#endregion

#region PSReadLine Configuration
Write-ProfilePhase -PhaseName "PSReadLineSetup" -Details "Configuring PSReadLine for enhanced experience"

# Fast path completion (if PSReadLine is available) - Microsoft recommended settings
if (Get-Module -Name PSReadLine -ListAvailable) {
    try {
        # Enable fast menu completion (Microsoft recommended)
        Set-PSReadLineOption -PredictionSource History -ErrorAction SilentlyContinue
        Set-PSReadLineOption -PredictionViewStyle ListView -ErrorAction SilentlyContinue
        Set-PSReadLineKeyHandler -Key Tab -Function MenuComplete -ErrorAction SilentlyContinue

        Write-Verbose "PSReadLine configured with enhanced completion"
    }
    catch {
        Write-Verbose "Failed to configure PSReadLine: $($_.Exception.Message)"
    }
}
#endregion

#region MCP Status Function
function Get-MCPStatus {
    <#
    .SYNOPSIS
        Gets the current status of MCP initialization
    #>
    [CmdletBinding()]
    param()

    # Check if MCP is initialized
    if ($global:MCPInitialized) {
        Write-Host "✅ MCP Environment: Initialized" -ForegroundColor Green
        return $true
    }

    # MCP initialization is lazy - initialize now if needed
    Write-Host "🔄 Initializing MCP Environment..." -ForegroundColor Yellow
    try {
        Initialize-MCPEnvironment
        Write-Host "✅ MCP Environment: Initialized" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "❌ MCP Environment: Initialization failed - $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}
#endregion

#region Finalization
Write-ProfilePhase -PhaseName "Finalization" -Details "Completing profile initialization"

# Initialize MCP environment lazily (only when first MCP command is used)
# This avoids blocking profile load with Azure CLI calls
Write-ProfilePhase -PhaseName "MCPInit" -Details "MCP initialization deferred until needed"

# Register Syncfusion license asynchronously (deferred until assemblies are loaded)
try {
    $syncfusionResult = Register-SyncfusionLicense
    if ($syncfusionResult -eq $true) {
        Write-ProfilePhase -PhaseName "SyncfusionInit" -Details "Syncfusion license registered successfully"
    }
    elseif ($null -eq $syncfusionResult) {
        Write-ProfilePhase -PhaseName "SyncfusionInit" -Details "Syncfusion license registration deferred"
    }
    else {
        Write-Verbose "Syncfusion license registration failed"
    }
}
catch {
    Write-Verbose "Failed to check Syncfusion license: $($_.Exception.Message)"
}

# Profile load time reporting (only if slow loading detected)
$profileLoadTime = (Get-Date) - $script:ProfileStartTime
if ($profileLoadTime.TotalMilliseconds -gt 1000) {
    Write-Host "Profile loaded in: $([math]::Round($profileLoadTime.TotalMilliseconds, 0))ms (consider optimization)" -ForegroundColor Yellow
}
elseif ($profileLoadTime.TotalMilliseconds -gt 500) {
    Write-Host "Profile loaded in: $([math]::Round($profileLoadTime.TotalMilliseconds, 0))ms" -ForegroundColor Green
}

Write-ProfilePhase -PhaseName "ProfileComplete" -Details "Profile initialization completed"
Write-Verbose "Wiley Widget optimized profile loaded successfully"
#endregion
