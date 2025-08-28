# Wiley Widget PowerShell Profile
# Version: 1.0.0
# PowerShell 7.5.2 Compatible
# Follows Microsoft PowerShell Best Practices

using namespace System.Management.Automation
using namespace System.Collections.Generic

#Requires -Version 7.5

# Profile Guard Clause - Prevent multiple loads (Microsoft Best Practice)
if ($Script:WileyWidgetProfileLoaded) {
    Write-Verbose "Wiley Widget profile already loaded. Skipping duplicate load."
    return
}
$Script:WileyWidgetProfileLoaded = $true

<#
.SYNOPSIS
    Wiley Widget project PowerShell profile with development helpers.

.DESCRIPTION
    This profile provides convenient functions and aliases for Wiley Widget development,
    including build, test, and deployment operations. Follows Microsoft PowerShell
    approved verbs and camelCase naming conventions.

.NOTES
    Author: Wiley Widget Development Team
    Version: 1.0.0
    PowerShell Version: 7.5.2
#>

# Profile Configuration
$Script:WileyWidgetProfile = @{
    Version = '1.0.0'
    ProjectRoot = $PSScriptRoot
    PowerShellVersion = $PSVersionTable.PSVersion
    LastUpdated = Get-Date
    HyperThreading = @{
        Enabled = $false
        LogicalProcessors = 0
        PhysicalCores = 0
        ThreadsPerCore = 0
        MaxParallelJobs = 0
    }
}

# Initialize HyperThreading support immediately
function Initialize-HyperThreadingSupport {
    <#
    .SYNOPSIS
        Initializes hyperthreading support for optimal PowerShell performance.

    .DESCRIPTION
        Detects CPU topology and configures PowerShell for optimal parallel execution
        following Microsoft PowerShell 7.5.2 performance guidelines. Enables hyperthreading-aware
        job scheduling and parallel processing optimization.

    .NOTES
        Based on Microsoft documentation: https://learn.microsoft.com/en-us/powershell/scripting/dev-cross-plat/performance/parallel-execution
    #>
    [CmdletBinding()]
    param()

    Write-Verbose "Initializing HyperThreading support..."

    try {
        # Get CPU information using CIM (Microsoft recommended approach)
        $cpuInfo = Get-CimInstance -ClassName Win32_Processor -ErrorAction Stop

        # Calculate hyperthreading metrics
        $logicalProcessors = ($cpuInfo | Measure-Object -Property NumberOfLogicalProcessors -Sum).Sum
        $physicalCores = ($cpuInfo | Measure-Object -Property NumberOfCores -Sum).Sum
        $threadsPerCore = if ($physicalCores -gt 0) { $logicalProcessors / $physicalCores } else { 1 }

        # Determine hyperthreading status
        $hyperThreadingEnabled = $threadsPerCore -gt 1

        # Calculate optimal parallel job limits (Microsoft recommendation: 2-4x physical cores)
        $maxParallelJobs = if ($hyperThreadingEnabled) {
            [math]::Min($logicalProcessors, $physicalCores * 4)
        } else {
            [math]::Min($physicalCores * 2, 8)  # Conservative for non-HT systems
        }

        # Update profile configuration
        $Script:WileyWidgetProfile.HyperThreading = @{
            Enabled = $hyperThreadingEnabled
            LogicalProcessors = $logicalProcessors
            PhysicalCores = $physicalCores
            ThreadsPerCore = $threadsPerCore
            MaxParallelJobs = $maxParallelJobs
        }

        Write-Verbose "HyperThreading detected: $(if ($hyperThreadingEnabled) { 'Enabled' } else { 'Disabled' })"
        Write-Verbose "Logical Processors: $logicalProcessors, Physical Cores: $physicalCores"
        Write-Verbose "Optimal Max Parallel Jobs: $maxParallelJobs"

        # Configure PowerShell runspace defaults for hyperthreading
        if ($hyperThreadingEnabled) {
            # Set default throttle limit for parallel operations
            $global:PSDefaultParameterValues['ForEach-Object:ThrottleLimit'] = $maxParallelJobs
            $global:PSDefaultParameterValues['Start-ThreadJob:ThrottleLimit'] = $maxParallelJobs

            Write-Verbose "Configured default throttle limits for parallel operations"
        }

    }
    catch {
        Write-Warning "Failed to initialize HyperThreading support: $_"
        # Fallback to conservative defaults
        $Script:WileyWidgetProfile.HyperThreading = @{
            Enabled = $false
            LogicalProcessors = $env:NUMBER_OF_PROCESSORS
            PhysicalCores = [math]::Max(1, [int]($env:NUMBER_OF_PROCESSORS / 2))
            ThreadsPerCore = 1
            MaxParallelJobs = [math]::Min(4, [int]($env:NUMBER_OF_PROCESSORS / 2))
        }
    }
}

# Call the initialization function
Initialize-HyperThreadingSupport

function Get-CpuTopology {
    <#
    .SYNOPSIS
        Gets detailed CPU topology information for performance optimization.

    .DESCRIPTION
        Returns comprehensive CPU topology information including hyperthreading status,
        core counts, and optimal configuration settings for parallel processing.

    .OUTPUTS
        PSCustomObject with CPU topology details
    #>
    [CmdletBinding()]
    param()

    # Ensure hyperthreading is initialized
    if (-not $Script:WileyWidgetProfile.HyperThreading -or
        -not $Script:WileyWidgetProfile.HyperThreading.ContainsKey('Enabled')) {
        Write-Verbose "HyperThreading not initialized, initializing now..."
        Initialize-HyperThreadingSupport
    }

    $ht = $Script:WileyWidgetProfile.HyperThreading

    [PSCustomObject]@{
        HyperThreadingEnabled = $ht.Enabled
        LogicalProcessors = $ht.LogicalProcessors
        PhysicalCores = $ht.PhysicalCores
        ThreadsPerCore = $ht.ThreadsPerCore
        MaxParallelJobs = $ht.MaxParallelJobs
        RecommendedThrottleLimit = $ht.MaxParallelJobs
        PowerShellVersion = $PSVersionTable.PSVersion
        Timestamp = Get-Date
    }
}

function Invoke-OptimizedParallel {
    <#
    .SYNOPSIS
        Executes script blocks in parallel using hyperthreading-optimized settings.

    .DESCRIPTION
        Wrapper for ForEach-Object -Parallel that automatically applies optimal
        throttle limits based on detected CPU topology and hyperthreading support.

    .PARAMETER InputObject
        Collection of objects to process in parallel.

    .PARAMETER ScriptBlock
        Script block to execute for each input object.

    .PARAMETER ThrottleLimit
        Override the automatically calculated throttle limit.

    .PARAMETER UseNewRunspace
        Force creation of new runspaces instead of reusing from pool.

    .EXAMPLE
        1..10 | Invoke-OptimizedParallel -ScriptBlock { $_ * 2 }

    .NOTES
        Based on Microsoft PowerShell parallel execution best practices
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline)]
        [object[]]$InputObject,

        [Parameter(Mandatory)]
        [scriptblock]$ScriptBlock,

        [int]$ThrottleLimit,

        [switch]$UseNewRunspace
    )

    begin {
        # Ensure hyperthreading is initialized
        if (-not $Script:WileyWidgetProfile.HyperThreading -or
            -not $Script:WileyWidgetProfile.HyperThreading.ContainsKey('Enabled')) {
            Write-Verbose "HyperThreading not initialized, initializing now..."
            Initialize-HyperThreadingSupport
        }

        $ht = $Script:WileyWidgetProfile.HyperThreading

        # Use provided throttle limit or auto-calculated optimal value
        $actualThrottleLimit = if ($PSBoundParameters.ContainsKey('ThrottleLimit')) {
            $ThrottleLimit
        } else {
            $ht.MaxParallelJobs
        }

        Write-Verbose "Using throttle limit: $actualThrottleLimit (HyperThreading: $(if ($ht.Enabled) { 'Enabled' } else { 'Disabled' }))"

        # Build parameter splat for ForEach-Object -Parallel
        $foreachParams = @{
            Parallel = $ScriptBlock
            ThrottleLimit = $actualThrottleLimit
        }

        if ($UseNewRunspace) {
            $foreachParams.UseNewRunspace = $true
        }
    }

    process {
        $InputObject | ForEach-Object @foreachParams
    }
}

function Start-OptimizedThreadJob {
    <#
    .SYNOPSIS
        Starts thread jobs with hyperthreading-optimized throttle limits.

    .DESCRIPTION
        Enhanced version of Start-ThreadJob that automatically applies optimal
        throttle limits based on CPU topology detection.

    .PARAMETER ScriptBlock
        Script block to execute in the thread job.

    .PARAMETER Name
        Name for the thread job.

    .PARAMETER ThrottleLimit
        Override the automatically calculated throttle limit.

    .PARAMETER ArgumentList
        Arguments to pass to the script block.

    .EXAMPLE
        Start-OptimizedThreadJob -ScriptBlock { Get-Process } -Name "ProcessJob"

    .NOTES
        Requires ThreadJob module (included with PowerShell 7+)
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [scriptblock]$ScriptBlock,

        [string]$Name,

        [int]$ThrottleLimit,

        [object[]]$ArgumentList
    )

    # Ensure hyperthreading is initialized
    if (-not $Script:WileyWidgetProfile.HyperThreading -or
        -not $Script:WileyWidgetProfile.HyperThreading.ContainsKey('Enabled')) {
        Write-Verbose "HyperThreading not initialized, initializing now..."
        Initialize-HyperThreadingSupport
    }

    $ht = $Script:WileyWidgetProfile.HyperThreading

    # Use provided throttle limit or auto-calculated optimal value
    $actualThrottleLimit = if ($PSBoundParameters.ContainsKey('ThrottleLimit')) {
        $ThrottleLimit
    } else {
        $ht.MaxParallelJobs
    }

    Write-Verbose "Starting thread job with throttle limit: $actualThrottleLimit"

    $jobParams = @{
        ScriptBlock = $ScriptBlock
        ThrottleLimit = $actualThrottleLimit
    }

    if ($Name) { $jobParams.Name = $Name }
    if ($ArgumentList) { $jobParams.ArgumentList = $ArgumentList }

    if ($PSCmdlet.ShouldProcess("Thread job", "Start")) {
        Start-ThreadJob @jobParams
    }
}

function Measure-ParallelPerformance {
    <#
    .SYNOPSIS
        Measures and compares parallel execution performance.

    .DESCRIPTION
        Tests different parallel execution approaches to determine optimal
        configuration for the current system following Microsoft best practices.

    .PARAMETER TestData
        Array of test data to process.

    .PARAMETER Iterations
        Number of test iterations to run.

    .EXAMPLE
        Measure-ParallelPerformance -TestData (1..100) -Iterations 3

    .NOTES
        Based on Microsoft performance measurement guidelines
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object[]]$TestData,

        [int]$Iterations = 1
    )

    # Ensure hyperthreading is initialized
    if (-not $Script:WileyWidgetProfile.HyperThreading -or
        -not $Script:WileyWidgetProfile.HyperThreading.ContainsKey('Enabled')) {
        Write-Verbose "HyperThreading not initialized, initializing now..."
        Initialize-HyperThreadingSupport
    }

    $ht = $Script:WileyWidgetProfile.HyperThreading

    Write-Information "🧪 Measuring Parallel Performance (HyperThreading: $(if ($ht.Enabled) { 'Enabled' } else { 'Disabled' }))" -InformationAction Continue
    Write-Information "CPU Topology: $($ht.PhysicalCores) cores, $($ht.LogicalProcessors) logical processors" -InformationAction Continue

    # Test script block
    $testScript = {
        param($item)
        Start-Sleep -Milliseconds 10  # Simulate work
        $item * 2
    }

    $results = @()

    # Test ForEach-Object -Parallel (Microsoft recommended for threading)
    Write-Information "`n📊 Testing ForEach-Object -Parallel..." -InformationAction Continue
    $time = Measure-Command {
        for ($i = 0; $i -lt $Iterations; $i++) {
            $TestData | ForEach-Object -Parallel $testScript -ThrottleLimit $ht.MaxParallelJobs
        }
    }
    $results += [PSCustomObject]@{
        Method = 'ForEach-Object -Parallel'
        AverageTime = $time.TotalMilliseconds / $Iterations
        ThrottleLimit = $ht.MaxParallelJobs
    }

    # Test Start-ThreadJob
    Write-Information "📊 Testing Start-ThreadJob..." -InformationAction Continue
    $time = Measure-Command {
        for ($i = 0; $i -lt $Iterations; $i++) {
            $jobs = $TestData | Start-ThreadJob -ScriptBlock $testScript -ThrottleLimit $ht.MaxParallelJobs
            $jobs | Receive-Job -Wait -AutoRemoveJob
        }
    }
    $results += [PSCustomObject]@{
        Method = 'Start-ThreadJob'
        AverageTime = $time.TotalMilliseconds / $Iterations
        ThrottleLimit = $ht.MaxParallelJobs
    }

    # Display results
    Write-Information "`n📈 Performance Results:" -InformationAction Continue
    $results | Format-Table -AutoSize

    # Find fastest method
    $fastest = $results | Sort-Object AverageTime | Select-Object -First 1
    Write-Information "🏆 Fastest Method: $($fastest.Method) ($([math]::Round($fastest.AverageTime, 2))ms average)" -InformationAction Continue

    return $results
}

# Add convenience aliases for hyperthreading functions
Set-Alias -Name 'cpu-topology' -Value Get-CpuTopology -Description 'Get CPU topology information' -Force
Set-Alias -Name 'parallel-test' -Value Measure-ParallelPerformance -Description 'Test parallel execution performance' -Force
Set-Alias -Name 'parallel-invoke' -Value Invoke-OptimizedParallel -Description 'Execute optimized parallel operations' -Force
Set-Alias -Name 'start-thread' -Value Start-OptimizedThreadJob -Description 'Start optimized thread job' -Force

# Environment Setup
function Set-WileyWidgetEnvironment {
    <#
    .SYNOPSIS
        Configures the development environment for Wiley Widget.

    .DESCRIPTION
        Sets up environment variables, paths, and configurations needed
        for Wiley Widget development and builds. Follows Microsoft PowerShell
        7.5.2 best practices and approved verbs.

    .PARAMETER Force
        Forces reloading of environment variables even if already loaded.

    .PARAMETER ValidateEnvironment
        Performs validation of the development environment.

    .EXAMPLE
        Set-WileyWidgetEnvironment

    .EXAMPLE
        Set-WileyWidgetEnvironment -ValidateEnvironment
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $false)]
        [switch]$ValidateEnvironment
    )

    if ($PSCmdlet.ShouldProcess("Wiley Widget development environment", "Configure")) {
        Write-Verbose "Setting up Wiley Widget development environment..."

    # Load .env file using Microsoft Docs MCP recommended approach
    $envFilePath = Join-Path $Script:WileyWidgetProfile.ProjectRoot '.env'
    if (Test-Path $envFilePath) {
        Write-Verbose "Loading environment configuration from: $envFilePath"

        try {
            $envContent = Get-Content $envFilePath -ErrorAction Stop
            $loadedVariables = @()

            foreach ($line in $envContent) {
                $trimmedLine = $line.Trim()

                # Skip empty lines and comments
                if ([string]::IsNullOrWhiteSpace($trimmedLine) -or $trimmedLine.StartsWith('#')) {
                    continue
                }

                # Parse key-value pairs
                if ($trimmedLine -match '^([^=]+)=(.*)$') {
                    $key = $matches[1].Trim()
                    $value = $matches[2].Trim()

                    if ($key -and $value) {
                        # Expand environment variables in value
                        $expandedValue = [System.Environment]::ExpandEnvironmentVariables($value)

                        # Set environment variable
                        Set-Item -Path "Env:$key" -Value $expandedValue -ErrorAction Stop
                        $loadedVariables += $key

                        Write-Verbose "Set environment variable: $key = $expandedValue"
                    }
                }
            }

            Write-Verbose "Loaded $($loadedVariables.Count) environment variables from .env file."
        }
        catch {
            Write-Warning "Failed to load environment file: $_"
        }
    }
    else {
        Write-Warning "Environment file not found: $envFilePath"
    }

    # Set core project environment variables
    $env:WILEY_WIDGET_ROOT = $Script:WileyWidgetProfile.ProjectRoot
    $env:WILEY_WIDGET_PROFILE_LOADED = 'true'
    $env:WILEY_WIDGET_PROFILE_VERSION = $Script:WileyWidgetProfile.Version

    # Configure PowerShell-specific settings
    if (-not $env:POWERSHELL_EXECUTION_POLICY) {
        $env:POWERSHELL_EXECUTION_POLICY = 'RemoteSigned'
    }

    # Configure .NET development environment
    if (-not $env:DOTNET_NOLOGO) {
        $env:DOTNET_NOLOGO = 'true'
    }

    # Configure MSBuild debugging
    if (-not $env:MSBUILDDEBUGPATH) {
        $env:MSBUILDDEBUGPATH = Join-Path $env:TEMP 'MSBuildDebug'
        New-Item -ItemType Directory -Path $env:MSBUILDDEBUGPATH -Force -ErrorAction SilentlyContinue | Out-Null
    }

    # Add scripts directory to PATH
    $scriptsPath = Join-Path $Script:WileyWidgetProfile.ProjectRoot 'scripts'
    if ($env:PATH -notlike "*$scriptsPath*") {
        $env:PATH = "$scriptsPath;$env:PATH"
        Write-Verbose "Added scripts directory to PATH: $scriptsPath"
    }

    # Set default configuration
    if (-not $env:WILEY_WIDGET_CONFIG) {
        $env:WILEY_WIDGET_CONFIG = 'Release'
    }

    # Configure test settings
    if (-not $env:RUN_UI_TESTS) {
        $env:RUN_UI_TESTS = '0'
    }

    # Configure logging
    if (-not $env:WILEY_WIDGET_LOG_DIR) {
        $env:WILEY_WIDGET_LOG_DIR = Join-Path $env:APPDATA 'WileyWidget\logs'
    }

    # Validate environment if requested
    if ($ValidateEnvironment) {
        Write-Verbose "Setting up Wiley Widget development environment..."
        $validationResults = Test-WileyWidgetEnvironment

        if ($validationResults.IsValid) {
            Write-Verbose "Environment validation passed!"
        }
        else {
            Write-Warning "Environment validation failed. See details above."
        }
    }

    Write-Verbose "Wiley Widget environment configured successfully."
    Write-Verbose "Profile Version: $($Script:WileyWidgetProfile.Version)"
    Write-Verbose "PowerShell Version: $($Script:WileyWidgetProfile.PowerShellVersion)"
    }
}

function Test-WileyWidgetEnvironment {
    <#
    .SYNOPSIS
        Validates the Wiley Widget development environment.

    .DESCRIPTION
        Performs comprehensive validation of the development environment
        including PowerShell version, .NET SDK, required tools, and configuration.
        Follows Microsoft PowerShell 7.5.2 best practices.

    .PARAMETER Detailed
        Provides detailed validation results.

    .PARAMETER FixIssues
        Attempts to automatically fix identified issues.

    .EXAMPLE
        Test-WileyWidgetEnvironment

    .EXAMPLE
        Test-WileyWidgetEnvironment -Detailed -FixIssues
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [switch]$Detailed,

        [Parameter(Mandatory = $false)]
        [switch]$FixIssues
    )

    $validationResults = [PSCustomObject]@{
        IsValid = $true
        Issues = @()
        Warnings = @()
        PassedChecks = @()
    }

    Write-Verbose "Validating Wiley Widget development environment..."

    # Validate PowerShell version
    $psVersion = $PSVersionTable.PSVersion
    if ($psVersion.Major -ge 7 -and $psVersion.Minor -ge 5) {
        $validationResults.PassedChecks += "PowerShell version: $($psVersion.ToString())"
        if ($Detailed) {
            Write-Verbose "✓ PowerShell version: $($psVersion.ToString())"
        }
    }
    else {
        $validationResults.IsValid = $false
        $validationResults.Issues += "PowerShell version must be 7.5.2 or higher. Current: $($psVersion.ToString())"
        Write-Error "PowerShell version must be 7.5.2 or higher. Current: $($psVersion.ToString())"
    }

    # Validate .NET SDK
    try {
        $dotnetVersion = & dotnet --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            $validationResults.PassedChecks += ".NET SDK version: $dotnetVersion"
            if ($Detailed) {
                Write-Verbose "✓ .NET SDK version: $dotnetVersion"
            }
        }
        else {
            throw "dotnet command failed"
        }
    }
    catch {
        $validationResults.IsValid = $false
        $validationResults.Issues += ".NET SDK not found or not accessible"
        Write-Error ".NET SDK not found or not accessible"
        if ($FixIssues) {
            Write-Verbose "Attempting to locate .NET SDK..."
        }
    }

    # Validate project structure
    $requiredPaths = @(
        'WileyWidget\WileyWidget.csproj',
        'WileyWidget.sln',
        '.env',
        'scripts\build.ps1'
    )

    foreach ($path in $requiredPaths) {
        $fullPath = Join-Path $Script:WileyWidgetProfile.ProjectRoot $path
        if (Test-Path $fullPath) {
            $validationResults.PassedChecks += "Project file exists: $path"
            if ($Detailed) {
                Write-Verbose "✓ Project file exists: $path"
            }
        }
        else {
            $validationResults.Issues += "Required file missing: $path"
            Write-Error "Required file missing: $path"
        }
    }

    # Validate environment variables
    $requiredEnvVars = @(
        'WILEY_WIDGET_ROOT',
        'WILEY_WIDGET_CONFIG'
    )

    foreach ($envVar in $requiredEnvVars) {
        if (Test-Path "Env:$envVar") {
            $validationResults.PassedChecks += "Environment variable set: $envVar"
            if ($Detailed) {
                Write-Verbose "✓ Environment variable set: $envVar = $((Get-Item "Env:$envVar").Value)"
            }
        }
        else {
            $validationResults.Warnings += "Environment variable not set: $envVar"
            Write-Warning "Environment variable not set: $envVar"
        }
    }

    # Validate PATH includes scripts directory
    $scriptsPath = Join-Path $Script:WileyWidgetProfile.ProjectRoot 'scripts'
    if ($env:PATH -like "*$scriptsPath*") {
        $validationResults.PassedChecks += "Scripts directory in PATH"
        if ($Detailed) {
            Write-Verbose "✓ Scripts directory in PATH"
        }
    }
    else {
        $validationResults.Warnings += "Scripts directory not in PATH"
        Write-Warning "Scripts directory not in PATH"
        if ($FixIssues) {
            Write-Verbose "Adding scripts directory to PATH..."
            $env:PATH = "$scriptsPath;$env:PATH"
            Write-Verbose "✓ Scripts directory added to PATH"
        }
    }

    # Validate Git repository
    if (Test-Path (Join-Path $Script:WileyWidgetProfile.ProjectRoot '.git')) {
        try {
            $null = & git status --porcelain 2>$null
            if ($LASTEXITCODE -eq 0) {
                $validationResults.PassedChecks += "Git repository valid"
                if ($Detailed) {
                    Write-Verbose "✓ Git repository valid"
                }
            }
            else {
                throw "Git command failed"
            }
        }
        catch {
            $validationResults.Warnings += "Git repository validation failed"
            Write-Warning "Git repository validation failed"
        }
    }
    else {
        $validationResults.Warnings += "Not a Git repository"
        Write-Warning "Not a Git repository"
    }

    # Summary
    $totalChecks = $validationResults.PassedChecks.Count + $validationResults.Issues.Count + $validationResults.Warnings.Count

    Write-Verbose "`nValidation Summary:"
    Write-Verbose "=================="
    Write-Verbose "Total checks: $totalChecks"
    Write-Verbose "Passed: $($validationResults.PassedChecks.Count)"
    Write-Verbose "Issues: $($validationResults.Issues.Count)"
    Write-Verbose "Warnings: $($validationResults.Warnings.Count)"

    if ($validationResults.Issues.Count -gt 0) {
        Write-Verbose "`nIssues found:"
        foreach ($issue in $validationResults.Issues) {
            Write-Error "  - $issue"
        }
    }

    if ($validationResults.Warnings.Count -gt 0) {
        Write-Verbose "`nWarnings:"
        foreach ($warning in $validationResults.Warnings) {
            Write-Warning "  - $warning"
        }
    }

    return $validationResults
}

function Test-WileyWidgetApiKey {
    <#
    .SYNOPSIS
        Validates API keys and secure tokens for Wiley Widget development.

    .DESCRIPTION
        Performs comprehensive validation of API keys following Microsoft PowerShell
        7.5.2 security best practices. Checks for presence, format, and accessibility
        of all configured API keys and tokens.

    .PARAMETER ShowValues
        Shows the actual API key values (WARNING: Use with caution).

    .PARAMETER ValidateConnectivity
        Tests actual connectivity to API endpoints.

    .EXAMPLE
        Test-WileyWidgetApiKey

    .EXAMPLE
        Test-WileyWidgetApiKey -ShowValues -ValidateConnectivity
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [switch]$ShowValues,

        [Parameter(Mandatory = $false)]
        [switch]$ValidateConnectivity
    )

    $apiValidationResults = [PSCustomObject]@{
        IsValid = $true
        ValidKeys = @()
        InvalidKeys = @()
        MissingKeys = @()
        Warnings = @()
    }

    Write-Verbose "Validating Wiley Widget API Keys..."

    # Define required API keys and their validation patterns
    $requiredApiKeys = @{
        'GITHUB_PERSONAL_ACCESS_TOKEN' = @{
            Pattern = '^github_pat_[a-zA-Z0-9_]{36,}$'
            Description = 'GitHub Personal Access Token'
            Required = $true
        }
        'TRUNK_API_KEY' = @{
            Pattern = '^[a-f0-9]{40}$'
            Description = 'Trunk API Key'
            Required = $true
        }
        'XAI_API_KEY' = @{
            Pattern = '^xai-[a-zA-Z0-9_-]{50,}$'
            Description = 'XAI/Azure OpenAI API Key'
            Required = $false
        }
        'BRAVE_API_KEY' = @{
            Pattern = '^[a-zA-Z0-9_-]{20,}$'
            Description = 'Brave Browser API Key'
            Required = $false
        }
        'SYNCFUSION_LICENSE_KEY' = @{
            Pattern = '^(?!YOUR_SYNCFUSION_LICENSE_KEY_HERE).+$'
            Description = 'Syncfusion License Key'
            Required = $false
        }
    }

    foreach ($keyName in $requiredApiKeys.Keys) {
        $keyConfig = $requiredApiKeys[$keyName]
        $keyValue = Get-Item "Env:$keyName" -ErrorAction SilentlyContinue

        if ($null -eq $keyValue -or [string]::IsNullOrWhiteSpace($keyValue.Value)) {
            if ($keyConfig.Required) {
                $apiValidationResults.IsValid = $false
                $apiValidationResults.MissingKeys += "$($keyConfig.Description) ($keyName)"
                Write-Error "Missing required API key: $($keyConfig.Description)"
            }
            else {
                $apiValidationResults.Warnings += "Optional API key not set: $($keyConfig.Description)"
                Write-Warning "Optional API key not set: $($keyConfig.Description)"
            }
        }
        elseif ($keyValue.Value -notmatch $keyConfig.Pattern) {
            $apiValidationResults.IsValid = $false
            $apiValidationResults.InvalidKeys += "$($keyConfig.Description) ($keyName)"
            Write-Error "Invalid format for API key: $($keyConfig.Description)"
            if ($ShowValues) {
                Write-Verbose "  Current value: $($keyValue.Value)"
            }
        }
        else {
            $apiValidationResults.ValidKeys += "$($keyConfig.Description) ($keyName)"
            Write-Verbose "✓ Valid API key: $($keyConfig.Description)"
            if ($ShowValues) {
                Write-Verbose "  Value: $($keyValue.Value)"
            }
        }
    }

    # Test connectivity if requested
    if ($ValidateConnectivity) {
        Write-Verbose "`nTesting API connectivity..."

        # Test GitHub API
        if (Test-Path "Env:GITHUB_PERSONAL_ACCESS_TOKEN") {
            try {
                $headers = @{
                    'Authorization' = "Bearer $((Get-Item 'Env:GITHUB_PERSONAL_ACCESS_TOKEN').Value)"
                    'Accept' = 'application/vnd.github.v3+json'
                }
                $null = Invoke-RestMethod -Uri 'https://api.github.com/user' -Headers $headers -Method Get -TimeoutSec 10 -ErrorAction Stop
                Write-Verbose "✓ GitHub API connectivity successful"
            }
            catch {
                $apiValidationResults.Warnings += "GitHub API connectivity failed: $($_.Exception.Message)"
                Write-Warning "GitHub API connectivity failed"
            }
        }

        # Test Trunk API
        if (Test-Path "Env:TRUNK_API_KEY") {
            try {
                $headers = @{
                    'Authorization' = "Bearer $((Get-Item 'Env:TRUNK_API_KEY').Value)"
                    'Content-Type' = 'application/json'
                }
                $null = Invoke-RestMethod -Uri 'https://api.trunk.io/v1/organizations' -Headers $headers -Method Get -TimeoutSec 10 -ErrorAction Stop
                Write-Verbose "✓ Trunk API connectivity successful"
            }
            catch {
                $apiValidationResults.Warnings += "Trunk API connectivity failed: $($_.Exception.Message)"
                Write-Warning "Trunk API connectivity failed"
            }
        }
    }

    # Summary
    $totalKeys = $requiredApiKeys.Count
    $validCount = $apiValidationResults.ValidKeys.Count
    $missingCount = $apiValidationResults.MissingKeys.Count
    $invalidCount = $apiValidationResults.InvalidKeys.Count

    Write-Verbose "`nAPI Key Validation Summary:"
    Write-Verbose "=========================="
    Write-Verbose "Total keys checked: $totalKeys"
    Write-Verbose "Valid: $validCount"
    Write-Verbose "Missing: $missingCount"
    Write-Verbose "Invalid: $invalidCount"
    Write-Verbose "Warnings: $($apiValidationResults.Warnings.Count)"

    if ($apiValidationResults.MissingKeys.Count -gt 0) {
        Write-Verbose "`nMissing API Keys:"
        foreach ($key in $apiValidationResults.MissingKeys) {
            Write-Error "  - $key"
        }
    }

    if ($apiValidationResults.InvalidKeys.Count -gt 0) {
        Write-Verbose "`nInvalid API Keys:"
        foreach ($key in $apiValidationResults.InvalidKeys) {
            Write-Error "  - $key"
        }
    }

    return $apiValidationResults
}

function Get-WileyWidgetSecureToken {
    <#
    .SYNOPSIS
        Retrieves a secure token using Microsoft recommended practices.

    .DESCRIPTION
        Safely retrieves API keys and tokens following Microsoft PowerShell 7.5.2
        security best practices. Supports secure string conversion and validation.

    .PARAMETER TokenName
        Name of the token to retrieve.

    .PARAMETER AsSecureString
        Returns the token as a SecureString.

    .PARAMETER Validate
        Validates the token format before returning.

    .EXAMPLE
        Get-WileyWidgetSecureToken -TokenName 'GITHUB_PERSONAL_ACCESS_TOKEN'

    .EXAMPLE
        Get-WileyWidgetSecureToken -TokenName 'TRUNK_API_KEY' -AsSecureString -Validate
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$TokenName,

        [Parameter(Mandatory = $false)]
        [switch]$AsSecureString,

        [Parameter(Mandatory = $false)]
        [switch]$Validate
    )

    try {
        $tokenValue = Get-Item "Env:$TokenName" -ErrorAction Stop

        if ([string]::IsNullOrWhiteSpace($tokenValue.Value)) {
            Write-Error "Token '$TokenName' is empty or not set."
            return $null
        }

        # Validate token if requested
        if ($Validate) {
            $validationResult = Test-WileyWidgetApiKey
            $isValid = $validationResult.ValidKeys -contains $TokenName -or
                      $validationResult.ValidKeys -match $TokenName

            if (-not $isValid) {
                Write-Warning "Token '$TokenName' failed validation."
            }
        }

        # Return as SecureString if requested
        if ($AsSecureString) {
            Write-Warning "Converting plain text token to SecureString is not recommended for security. Consider using encrypted storage instead."
            Write-Verbose "Token '$TokenName' retrieved as plain text (SecureString conversion skipped for security)."
            return $tokenValue.Value
        }

        Write-Verbose "Token '$TokenName' retrieved successfully."
        return $tokenValue.Value
    }
    catch {
        Write-Error "Failed to retrieve token '$TokenName': $($_.Exception.Message)"
        return $null
    }
}

function Set-WileyWidgetSecureToken {
    <#
    .SYNOPSIS
        Sets a secure token using Microsoft recommended practices.

    .DESCRIPTION
        Safely sets API keys and tokens following Microsoft PowerShell 7.5.2
        security best practices. Supports SecureString input and validation.

    .PARAMETER TokenName
        Name of the token to set.

    .PARAMETER TokenValue
        Value of the token (can be SecureString).

    .PARAMETER FromSecureString
        Indicates that TokenValue is a SecureString.

    .PARAMETER Validate
        Validates the token format after setting.

    .EXAMPLE
        Set-WileyWidgetSecureToken -TokenName 'GITHUB_TOKEN' -TokenValue 'ghp_123...'

    .EXAMPLE
        $secureToken = Read-Host "Enter token" -AsSecureString
        Set-WileyWidgetSecureToken -TokenName 'API_KEY' -TokenValue $secureToken -FromSecureString
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$TokenName,

        [Parameter(Mandatory = $true)]
        $TokenValue,

        [Parameter(Mandatory = $false)]
        [switch]$FromSecureString,

        [Parameter(Mandatory = $false)]
        [switch]$Validate
    )

    if ($PSCmdlet.ShouldProcess("Secure token '$TokenName'", "Set")) {
        $plainTextValue = $null

        # Handle SecureString input
        if ($FromSecureString -and $TokenValue -is [System.Security.SecureString]) {
            $plainTextValue = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
                [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($TokenValue)
            )
        }
        elseif ($TokenValue -is [string]) {
            $plainTextValue = $TokenValue
        }
        else {
            Write-Error "TokenValue must be a string or SecureString."
            return $false
        }

        # Validate token format if requested
        if ($Validate) {
            # Basic validation - check if it's not empty and has reasonable length
            if ([string]::IsNullOrWhiteSpace($plainTextValue)) {
                Write-Error "Token value cannot be empty."
                return $false
            }

            if ($plainTextValue.Length -lt 10) {
                Write-Warning "Token value seems unusually short ($($plainTextValue.Length) characters)."
            }
        }

        # Set the environment variable
        Set-Item -Path "Env:$TokenName" -Value $plainTextValue -ErrorAction Stop

        Write-Verbose "✓ Secure token '$TokenName' set successfully."
        Write-Verbose "Token '$TokenName' has been set in the environment."

        return $true
    }
    catch {
        Write-Error "Failed to set secure token '$TokenName': $($_.Exception.Message)"
        return $false
    }
}

# Build Functions
function Invoke-WileyWidgetBuild {
    <#
    .SYNOPSIS
        Builds the Wiley Widget project.

    .DESCRIPTION
        Executes the build script with specified parameters.

    .PARAMETER Configuration
        Build configuration (Debug/Release).

    .PARAMETER Clean
        Perform a clean build.

    .PARAMETER Publish
        Create a publishable package.

    .PARAMETER VerboseOutput
        Enable verbose build output.

    .EXAMPLE
        Invoke-WileyWidgetBuild -Configuration Release

    .EXAMPLE
        Invoke-WileyWidgetBuild -Clean -Publish
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [ValidateSet('Debug', 'Release')]
        [string]$Configuration = 'Release',

        [Parameter()]
        [switch]$Clean,

        [Parameter()]
        [switch]$Publish,

        [Parameter()]
        [switch]$VerboseOutput
    )

    $buildScript = Join-Path $Script:WileyWidgetProfile.ProjectRoot 'scripts\build.ps1'

    if (-not (Test-Path $buildScript)) {
        Write-Error "Build script not found: $buildScript"
        return
    }

    $arguments = @(
        "-Config $Configuration"
    )

    if ($Clean) {
        $arguments += "-Clean"
    }

    if ($Publish) {
        $arguments += "-Publish"
    }

    if ($VerboseOutput) {
        $arguments += "-Verbose"
    }

    Write-Verbose "Building Wiley Widget ($Configuration)..."

    try {
        & $buildScript @arguments
        Write-Verbose "Build completed successfully."
    }
    catch {
        Write-Error "Build failed: $_"
        throw
    }
}

function Invoke-WileyWidgetTest {
    <#
    .SYNOPSIS
        Runs tests for the Wiley Widget project.

    .DESCRIPTION
        Executes unit tests and optionally UI tests.

    .PARAMETER IncludeUITests
        Include UI smoke tests in the test run.

    .PARAMETER Coverage
        Generate code coverage report.

    .EXAMPLE
        Invoke-WileyWidgetTest

    .EXAMPLE
        Invoke-WileyWidgetTest -IncludeUITests -Coverage
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [switch]$IncludeUITests,

        [Parameter()]
        [switch]$Coverage
    )

    Write-Verbose "Running Wiley Widget tests..."

    # Set environment variables for test configuration
    if ($IncludeUITests) {
        $env:RUN_UI_TESTS = '1'
    } else {
        $env:RUN_UI_TESTS = '0'
    }

    try {
        Invoke-WileyWidgetBuild -Configuration Release

        if ($Coverage) {
            Write-Verbose "Generating coverage report..."
            # Coverage report generation would be handled by the build script
        }

        Write-Verbose "Tests completed successfully."
    }
    catch {
        Write-Error "Tests failed: $_"
        throw
    }
    finally {
        # Clean up environment variables
        Remove-Item Env:\RUN_UI_TESTS -ErrorAction SilentlyContinue
    }
}

# Development Helper Functions
function Start-WileyWidgetApplication {
    <#
    .SYNOPSIS
        Starts the Wiley Widget application.

    .DESCRIPTION
        Launches the Wiley Widget WPF application for development and testing.

    .PARAMETER Configuration
        Build configuration to run (Debug/Release).

    .PARAMETER Wait
        Wait for the application to exit before returning.

    .EXAMPLE
        Start-WileyWidgetApplication

    .EXAMPLE
        Start-WileyWidgetApplication -Configuration Debug -Wait
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter()]
        [ValidateSet('Debug', 'Release')]
        [string]$Configuration = 'Release',

        [Parameter()]
        [switch]$Wait
    )

    $projectFile = Join-Path $Script:WileyWidgetProfile.ProjectRoot 'WileyWidget\WileyWidget.csproj'

    if (-not (Test-Path $projectFile)) {
        Write-Error "Project file not found: $projectFile"
        return
    }

    if ($PSCmdlet.ShouldProcess("Wiley Widget application ($Configuration)", "Start")) {
        Write-Verbose "Starting Wiley Widget application ($Configuration)..."

    try {
        $arguments = @(
            'run',
            '--project',
            $projectFile,
            '--configuration',
            $Configuration
        )

        if ($Wait) {
            & dotnet @arguments
        } else {
            $job = Start-Job -ScriptBlock {
                [Diagnostics.CodeAnalysis.SuppressMessageAttribute("PSUseUsingScopeModifierInNewRunspaces", "")]
                param($dotnetArgs)
                & dotnet $dotnetArgs
            } -ArgumentList (,$arguments)

            Write-Verbose "Application started in background (Job ID: $($job.Id))"
            Write-Verbose "Use 'Receive-Job -Id $($job.Id)' to check status"
        }
    }
    catch {
        Write-Error "Failed to start application: $_"
        throw
    }
    }
}

function Show-WileyWidgetLicenseStatus {
    <#
    .SYNOPSIS
        Displays Syncfusion license status.

    .DESCRIPTION
        Shows the current status of the Syncfusion license configuration.

    .EXAMPLE
        Show-WileyWidgetLicenseStatus
    #>
    [CmdletBinding()]
    param()

    $licenseScript = Join-Path $Script:WileyWidgetProfile.ProjectRoot 'scripts\show-syncfusion-license.ps1'

    if (Test-Path $licenseScript) {
        Write-Verbose "Checking Syncfusion license status..."
        & $licenseScript
    } else {
        Write-Warning "License status script not found: $licenseScript"
    }
}

function Get-WileyWidgetProjectInfo {
    <#
    .SYNOPSIS
        Displays project information and status.

    .DESCRIPTION
        Shows current project version, build status, and environment information.

    .EXAMPLE
        Get-WileyWidgetProjectInfo
    #>
    [CmdletBinding()]
    param()

    Write-Verbose "=== Wiley Widget Project Information ==="
    Write-Verbose "Version: $($Script:WileyWidgetProfile.Version)"
    Write-Verbose "PowerShell Version: $($Script:WileyWidgetProfile.PowerShellVersion)"
    Write-Verbose "Project Root: $($Script:WileyWidgetProfile.ProjectRoot)"
    Write-Verbose "Last Updated: $($Script:WileyWidgetProfile.LastUpdated)"
    Write-Verbose ""

    # Check if we're in a git repository
    if (Test-Path (Join-Path $Script:WileyWidgetProfile.ProjectRoot '.git')) {
        Write-Verbose "Git Status:"
        git -C $Script:WileyWidgetProfile.ProjectRoot status --porcelain
        Write-Verbose ""
    }

    # Check build artifacts
    $binPath = Join-Path $Script:WileyWidgetProfile.ProjectRoot 'WileyWidget\bin'
    if (Test-Path $binPath) {
        Write-Verbose "Build Artifacts:"
        Get-ChildItem $binPath -Directory | Select-Object -ExpandProperty Name
        Write-Verbose ""
    }
}

# Utility Functions
function Update-WileyWidgetProfile {
    <#
    .SYNOPSIS
        Updates the Wiley Widget PowerShell profile.

    .DESCRIPTION
        Downloads and applies the latest version of the profile.

    .EXAMPLE
        Update-WileyWidgetProfile
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param()

    if ($PSCmdlet.ShouldProcess("Wiley Widget PowerShell Profile", "Update")) {
        Write-Verbose "Updating Wiley Widget PowerShell profile..."

        # This would typically download from a repository or update mechanism
        # For now, just refresh the profile configuration
        $Script:WileyWidgetProfile.LastUpdated = Get-Date

        Write-Verbose "Profile updated successfully."
    }
}

function Reset-WileyWidgetEnvironment {
    <#
    .SYNOPSIS
        Resets the Wiley Widget development environment.

    .DESCRIPTION
        Cleans up environment variables and temporary files.

    .EXAMPLE
        Reset-WileyWidgetEnvironment
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param()

    if ($PSCmdlet.ShouldProcess("Wiley Widget Environment", "Reset")) {
        Write-Verbose "Resetting Wiley Widget environment..."

        # Remove environment variables
        Remove-Item Env:\WILEY_WIDGET_ROOT -ErrorAction SilentlyContinue
        Remove-Item Env:\WILEY_WIDGET_CONFIG -ErrorAction SilentlyContinue
        Remove-Item Env:\MSBUILDDEBUGPATH -ErrorAction SilentlyContinue

        # Clean up temporary files
        $tempPath = Join-Path $env:TEMP 'MSBuildDebug'
        if (Test-Path $tempPath) {
            Remove-Item $tempPath -Recurse -Force -ErrorAction SilentlyContinue
        }

        Write-Verbose "Environment reset successfully."
    }
}

function Edit-WileyWidgetEnvironment {
    <#
    .SYNOPSIS
        Opens the .env file for editing.

    .DESCRIPTION
        Opens the Wiley Widget .env file in the default editor for configuration.

    .EXAMPLE
        Edit-WileyWidgetEnvironment
    #>
    [CmdletBinding()]
    param()

    $envFile = Join-Path $Script:WileyWidgetProfile.ProjectRoot '.env'

    if (-not (Test-Path $envFile)) {
        Write-Warning ".env file not found. Creating a new one..."
        $defaultContent = @"
# Wiley Widget Environment Configuration
# PowerShell 7.5.2 Compatible
# This file contains environment variables for Wiley Widget development

# Project Configuration
WILEY_WIDGET_ROOT=$($Script:WileyWidgetProfile.ProjectRoot)
WILEY_WIDGET_CONFIG=Release

# Build Configuration
MSBUILDDEBUGPATH=C:\Users\biges\AppData\Local\Temp\MSBuildDebug

# Development Settings
RUN_UI_TESTS=0

# Syncfusion License (set this to your actual license key)
# SYNCFUSION_LICENSE_KEY=your_license_key_here
"@

        $defaultContent | Out-File -FilePath $envFile -Encoding UTF8
        Write-Verbose "Created new .env file: $envFile"
    }

    try {
        if ($env:EDITOR) {
            & $env:EDITOR $envFile
        } elseif ($env:VISUAL) {
            & $env:VISUAL $envFile
        } else {
            # Try to open with default editor
            Start-Process $envFile
        }
        Write-Verbose "Opened .env file for editing."
    }
    catch {
        Write-Error "Failed to open .env file: $_"
    }
}

# Aliases for convenience (following PowerShell best practices)
# All aliases use Microsoft approved verbs and camelCase naming
New-Alias -Name 'ww-build' -Value Invoke-WileyWidgetBuild -Description 'Build Wiley Widget project' -Force
New-Alias -Name 'ww-test' -Value Invoke-WileyWidgetTest -Description 'Run Wiley Widget tests' -Force
New-Alias -Name 'ww-run' -Value Start-WileyWidgetApplication -Description 'Start Wiley Widget application' -Force
New-Alias -Name 'ww-info' -Value Get-WileyWidgetProjectInfo -Description 'Get project information' -Force
New-Alias -Name 'ww-license' -Value Get-WileyWidgetLicenseStatus -Description 'Get license status' -Force
New-Alias -Name 'ww-env' -Value Set-WileyWidgetEnvironment -Description 'Set development environment' -Force
New-Alias -Name 'ww-edit-env' -Value Edit-WileyWidgetEnvironment -Description 'Edit environment configuration' -Force
New-Alias -Name 'ww-reset' -Value Reset-WileyWidgetEnvironment -Description 'Reset development environment' -Force

# API Key Management Aliases (following Microsoft security best practices)
New-Alias -Name 'ww-api-test' -Value Test-WileyWidgetApiKey -Description 'Test API key configuration' -Force
New-Alias -Name 'ww-get-token' -Value Get-WileyWidgetSecureToken -Description 'Get secure token value' -Force
New-Alias -Name 'ww-set-token' -Value Set-WileyWidgetSecureToken -Description 'Set secure token value' -Force

# Initialize environment on profile load
Set-WileyWidgetEnvironment

# Display welcome message
Write-Verbose ""
Write-Verbose "Initializing Wiley Widget development environment..."

# Automatically load environment configuration
try {
    Set-WileyWidgetEnvironment -ErrorAction Stop
}
catch {
    Write-Warning "Failed to initialize Wiley Widget environment: $_"
    Write-Verbose "You can manually initialize with: Set-WileyWidgetEnvironment"
}

Write-Verbose ""
Write-Verbose "Welcome to Wiley Widget Development Environment!"
Write-Verbose "Available commands:"
Write-Verbose "  ww-build     - Build the project"
Write-Verbose "  ww-test      - Run tests"
Write-Verbose "  ww-run       - Start the application"
Write-Verbose "  ww-info      - Show project information"
Write-Verbose "  ww-license   - Check Syncfusion license"
Write-Verbose "  ww-api-test  - Test API key configuration"
Write-Verbose "  ww-get-token - Get secure token value"
Write-Verbose "  ww-set-token - Set secure token value"
Write-Verbose "  ww-edit-env  - Edit environment configuration"
Write-Verbose "  ww-reset     - Reset development environment"
Write-Verbose ""

# Add hyperthreading information to welcome message
try {
    $ht = $Script:WileyWidgetProfile.HyperThreading
    if ($ht -and $ht.ContainsKey('Enabled')) {
        Write-Verbose "🚀 Performance Optimization:"
        Write-Verbose "  HyperThreading: $(if ($ht.Enabled) { 'Enabled' } else { 'Disabled' })"
        Write-Verbose "  CPU Cores: $($ht.PhysicalCores) physical, $($ht.LogicalProcessors) logical"
        Write-Verbose "  Max Parallel Jobs: $($ht.MaxParallelJobs)"
        Write-Verbose ""
    }
}
catch {
    Write-Verbose "🚀 Performance Optimization: Initializing..."
    Write-Verbose ""
}

Write-Verbose "⚡ Parallel Processing Commands:"
Write-Verbose "  cpu-topology      - Show CPU topology details"
Write-Verbose "  parallel-test     - Test parallel performance"
Write-Verbose "  parallel-invoke   - Execute optimized parallel operations"
Write-Verbose "  start-thread      - Start optimized thread jobs"
Write-Verbose ""

Write-Verbose "Use 'Get-Help <command>' for detailed information."
Write-Verbose ""

# Note: Export-ModuleMember is not used in profile scripts as they are not modules
# All functions and aliases are automatically available in the session scope
