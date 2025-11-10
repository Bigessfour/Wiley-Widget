# CSX Test Discovery and Execution Script for VS Code Test Explorer
# This script helps the Test Explorer extension discover and run CSX tests

[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'Console output is the intended behavior for test reporting')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Justification = 'Function names match test discovery patterns')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '', Justification = 'Discovering multiple tests is the correct plural form')]
[CmdletBinding()]
param(
    [string]$Action = "discover",
    [string]$TestFile = "",
    [string]$TestPattern = "*.csx"
)

$WorkspaceRoot = $env:WW_REPO_ROOT
if (-not $WorkspaceRoot) {
    # Get the true workspace root (two levels up from scripts/tools)
    $WorkspaceRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}

$CSXTestsPath = Join-Path $WorkspaceRoot "scripts/examples/csharp"
$LogsPath = Join-Path $WorkspaceRoot "logs"
$TestLogsPath = Join-Path $WorkspaceRoot "test-logs"

# Ensure directories exist
if (-not (Test-Path $LogsPath)) { New-Item -ItemType Directory -Path $LogsPath -Force | Out-Null }
if (-not (Test-Path $TestLogsPath)) { New-Item -ItemType Directory -Path $TestLogsPath -Force | Out-Null }

function Test-DockerImageAvailable {
    param([string]$ImageName)

    try {
        $imageCheck = docker images -q $ImageName 2>$null
        return ($null -ne $imageCheck -and $imageCheck.Trim() -ne "")
    } catch {
        return $false
    }
}

function Discover-CSXTests {
    Write-Host "Discovering CSX tests in: $CSXTestsPath"

    $tests = Get-ChildItem -Path $CSXTestsPath -Filter $TestPattern | ForEach-Object {
        $testName = $_.BaseName
        $description = ""

        # Try to extract test description from file header
        $content = Get-Content $_.FullName -TotalCount 10
        $descriptionLine = $content | Where-Object { $_ -match "^//\s*(Purpose|Description):\s*(.+)" }
        if ($descriptionLine) {
            $description = ($descriptionLine -split ":\s*", 2)[1].Trim()
        }

        [PSCustomObject]@{
            Name        = $testName
            File        = $_.Name
            FullPath    = $_.FullName
            Description = $description
            Type        = "CSX"
            Category    = "Integration"
        }
    }

    Write-Host "Found $($tests.Count) CSX tests"
    return $tests
}

function Run-CSXTest {
    param([string]$TestFileName)

    Write-Host "Running CSX test: $TestFileName"

    $dockerImage = "wiley-widget/csx-mcp:enhanced"
    $logFile = Join-Path $TestLogsPath "$($TestFileName -replace '\.csx$', '.log')"

    # Validate Docker image exists
    if (-not (Test-DockerImageAvailable -ImageName $dockerImage)) {
        Write-Host "Docker image '$dockerImage' not found. Building image..." -ForegroundColor Yellow
        Write-Host "Run: docker build -t $dockerImage -f docker/Dockerfile.csx-tests ." -ForegroundColor Cyan

        $testResult = @{
            TestName  = $TestFileName
            StartTime = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
            EndTime   = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
            Duration  = 0
            ExitCode  = -1
            Output    = "Docker image not available. Please build the image first."
            Status    = "IMAGE_NOT_FOUND"
        }
        $testResult | ConvertTo-Json -Depth 3 | Out-File $logFile -Encoding UTF8
        Write-Error "Docker image not available"
        return $testResult
    }

    # Prepare Docker command with full repo access for path resolution
    # Mount workspace as read-only (ro) and logs as read-write (rw)
    $dockerArgs = @(
        "run", "--rm"
        "-w", "/app"
        "-v", "${WorkspaceRoot}:/app:ro"
        "-v", "${LogsPath}:/logs:rw"
        "-v", "${TestLogsPath}:/test-logs:rw"
        "-e", "WW_REPO_ROOT=/app"
        "-e", "WW_LOGS_DIR=/logs"
        "-e", "CSX_ALLOWED_PATH=/app"
        $dockerImage
        "scripts/examples/csharp/$TestFileName"
    )

    Write-Host "Executing: docker $($dockerArgs -join ' ')"

    # Run the test and capture output
    $startTime = Get-Date
    try {
        $output = & docker @dockerArgs 2>&1
        $exitCode = $LASTEXITCODE
        $endTime = Get-Date
        $duration = $endTime - $startTime

        # Parse compilation errors and runtime errors separately
        $compilationErrors = @()
        $runtimeErrors = @()
        $outputString = $output -join "`n"

        # Extract C# compilation errors
        if ($outputString -match "error CS\d+") {
            $compilationErrors = ($outputString -split "`n" | Where-Object { $_ -match "error CS\d+" })
        }

        # Extract runtime exceptions
        if ($outputString -match "Exception|Error:") {
            $runtimeErrors = ($outputString -split "`n" | Where-Object { $_ -match "Exception|Error:" })
        }

        # Write results to log file
        $testResult = @{
            TestName          = $TestFileName
            StartTime         = $startTime.ToString("yyyy-MM-dd HH:mm:ss")
            EndTime           = $endTime.ToString("yyyy-MM-dd HH:mm:ss")
            Duration          = $duration.TotalSeconds
            ExitCode          = $exitCode
            Output            = $outputString
            CompilationErrors = $compilationErrors
            RuntimeErrors     = $runtimeErrors
            Status            = if ($exitCode -eq 0) { "PASSED" }
            elseif ($compilationErrors.Count -gt 0) { "COMPILATION_ERROR" }
            elseif ($runtimeErrors.Count -gt 0) { "RUNTIME_ERROR" }
            else { "FAILED" }
        }

        $testResult | ConvertTo-Json -Depth 3 | Out-File $logFile -Encoding UTF8

        # Output for Test Explorer
        Write-Host "Test Result: $($testResult.Status)" -ForegroundColor $(
            switch ($testResult.Status) {
                "PASSED" { "Green" }
                "COMPILATION_ERROR" { "Yellow" }
                "RUNTIME_ERROR" { "Red" }
                default { "Red" }
            }
        )
        Write-Host "Duration: $($testResult.Duration) seconds"

        if ($exitCode -ne 0) {
            Write-Host "`n--- Test Output ---" -ForegroundColor Yellow

            if ($compilationErrors.Count -gt 0) {
                Write-Host "Compilation Errors:" -ForegroundColor Yellow
                $compilationErrors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
            }

            if ($runtimeErrors.Count -gt 0) {
                Write-Host "Runtime Errors:" -ForegroundColor Yellow
                $runtimeErrors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
            }

            Write-Host "`n--- Full Output ---" -ForegroundColor Yellow
            Write-Host $outputString -ForegroundColor Gray
        } else {
            Write-Host "Test passed successfully!" -ForegroundColor Green
        }

        return $testResult

    } catch {
        Write-Error "Failed to run test: $_"
        return $null
    }
}

function Show-TestSummary {
    $tests = Discover-CSXTests

    Write-Host "=== CSX Test Summary ===" -ForegroundColor Yellow
    Write-Host "Total tests found: $($tests.Count)" -ForegroundColor Green

    $stats = @{
        PASSED            = 0
        FAILED            = 0
        COMPILATION_ERROR = 0
        RUNTIME_ERROR     = 0
        UNKNOWN           = 0
    }

    $tests | ForEach-Object {
        $status = "UNKNOWN"
        $logFile = Join-Path $TestLogsPath "$($_.Name).log"

        if (Test-Path $logFile) {
            try {
                $lastResult = Get-Content $logFile -Raw | ConvertFrom-Json
                $status = $lastResult.Status
                $stats[$status]++
            } catch {
                $status = "ERROR"
                $stats["UNKNOWN"]++
            }
        } else {
            $stats["UNKNOWN"]++
        }

        $color = switch ($status) {
            "PASSED" { "Green" }
            "COMPILATION_ERROR" { "Yellow" }
            "RUNTIME_ERROR" { "Red" }
            "FAILED" { "Red" }
            default { "Gray" }
        }

        Write-Host "  [$status] $($_.Name)" -ForegroundColor $color
        if ($_.Description) {
            Write-Host "    $($_.Description)" -ForegroundColor Gray
        }

        # Show recent errors if any
        if ($status -ne "PASSED" -and $status -ne "UNKNOWN" -and (Test-Path $logFile)) {
            try {
                $result = Get-Content $logFile -Raw | ConvertFrom-Json
                if ($result.CompilationErrors -and $result.CompilationErrors.Count -gt 0) {
                    Write-Host "    Compilation Issues: $($result.CompilationErrors.Count)" -ForegroundColor Yellow
                }
                if ($result.RuntimeErrors -and $result.RuntimeErrors.Count -gt 0) {
                    Write-Host "    Runtime Issues: $($result.RuntimeErrors.Count)" -ForegroundColor Red
                }
            } catch {
                # Intentionally empty - silently ignore JSON parse errors for corrupted log files
                [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingEmptyCatchBlock', '', Justification = 'Intentionally ignoring parse errors')]
                param()
            }
        }
    }

    Write-Host "`n=== Test Statistics ===" -ForegroundColor Yellow
    $stats.GetEnumerator() | Sort-Object Key | ForEach-Object {
        $color = switch ($_.Key) {
            "PASSED" { "Green" }
            "COMPILATION_ERROR" { "Yellow" }
            "RUNTIME_ERROR" { "Red" }
            "FAILED" { "Red" }
            default { "Gray" }
        }
        Write-Host "  $($_.Key): $($_.Value)" -ForegroundColor $color
    }
}

# Main execution logic
switch ($Action.ToLower()) {
    "discover" {
        $tests = Discover-CSXTests
        $tests | Format-Table Name, Description, File -AutoSize
    }
    "run" {
        if (-not $TestFile) {
            Write-Error "TestFile parameter required for run action"
            exit 1
        }
        $result = Run-CSXTest -TestFileName $TestFile
        exit $(if ($result.ExitCode -eq 0) { 0 } else { 1 })
    }
    "summary" {
        Show-TestSummary
    }
    default {
        Write-Host "Usage: csx-test-adapter.ps1 -Action [discover|run|summary] [-TestFile <filename>]"
        Write-Host "Actions:"
        Write-Host "  discover  - Find all CSX test files"
        Write-Host "  run       - Run a specific CSX test (requires -TestFile)"
        Write-Host "  summary   - Show test results summary"
    }
}
