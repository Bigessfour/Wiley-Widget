# Wiley Widget CI/CD Health Check Script
# Validates environment readiness for 90% success rate

param(
    [switch]$Quick,
    [switch]$Detailed,
    [switch]$Fix,
    [switch]$CI
)

$ErrorActionPreference = "Stop"

# Configuration
$projectRoot = Split-Path -Parent $PSScriptRoot
$healthReport = Join-Path $projectRoot "health-check-report.json"

function Write-HealthLog {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $color = switch ($Level) {
        "ERROR" { "Red" }
        "WARNING" { "Yellow" }
        "SUCCESS" { "Green" }
        default { "White" }
    }
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}

function Test-DotNetEnvironment {
    Write-HealthLog "🔍 Checking .NET environment..." "INFO"

    $results = @{
        DotNetInstalled = $false
        Version = $null
        SDKVersion = $null
        TargetFramework = $null
        Warnings = @()
        Errors = @()
    }

    try {
        $dotnetVersion = dotnet --version
        $results.DotNetInstalled = $true
        $results.Version = $dotnetVersion
        Write-HealthLog "✅ .NET SDK found: $dotnetVersion" "SUCCESS"

        # Check if version matches global.json
        if (Test-Path (Join-Path $projectRoot "global.json")) {
            $globalJson = Get-Content (Join-Path $projectRoot "global.json") | ConvertFrom-Json
            $expectedVersion = $globalJson.sdk.version
            if ($dotnetVersion -notlike "$expectedVersion*") {
                $results.Warnings += "SDK version mismatch. Expected: $expectedVersion, Found: $dotnetVersion"
                Write-HealthLog "⚠️ SDK version mismatch detected" "WARNING"
            }
        }

        # Check target framework
        $csprojPath = Join-Path $projectRoot "WileyWidget.csproj"
        if (Test-Path $csprojPath) {
            $csprojContent = Get-Content $csprojPath -Raw
            if ($csprojContent -match '<TargetFramework>([^<]+)</TargetFramework>') {
                $results.TargetFramework = $matches[1]
                Write-HealthLog "📋 Target Framework: $($results.TargetFramework)" "INFO"
            }
        }

    } catch {
        $results.Errors += "Failed to detect .NET SDK: $($_.Exception.Message)"
        Write-HealthLog "❌ .NET SDK not found or not working" "ERROR"
    }

    return $results
}

function Test-BuildEnvironment {
    Write-HealthLog "🔨 Checking build environment..." "INFO"

    $results = @{
        CanRestore = $false
        CanBuild = $false
        BuildTime = $null
        Warnings = @()
        Errors = @()
    }

    try {
        Push-Location $projectRoot

        # Test restore
        Write-HealthLog "Testing package restore..." "INFO"
        $restoreTime = Measure-Command { dotnet restore --verbosity minimal }
        $results.CanRestore = $true
        Write-HealthLog "✅ Package restore successful ($($restoreTime.TotalSeconds)s)" "SUCCESS"

        # Test build
        Write-HealthLog "Testing build..." "INFO"
        $buildTime = Measure-Command { dotnet build --configuration Release --verbosity minimal }
        if ($LASTEXITCODE -eq 0) {
            $results.CanBuild = $true
            $results.BuildTime = $buildTime.TotalSeconds
            Write-HealthLog "✅ Build successful ($($buildTime.TotalSeconds)s)" "SUCCESS"
        } else {
            $results.Errors += "Build failed with exit code $LASTEXITCODE"
            Write-HealthLog "❌ Build failed" "ERROR"
        }

    } catch {
        $results.Errors += "Build environment test failed: $($_.Exception.Message)"
        Write-HealthLog "❌ Build environment check failed" "ERROR"
    } finally {
        Pop-Location
    }

    return $results
}

function Test-TestEnvironment {
    Write-HealthLog "🧪 Checking test environment..." "INFO"

    $results = @{
        CanRunTests = $false
        TestTime = $null
        TestCount = 0
        Warnings = @()
        Errors = @()
    }

    try {
        Push-Location $projectRoot

        # Quick test run
        Write-HealthLog "Running quick test validation..." "INFO"
        $testTime = Measure-Command {
            dotnet test "WileyWidget.Tests\WileyWidget.Tests.csproj" `
                --configuration Release `
                --filter "EntityValidation" `
                --verbosity minimal `
                --logger "console;verbosity=minimal"
        }

        if ($LASTEXITCODE -eq 0) {
            $results.CanRunTests = $true
            $results.TestTime = $testTime.TotalSeconds
            Write-HealthLog "✅ Test execution successful ($($testTime.TotalSeconds)s)" "SUCCESS"
        } else {
            $results.Errors += "Test execution failed with exit code $LASTEXITCODE"
            Write-HealthLog "❌ Test execution failed" "ERROR"
        }

    } catch {
        $results.Errors += "Test environment check failed: $($_.Exception.Message)"
        Write-HealthLog "❌ Test environment check failed" "ERROR"
    } finally {
        Pop-Location
    }

    return $results
}

function Test-SystemResources {
    Write-HealthLog "💻 Checking system resources..." "INFO"

    $results = @{
        DiskSpaceGB = 0
        MemoryGB = 0
        Warnings = @()
        Errors = @()
    }

    try {
        # Check disk space
        $diskSpace = Get-WmiObject -Class Win32_LogicalDisk | Where-Object { $_.DeviceID -eq 'C:' }
        $results.DiskSpaceGB = [math]::Round($diskSpace.FreeSpace / 1GB, 2)

        if ($results.DiskSpaceGB -lt 10) {
            $results.Warnings += "Low disk space: $($results.DiskSpaceGB)GB available"
            Write-HealthLog "⚠️ Low disk space: $($results.DiskSpaceGB)GB" "WARNING"
        } else {
            Write-HealthLog "✅ Sufficient disk space: $($results.DiskSpaceGB)GB" "SUCCESS"
        }

        # Check memory
        $memory = Get-WmiObject -Class Win32_ComputerSystem
        $results.MemoryGB = [math]::Round($memory.TotalPhysicalMemory / 1GB, 2)
        Write-HealthLog "💾 System memory: $($results.MemoryGB)GB" "INFO"

    } catch {
        $results.Errors += "System resource check failed: $($_.Exception.Message)"
        Write-HealthLog "❌ System resource check failed" "ERROR"
    }

    return $results
}

function Test-ExternalDependencies {
    Write-HealthLog "🔗 Checking external dependencies..." "INFO"

    $results = @{
        GitAvailable = $false
        GitVersion = $null
        Warnings = @()
        Errors = @()
    }

    try {
        # Check Git
        $gitVersion = git --version
        if ($gitVersion) {
            $results.GitAvailable = $true
            $results.GitVersion = $gitVersion
            Write-HealthLog "✅ Git available: $gitVersion" "SUCCESS"
        }

        # Check if in git repository
        $gitStatus = git status 2>$null
        if ($LASTEXITCODE -ne 0) {
            $results.Warnings += "Not in a git repository"
            Write-HealthLog "⚠️ Not in a git repository" "WARNING"
        }

    } catch {
        $results.Errors += "External dependency check failed: $($_.Exception.Message)"
        Write-HealthLog "❌ External dependency check failed" "ERROR"
    }

    return $results
}

function Invoke-HealthFixes {
    Write-HealthLog "🔧 Applying health fixes..." "INFO"

    try {
        Push-Location $projectRoot

        # Clear NuGet cache if issues detected
        Write-HealthLog "Clearing NuGet cache..." "INFO"
        dotnet nuget locals all --clear

        # Clean and restore
        Write-HealthLog "Cleaning and restoring..." "INFO"
        dotnet clean
        dotnet restore

        # Rebuild
        Write-HealthLog "Rebuilding project..." "INFO"
        dotnet build --configuration Release --verbosity minimal

        Write-HealthLog "✅ Health fixes applied" "SUCCESS"

    } catch {
        Write-HealthLog "❌ Health fixes failed: $($_.Exception.Message)" "ERROR"
    } finally {
        Pop-Location
    }
}

function Export-HealthReport {
    param($healthData)

    $report = @{
        timestamp = Get-Date -Format "o"
        environment = @{
            os = $env:OS
            username = $env:USERNAME
            computername = $env:COMPUTERNAME
        }
        checks = $healthData
        overall_status = "unknown"
        recommendations = @()
    }

    # Calculate overall status
    $totalErrors = ($healthData.Values | Where-Object { $_.Errors } | Measure-Object -Property Errors -Sum).Sum
    $totalWarnings = ($healthData.Values | Where-Object { $_.Warnings } | Measure-Object -Property Warnings -Sum).Sum

    if ($totalErrors -eq 0) {
        $report.overall_status = "healthy"
        if ($totalWarnings -gt 0) {
            $report.recommendations += "Address $($totalWarnings) warnings for optimal performance"
        }
    } elseif ($totalErrors -le 2) {
        $report.overall_status = "warning"
        $report.recommendations += "Fix $($totalErrors) errors to improve reliability"
    } else {
        $report.overall_status = "unhealthy"
        $report.recommendations += "Critical issues detected. Address $($totalErrors) errors before proceeding"
    }

    # Export report
    $report | ConvertTo-Json -Depth 10 | Out-File $healthReport -Encoding UTF8
    Write-HealthLog "📊 Health report exported to: $healthReport" "INFO"
}

# Main execution
Write-HealthLog "🏥 Wiley Widget CI/CD Health Check" "INFO"
Write-HealthLog "==================================" "INFO"

$healthData = @{}

# Run health checks
$healthData.DotNet = Test-DotNetEnvironment
$healthData.System = Test-SystemResources
$healthData.External = Test-ExternalDependencies

if (-not $Quick) {
    $healthData.Build = Test-BuildEnvironment
    $healthData.Test = Test-TestEnvironment
}

# Apply fixes if requested
if ($Fix) {
    Invoke-HealthFixes
}

# Calculate and display summary
$totalErrors = 0
$totalWarnings = 0

foreach ($check in $healthData.GetEnumerator()) {
    $errors = $check.Value.Errors.Count
    $warnings = $check.Value.Warnings.Count
    $totalErrors += $errors
    $totalWarnings += $warnings

    $status = if ($errors -eq 0) { "✅" } else { "❌" }
    Write-HealthLog "$status $($check.Key): $errors errors, $warnings warnings" $(if ($errors -eq 0) { "SUCCESS" } else { "ERROR" })
}

# Overall assessment
Write-HealthLog "`n📈 Overall Health Assessment:" "INFO"
if ($totalErrors -eq 0) {
    Write-HealthLog "✅ Environment is healthy and ready for CI/CD" "SUCCESS"
    if ($totalWarnings -gt 0) {
        Write-HealthLog "⚠️ $($totalWarnings) warnings detected - consider addressing for optimal performance" "WARNING"
    }
} else {
    Write-HealthLog "❌ $($totalErrors) critical issues detected" "ERROR"
    Write-HealthLog "🔧 Run with -Fix parameter to attempt automatic resolution" "INFO"
}

# Export report
Export-HealthReport -healthData $healthData

# CI mode exit codes
if ($CI) {
    if ($totalErrors -gt 0) {
        exit 1
    } else {
        exit 0
    }
}
