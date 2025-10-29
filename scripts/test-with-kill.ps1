# UI Test Runner with Process Cleanup
# Runs UI tests with automatic cleanup of lingering processes

param(
    [Parameter(Mandatory = $true)]
    [string]$Project,
    [string]$Verbosity = "normal",
    [int]$TimeoutSeconds = 30,
    [string]$Filter
)

Write-Output "🚀 UI Test Runner with Process Cleanup"
Write-Output "Project: $Project"
Write-Output "Verbosity: $Verbosity"
Write-Output "Timeout: $TimeoutSeconds seconds"
Write-Output ""

# Ensure we're in the right directory
Push-Location $PSScriptRoot

try {
    # Clean up any lingering test processes before building
    Write-Output "🧹 Cleaning up lingering test processes..."
    $processNames = @('WileyWidget', 'testhost', 'vstest.console', 'dotnet', 'FlaUI', 'UIAutomation')

    foreach ($name in $processNames) {
        try {
            $processes = Get-Process -Name $name -ErrorAction SilentlyContinue
            if ($processes) {
                Write-Output "Stopping $name processes..."
                $processes | ForEach-Object {
                    Write-Output "  Stopping $($_.ProcessName) (pid=$($_.Id))"
                    $_ | Stop-Process -Force -ErrorAction SilentlyContinue
                }
            }
        }
        catch {
            Write-Output "  Warning: Could not stop $name processes: $($_.Exception.Message)"
        }
    }

    # Wait a moment for processes to fully terminate
    Start-Sleep -Seconds 2

    # Build the project first
    Write-Output "📦 Building project..."
    $buildArgs = @(
        "build",
        "$PSScriptRoot\..\WileyWidget.csproj",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary",
        # Force legacy (non-wpftmp) path for XAML to avoid design-time NuGet resolution issues
        "/p:GenerateTemporaryTargetAssembly=false",
        "/p:IncludePackageReferencesDuringMarkupCompilation=false",
        "/p:AlwaysCompileMarkupFilesInSeparateDomain=false"
    )

    & dotnet $buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    # Run UI tests
    Write-Output "🧪 Running UI tests..."
    $testArgs = @(
        "test",
        $Project,
        "--logger",
        "console;verbosity=$Verbosity"
    )

    if ($Filter) {
        $testArgs += "--filter", $Filter
    }

    # Add timeout (convert seconds to milliseconds for blame-hang-timeout)
    $timeoutMs = $TimeoutSeconds * 1000
    $testArgs += "--blame-hang-timeout", "$timeoutMs"

    Write-Output "Command: dotnet $($testArgs -join ' ')"
    Write-Output ""

    $startTime = Get-Date

    # Capture the output
    $testOutput = & dotnet $testArgs 2>&1
    $exitCode = $LASTEXITCODE

    $endTime = Get-Date
    $duration = $endTime - $startTime

    Write-Output ""
    Write-Output "⏱️  UI Testing completed in $($duration.TotalSeconds.ToString("F1")) seconds"

    # Write full output to log file
    $logFile = "$PSScriptRoot\..\test-output.log"
    Write-Output "📝 Writing full test output to $logFile..."
    $testOutput | Out-File -FilePath $logFile -Encoding utf8

    # Also display the output to console
    $testOutput | Write-Output

    # Process test output for error file
    $errorFile = "$PSScriptRoot\..\test-error.txt"
    $errors = $testOutput | Where-Object { $_ -match "error|Error|ERROR|Failed|FAILED" -or $_.ToString().Contains("Exception") }

    if ($errors) {
        Write-Output "📝 Writing errors to $errorFile..."

        # Extract errors with file paths and sort by file
        $fileErrors = @()
        foreach ($line in $errors) {
            if ($line -match "(.+?\.cs|\.vb|\.fs|\.xaml|\.cshtml|\.vbhtml|\.razor)\((\d+),(\d+)\):\s*(.+)$") {
                $file = $matches[1]
                $lineNum = $matches[2]
                $colNum = $matches[3]
                $message = $matches[4]
                $fileErrors += [PSCustomObject]@{
                    File     = $file
                    Line     = [int]$lineNum
                    Column   = [int]$colNum
                    Message  = $message
                    FullLine = $line
                }
            }
            elseif ($line -match "(.+?\.cs|\.vb|\.fs|\.xaml|\.cshtml|\.vbhtml|\.razor)[^(]*:\s*(.+)$") {
                $file = $matches[1]
                $message = $matches[2]
                $fileErrors += [PSCustomObject]@{
                    File     = $file
                    Line     = 0
                    Column   = 0
                    Message  = $message
                    FullLine = $line
                }
            }
            else {
                $fileErrors += [PSCustomObject]@{
                    File     = "Unknown"
                    Line     = 0
                    Column   = 0
                    Message  = $line
                    FullLine = $line
                }
            }
        }

        # Sort by file name, then by line number
        $sortedErrors = $fileErrors | Sort-Object File, Line

        # Write to file
        $sortedErrors | ForEach-Object {
            "$($_.File)($($_.Line),$($_.Column)): $($_.Message)" | Out-File -FilePath $errorFile -Append
        }

        Write-Output "📄 Errors sorted and saved to test-error.txt"
    }
    else {
        # Clear the error file if no errors
        "" | Out-File -FilePath $errorFile
    }

    if ($exitCode -eq 0) {
        Write-Output "✅ All UI tests passed!"
    }
    else {
        Write-Output "❌ Some UI tests failed (Exit code: $exitCode)"
    }

    exit $exitCode

}
catch {
    Write-Error "UI test execution failed: $($_.Exception.Message)"
    exit 1
}
finally {
    # Final cleanup
    Write-Output "🧹 Final cleanup..."
    try {
        foreach ($name in $processNames) {
            Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object {
                Write-Output "  Force stopping $($_.ProcessName) (pid=$($_.Id))"
                $_ | Stop-Process -Force -ErrorAction SilentlyContinue
            }
        }
    }
    catch { }

    Pop-Location
}
