<#
.SYNOPSIS
    Production-ready Docker UI test runner for Wiley Widget WPF application.

.DESCRIPTION
    Builds and executes FlaUI-based UI tests in a Windows container with
    .NET 9.0 Desktop Runtime. Handles container lifecycle, result collection,
    and comprehensive error handling using modern PowerShell 7.5.4 patterns.

.PARAMETER Action
    Action to perform: Build, Run, BuildAndRun, Clean, or Status
    Default: BuildAndRun

.PARAMETER TestFilter
    xUnit test filter expression (e.g., "FullyQualifiedName~MunicipalAccountView")
    Default: Empty (runs all tests)

.PARAMETER Verbosity
    Test output verbosity: quiet, minimal, normal, detailed, diagnostic
    Default: detailed

.PARAMETER NoCache
    Force rebuild without using Docker cache

.PARAMETER KeepContainer
    Keep container after execution for debugging

.PARAMETER TimeoutMinutes
    Test execution timeout in minutes
    Default: 30

.PARAMETER IncludeSqlServer
    Start SQL Server test container
    Default: True

.EXAMPLE
    .\Run-DockerUiTest.ps1 -Action BuildAndRun
    Build image and run all UI tests

.EXAMPLE
    .\Run-DockerUiTest.ps1 -TestFilter "Category=UI" -Verbosity normal
    Run only UI category tests with normal verbosity

.EXAMPLE
    .\Run-DockerUiTest.ps1 -Action Build -NoCache
    Rebuild Docker image without cache

.NOTES
    Author: Wiley Widget Team
    Version: 2.0.0
    Requires: PowerShell 7.5+, Docker Desktop with Windows containers enabled
    PSScriptAnalyzer: Compliant
#>

#Requires -Version 7.5

[CmdletBinding(SupportsShouldProcess)]
[OutputType([int])]
param(
    [Parameter()]
    [ValidateSet('Build', 'Run', 'BuildAndRun', 'Clean', 'Status')]
    [string]$Action = 'BuildAndRun',

    [Parameter()]
    [string]$TestFilter = '',

    [Parameter()]
    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$Verbosity = 'detailed',

    [Parameter()]
    [switch]$NoCache,

    [Parameter()]
    [switch]$KeepContainer,

    [Parameter()]
    [ValidateRange(1, 120)]
    [int]$TimeoutMinutes = 30,

    [Parameter()]
    [bool]$IncludeSqlServer = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$InformationPreference = 'Continue'

# ============================================================================
# CONSTANTS & CONFIGURATION
# ============================================================================
$script:RepoRoot = Split-Path -Parent $PSScriptRoot
$script:DockerDir = Join-Path $script:RepoRoot 'docker'
$script:ComposeFile = Join-Path $script:DockerDir 'docker-compose.uitest.yml'
$script:Dockerfile = Join-Path $script:DockerDir 'Dockerfile.uitest'
$script:TestResultsDir = Join-Path $script:RepoRoot 'TestResults'
$script:TestLogsDir = Join-Path $script:RepoRoot 'test-logs'
$script:CoverageDir = Join-Path $script:RepoRoot 'coverage'

$script:ImageName = 'wileywidget/uitest:latest'
$script:ContainerName = 'wiley-widget-uitest'
$script:SqlServerContainer = 'wiley-widget-sqlserver-test'

# ============================================================================
# UTILITY FUNCTIONS
# ============================================================================

function Write-StatusMessage {
    <#
    .SYNOPSIS
        Writes a formatted information message
    .PARAMETER Message
        The message to write
    .PARAMETER Type
        Message type: Info, Success, Warning, Error, Header
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter()]
        [ValidateSet('Info', 'Success', 'Warning', 'Error', 'Header', 'Step')]
        [string]$Type = 'Info'
    )

    $prefix = switch ($Type) {
        'Success' { '✓' }
        'Warning' { '⚠' }
        'Error' { '✗' }
        'Step' { '►' }
        default { '' }
    }

    $tags = switch ($Type) {
        'Success' { @{ ForegroundColor = 'Green' } }
        'Warning' { @{ ForegroundColor = 'Yellow' } }
        'Error' { @{ ForegroundColor = 'Red' } }
        'Header' { @{ ForegroundColor = 'Cyan' } }
        'Step' { @{ ForegroundColor = 'Yellow' } }
        default { @{} }
    }

    if ($Type -eq 'Header') {
        Write-Information ''
        Write-Information ('═' * 67) @tags
        Write-Information "  $Message" @tags
        Write-Information ('═' * 67) @tags
        Write-Information ''
    }
    else {
        $fullMessage = if ($prefix) { "$prefix $Message" } else { $Message }
        Write-Information $fullMessage @tags
    }
}

function Test-WindowsContainerMode {
    <#
    .SYNOPSIS
        Verifies Docker is running in Windows container mode
    .OUTPUTS
        Boolean indicating if Windows containers are enabled
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    Write-StatusMessage 'Checking Docker Windows container mode...' -Type Step

    try {
        $dockerInfo = & docker info 2>&1 | Out-String

        if ($dockerInfo -match 'OSType:\s*windows') {
            Write-StatusMessage 'Docker is in Windows container mode' -Type Success
            return $true
        }
        else {
            Write-StatusMessage 'Docker is not in Windows container mode' -Type Error
            Write-Information '  Please switch to Windows containers:'
            Write-Information '  Right-click Docker Desktop tray icon → Switch to Windows containers'
            return $false
        }
    }
    catch {
        Write-StatusMessage 'Docker is not running or not installed' -Type Error
        Write-Information "  Error: $_"
        return $false
    }
}

function Test-Prerequisite {
    <#
    .SYNOPSIS
        Validates all prerequisites for Docker UI test execution
    .OUTPUTS
        Boolean indicating if all prerequisites are met
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    Write-StatusMessage 'Checking Prerequisites' -Type Header

    $allValid = $true

    # Check Docker
    Write-StatusMessage 'Checking Docker installation...' -Type Step
    if (Get-Command docker -ErrorAction SilentlyContinue) {
        $dockerVersion = & docker --version
        Write-StatusMessage "Docker found: $dockerVersion" -Type Success
    }
    else {
        Write-StatusMessage 'Docker not found in PATH' -Type Error
        $allValid = $false
    }

    # Check Docker Compose
    Write-StatusMessage 'Checking Docker Compose...' -Type Step
    if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
        $composeVersion = & docker-compose --version
        Write-StatusMessage "Docker Compose found: $composeVersion" -Type Success
    }
    else {
        Write-StatusMessage 'Docker Compose not found' -Type Error
        $allValid = $false
    }

    # Check Windows containers
    if (-not (Test-WindowsContainerMode)) {
        $allValid = $false
    }

    # Check required files
    Write-StatusMessage 'Checking required files...' -Type Step
    $requiredFiles = @($script:ComposeFile, $script:Dockerfile)
    foreach ($file in $requiredFiles) {
        if (Test-Path $file) {
            Write-StatusMessage "Found: $(Split-Path -Leaf $file)" -Type Success
        }
        else {
            Write-StatusMessage "Missing: $file" -Type Error
            $allValid = $false
        }
    }

    # Check Syncfusion license
    Write-StatusMessage 'Checking Syncfusion license...' -Type Step
    if ($env:SYNCFUSION_LICENSE_KEY) {
        Write-StatusMessage 'SYNCFUSION_LICENSE_KEY environment variable is set' -Type Success
    }
    else {
        Write-StatusMessage 'SYNCFUSION_LICENSE_KEY not set (tests may fail if using Syncfusion controls)' -Type Warning
    }

    Write-Information ''
    if ($allValid) {
        Write-StatusMessage 'All prerequisites satisfied' -Type Success
    }
    else {
        Write-StatusMessage 'Some prerequisites are missing' -Type Error
        throw 'Prerequisites check failed'
    }

    return $allValid
}

function Initialize-OutputDirectory {
    <#
    .SYNOPSIS
        Creates output directories for test results if they don't exist
    #>
    [CmdletBinding()]
    param()

    Write-StatusMessage 'Initializing output directories...' -Type Step

    $directories = @($script:TestResultsDir, $script:TestLogsDir, $script:CoverageDir)
    foreach ($dir in $directories) {
        if (-not (Test-Path $dir)) {
            $null = New-Item -ItemType Directory -Path $dir -Force
            Write-StatusMessage "Created: $dir" -Type Success
        }
        else {
            Write-StatusMessage "Exists: $dir" -Type Success
        }
    }
}

function Invoke-DockerImageBuild {
    <#
    .SYNOPSIS
        Builds the Docker image for UI tests
    .PARAMETER UseCache
        Whether to use Docker cache (default: true)
    .OUTPUTS
        TimeSpan representing build duration
    #>
    [CmdletBinding()]
    [OutputType([TimeSpan])]
    param(
        [Parameter()]
        [bool]$UseCache = $true
    )

    Write-StatusMessage 'Building Docker Image' -Type Header

    $buildArgs = [System.Collections.Generic.List[string]]::new()
    $buildArgs.AddRange(@('compose', '-f', $script:ComposeFile, 'build'))

    if (-not $UseCache) {
        $buildArgs.Add('--no-cache')
        Write-StatusMessage 'Building with --no-cache (this may take longer)...' -Type Step
    }
    else {
        Write-StatusMessage 'Building with cache enabled...' -Type Step
    }

    $buildArgs.AddRange(@('--progress', 'plain', 'uitest-runner'))

    Write-Information ''
    Write-Information "Build command: docker $($buildArgs -join ' ')"
    Write-Information ''

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    & docker @buildArgs

    if ($LASTEXITCODE -ne 0) {
        throw "Docker build failed with exit code $LASTEXITCODE"
    }

    $stopwatch.Stop()
    Write-Information ''
    Write-StatusMessage "Build completed in $($stopwatch.Elapsed.TotalSeconds.ToString('F2')) seconds" -Type Success

    return $stopwatch.Elapsed
}

function Invoke-DockerTestRun {
    <#
    .SYNOPSIS
        Executes UI tests in Docker container
    .OUTPUTS
        Integer exit code from test execution
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param()

    Write-StatusMessage 'Running UI Tests in Container' -Type Header

    # Set environment variables for the container
    $env:TEST_FILTER = $TestFilter
    $env:TEST_VERBOSITY = $Verbosity

    $runArgs = [System.Collections.Generic.List[string]]::new()
    $runArgs.AddRange(@('compose', '-f', $script:ComposeFile, 'run'))

    if (-not $KeepContainer) {
        $runArgs.Add('--rm')
    }

    # Service selection
    if ($IncludeSqlServer) {
        $runArgs.Add('uitest-runner')
    }
    else {
        $runArgs.AddRange(@('--no-deps', 'uitest-runner'))
    }

    Write-StatusMessage 'Test configuration:' -Type Step
    Write-Information "  Filter: $(if ($TestFilter) { $TestFilter } else { 'All tests' })"
    Write-Information "  Verbosity: $Verbosity"
    Write-Information "  Timeout: $TimeoutMinutes minutes"
    Write-Information "  SQL Server: $IncludeSqlServer"
    Write-Information ''

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    Write-Information "Run command: docker $($runArgs -join ' ')"
    Write-Information ''

    & docker @runArgs

    $exitCode = $LASTEXITCODE
    $stopwatch.Stop()

    Write-Information ''
    Write-Information ('═' * 67)

    if ($exitCode -eq 0) {
        Write-StatusMessage "All tests passed in $($stopwatch.Elapsed.TotalSeconds.ToString('F2')) seconds" -Type Success
    }
    else {
        Write-StatusMessage "Tests failed with exit code $exitCode after $($stopwatch.Elapsed.TotalSeconds.ToString('F2')) seconds" -Type Error
    }

    return $exitCode
}

function Remove-DockerContainer {
    <#
    .SYNOPSIS
        Cleans up Docker containers, images, and volumes
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Write-StatusMessage 'Cleaning Up Containers and Images' -Type Header

    Write-StatusMessage 'Stopping and removing containers...' -Type Step

    # Stop and remove UI test container
    $containerExists = & docker ps -a --format '{{.Names}}' | Select-String -Pattern $script:ContainerName -Quiet
    if ($containerExists) {
        if ($PSCmdlet.ShouldProcess($script:ContainerName, 'Remove container')) {
            $null = & docker stop $script:ContainerName 2>&1
            $null = & docker rm $script:ContainerName 2>&1
            Write-StatusMessage "Removed container: $script:ContainerName" -Type Success
        }
    }

    # Stop and remove SQL Server container
    $sqlExists = & docker ps -a --format '{{.Names}}' | Select-String -Pattern $script:SqlServerContainer -Quiet
    if ($sqlExists) {
        if ($PSCmdlet.ShouldProcess($script:SqlServerContainer, 'Remove container')) {
            $null = & docker stop $script:SqlServerContainer 2>&1
            $null = & docker rm $script:SqlServerContainer 2>&1
            Write-StatusMessage "Removed container: $script:SqlServerContainer" -Type Success
        }
    }

    # Remove networks
    Write-StatusMessage 'Removing networks...' -Type Step
    $null = & docker network rm wiley-widget-uitest-net 2>&1

    # Optionally remove images
    $removeImage = Read-Host "Remove Docker image '$script:ImageName'? (y/N)"
    if ($removeImage -in @('y', 'Y')) {
        if ($PSCmdlet.ShouldProcess($script:ImageName, 'Remove image')) {
            $null = & docker rmi $script:ImageName 2>&1
            Write-StatusMessage "Removed image: $script:ImageName" -Type Success
        }
    }

    # Clean volumes
    $removeVolumes = Read-Host 'Remove Docker volumes? (y/N)'
    if ($removeVolumes -in @('y', 'Y')) {
        if ($PSCmdlet.ShouldProcess('Docker volumes', 'Prune')) {
            $null = & docker volume prune -f 2>&1
            Write-StatusMessage 'Removed unused volumes' -Type Success
        }
    }

    Write-Information ''
    Write-StatusMessage 'Cleanup completed' -Type Success
}

function Get-DockerContainerStatus {
    <#
    .SYNOPSIS
        Retrieves and displays status of Docker containers and test results
    #>
    [CmdletBinding()]
    param()

    Write-StatusMessage 'Container Status' -Type Header

    Write-StatusMessage 'UI Test Container:' -Type Step
    $uiExists = & docker ps -a --format '{{.Names}}' | Select-String -Pattern $script:ContainerName -Quiet
    if ($uiExists) {
        & docker ps -a --filter "name=$script:ContainerName" --format 'table {{.Names}}\t{{.Status}}\t{{.Image}}'
    }
    else {
        Write-Information '  Not found'
    }

    Write-Information ''
    Write-StatusMessage 'SQL Server Container:' -Type Step
    $sqlExists = & docker ps -a --format '{{.Names}}' | Select-String -Pattern $script:SqlServerContainer -Quiet
    if ($sqlExists) {
        & docker ps -a --filter "name=$script:SqlServerContainer" --format 'table {{.Names}}\t{{.Status}}\t{{.Image}}'
    }
    else {
        Write-Information '  Not found'
    }

    Write-Information ''
    Write-StatusMessage 'Docker Images:' -Type Step
    & docker images --filter 'reference=wileywidget/*' --format 'table {{.Repository}}\t{{.Tag}}\t{{.Size}}\t{{.CreatedAt}}'

    Write-Information ''
    Write-StatusMessage 'Test Results:' -Type Step
    if (Test-Path $script:TestResultsDir) {
        $results = Get-ChildItem -Path $script:TestResultsDir -Filter '*.trx' -Recurse
        if ($results) {
            Write-Information "  Found $($results.Count) test result file(s):"
            $results | ForEach-Object { Write-Information "    - $($_.FullName)" }
        }
        else {
            Write-Information '  No test results found'
        }
    }
}

function Export-TestResult {
    <#
    .SYNOPSIS
        Collects and summarizes test results from container execution
    #>
    [CmdletBinding()]
    param()

    Write-StatusMessage 'Collecting Test Results' -Type Header

    Write-StatusMessage 'Verifying results from container...' -Type Step

    if (Test-Path $script:TestResultsDir) {
        $trxFiles = Get-ChildItem -Path $script:TestResultsDir -Filter '*.trx' -Recurse
        $coverageFiles = Get-ChildItem -Path $script:CoverageDir -Filter '*.xml' -Recurse -ErrorAction SilentlyContinue

        Write-StatusMessage 'Test results available:' -Type Success
        Write-Information "  TRX files: $($trxFiles.Count)"
        if ($coverageFiles) {
            Write-Information "  Coverage files: $($coverageFiles.Count)"
        }

        # Generate summary
        foreach ($trx in $trxFiles) {
            Write-Information ''
            Write-StatusMessage "Results from: $($trx.Name)" -Type Step

            # Parse TRX for quick summary
            try {
                [xml]$trxContent = Get-Content $trx.FullName -ErrorAction Stop
                $summary = $trxContent.TestRun.ResultSummary

                if ($summary) {
                    $outcome = $summary.outcome
                    $total = $summary.Counters.total
                    $passed = $summary.Counters.passed
                    $failed = $summary.Counters.failed
                    $skipped = $summary.Counters.inconclusive

                    Write-Information "    Total: $total | Passed: $passed | Failed: $failed | Skipped: $skipped"
                    Write-Information "    Outcome: $outcome"
                }
            }
            catch {
                Write-Information '    (Unable to parse TRX file)'
            }
        }
    }
    else {
        Write-StatusMessage 'No test results directory found' -Type Warning
    }
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

function Invoke-MainExecution {
    <#
    .SYNOPSIS
        Main execution logic for the script
    .OUTPUTS
        Integer exit code
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param()

    try {
        Write-StatusMessage 'Wiley Widget Docker UI Test Runner' -Type Header
        Write-Information "  Action: $Action"
        Write-Information "  Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
        Write-Information ''

        # Check prerequisites for all actions except Status
        if ($Action -ne 'Status') {
            $null = Test-Prerequisite
            Initialize-OutputDirectory
        }

        # Execute requested action
        $exitCode = 0

        switch ($Action) {
            'Build' {
                $null = Invoke-DockerImageBuild -UseCache (-not $NoCache)
            }

            'Run' {
                $exitCode = Invoke-DockerTestRun
                Export-TestResult
            }

            'BuildAndRun' {
                $null = Invoke-DockerImageBuild -UseCache (-not $NoCache)
                Write-Information ''
                $exitCode = Invoke-DockerTestRun
                Export-TestResult
            }

            'Clean' {
                Remove-DockerContainer
            }

            'Status' {
                Get-DockerContainerStatus
            }
        }

        if ($Action -ne 'Run' -and $Action -ne 'BuildAndRun') {
            Write-Information ''
            Write-StatusMessage 'Operation completed successfully' -Type Success
        }

        return $exitCode
    }
    catch {
        Write-Information ''
        Write-StatusMessage "Operation failed: $_" -Type Error
        Write-Information ''
        Write-Information 'Stack trace:' -InformationAction Continue
        Write-Information $_.ScriptStackTrace -InformationAction Continue
        return 1
    }
}

# Execute main function and exit with its return code
exit (Invoke-MainExecution)
