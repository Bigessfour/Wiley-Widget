# CI/CD Tools Verification Script
param(
    [Parameter(Mandatory=$false)]
    [switch]$Detailed,
    [Parameter(Mandatory=$false)]
    [switch]$FixIssues,
    [Parameter(Mandatory=$false)]
    [string]$LogFile = "cicd-verification.log"
)

Write-Host "🔧 WileyWidget CI/CD Tools Verification" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

$startTime = Get-Date
$results = @{}
$issues = @()

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] [$Level] $Message"
    Write-Host $logEntry -ForegroundColor $(if ($Level -eq "ERROR") { "Red" } elseif ($Level -eq "WARN") { "Yellow" } else { "White" })
    Add-Content $LogFile $logEntry
}

function Test-Tool {
    param([string]$Name, [string]$Command, [string]$ExpectedOutput = "", [bool]$Required = $true)

    Write-Host "  🔍 Checking $Name..." -NoNewline
    try {
        # Use Start-Process to better handle command execution
        $process = Start-Process -FilePath "cmd.exe" -ArgumentList "/c $Command" -NoNewWindow -Wait -PassThru -RedirectStandardOutput "temp_output.txt" -RedirectStandardError "temp_error.txt"
        $output = Get-Content "temp_output.txt" -Raw
        $errorOutput = Get-Content "temp_error.txt" -Raw
        $exitCode = $process.ExitCode

        # Clean up temp files
        Remove-Item "temp_output.txt" -ErrorAction SilentlyContinue
        Remove-Item "temp_error.txt" -ErrorAction SilentlyContinue

        if ($exitCode -eq 0 -and ($ExpectedOutput -eq "" -or $output -like "*$ExpectedOutput*")) {
            Write-Host " ✅" -ForegroundColor Green
            $results[$Name] = @{ Status = "OK"; Output = $output; ExitCode = $exitCode }
            if ($Detailed) {
                $displayOutput = ($output -replace "`n", " | ").Trim()
                if ($displayOutput.Length -gt 100) { $displayOutput = $displayOutput.Substring(0, 100) + "..." }
                Write-Host "     Output: $displayOutput" -ForegroundColor Gray
            }
        } else {
            Write-Host " ❌" -ForegroundColor Red
            $results[$Name] = @{ Status = "FAIL"; Output = "$output`n$errorOutput"; ExitCode = $exitCode }
            if ($Required) {
                $issues += "$Name failed (Exit: $exitCode)"
            }
            if ($Detailed) {
                $displayError = ("$output`n$errorOutput" -replace "`n", " | ").Trim()
                if ($displayError.Length -gt 100) { $displayError = $displayError.Substring(0, 100) + "..." }
                Write-Host "     Error: $displayError" -ForegroundColor Red
            }
        }
    } catch {
        Write-Host " ❌" -ForegroundColor Red
        $results[$Name] = @{ Status = "ERROR"; Output = $_.Exception.Message; ExitCode = -1 }
        if ($Required) {
            $issues += "$Name error: $($_.Exception.Message)"
        }
        if ($Detailed) {
            Write-Host "     Exception: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

# Clear previous log
if (Test-Path $LogFile) { Clear-Content $LogFile }

Write-Log "Starting CI/CD Tools Verification"

# Check Core Development Tools
Write-Host "`n📦 Core Development Tools:" -ForegroundColor Yellow
Test-Tool "Git" "git --version" "git version"
Test-Tool "Node.js" "node --version" "v"
Test-Tool "NPM" "npm --version" -Required $false
Test-Tool "PowerShell" "pwsh --version" "PowerShell"
Test-Tool ".NET SDK" "dotnet --version" -Required $false

# Check Trunk and Linters
Write-Host "`n🔍 Trunk & Linters:" -ForegroundColor Yellow
Test-Tool "Trunk CLI" "trunk --version" "1.25.0"
Test-Tool "Trunk Check" "trunk check --help" "trunk check" -Required $false

# Check Azure Tools
Write-Host "`n☁️  Azure Tools:" -ForegroundColor Yellow
Test-Tool "Azure CLI" "az --version" "azure-cli" -Required $false
Test-Tool "Azure Account" "az account show" -Required $false

# Check Build Tools
Write-Host "`n🔨 Build Tools:" -ForegroundColor Yellow
Test-Tool "MSBuild" "msbuild /version" -Required $false
Test-Tool "NuGet" "nuget help" "NuGet" -Required $false

# Check Testing Tools
Write-Host "`n🧪 Testing Tools:" -ForegroundColor Yellow
Test-Tool "VSTest" "vstest.console.exe /?" -Required $false

# Check GitHub Tools
Write-Host "`n🐙 GitHub Tools:" -ForegroundColor Yellow
Test-Tool "GitHub CLI" "gh --version" "gh version" -Required $false

# Check CI/CD Configuration
Write-Host "`n⚙️  CI/CD Configuration:" -ForegroundColor Yellow

# Check trunk.yaml
$trunkConfig = ".\.trunk\trunk.yaml"
if (Test-Path $trunkConfig) {
    Write-Host "  🔍 Trunk Configuration..." -NoNewline
    try {
        $configContent = Get-Content $trunkConfig -Raw
        if ($configContent -match "version:" -and $configContent -match "lint:") {
            Write-Host " ✅" -ForegroundColor Green
            $linterCount = ($configContent | Select-String "enabled:" -Context 0,10 | ForEach-Object { $_.Context.PostContext -split "`n" | Where-Object { $_.Trim().StartsWith("- ") } }).Count
            $results["Trunk Config"] = @{ Status = "OK"; Output = "Valid config with ~$linterCount linters"; ExitCode = 0 }
            if ($Detailed) {
                Write-Host "     Contains linter configuration" -ForegroundColor Gray
            }
        } else {
            throw "Invalid trunk configuration format"
        }
    } catch {
        Write-Host " ❌" -ForegroundColor Red
        $results["Trunk Config"] = @{ Status = "ERROR"; Output = $_.Exception.Message; ExitCode = -1 }
        $issues += "Trunk config error: $($_.Exception.Message)"
    }
} else {
    Write-Host "  🔍 Trunk Configuration... ❌" -ForegroundColor Red
    $results["Trunk Config"] = @{ Status = "MISSING"; Output = "File not found"; ExitCode = -1 }
    $issues += "Trunk configuration file missing"
}

# Check GitHub Actions
$githubWorkflows = ".\.github\workflows"
if (Test-Path $githubWorkflows) {
    $workflows = Get-ChildItem "$githubWorkflows\*.yml" -ErrorAction SilentlyContinue
    Write-Host "  🔍 GitHub Actions Workflows..." -NoNewline
    if ($workflows.Count -gt 0) {
        Write-Host " ✅" -ForegroundColor Green
        $results["GitHub Actions"] = @{ Status = "OK"; Output = "$($workflows.Count) workflow(s) found"; ExitCode = 0 }
        if ($Detailed) {
            foreach ($wf in $workflows) {
                Write-Host "     $($wf.Name)" -ForegroundColor Gray
            }
        }
    } else {
        Write-Host " ⚠️" -ForegroundColor Yellow
        $results["GitHub Actions"] = @{ Status = "WARN"; Output = "Directory exists but no workflows found"; ExitCode = 0 }
    }
} else {
    Write-Host "  🔍 GitHub Actions Workflows... ❌" -ForegroundColor Red
    $results["GitHub Actions"] = @{ Status = "MISSING"; Output = "Directory not found"; ExitCode = -1 }
    $issues += "GitHub Actions workflows directory missing"
}

# Check Scripts Directory
$scriptsDir = ".\scripts"
if (Test-Path $scriptsDir) {
    $scripts = Get-ChildItem "$scriptsDir\*.ps1" -ErrorAction SilentlyContinue
    Write-Host "  🔍 Build Scripts..." -NoNewline
    if ($scripts.Count -gt 0) {
        Write-Host " ✅" -ForegroundColor Green
        $results["Build Scripts"] = @{ Status = "OK"; Output = "$($scripts.Count) PowerShell script(s) found"; ExitCode = 0 }
        if ($Detailed) {
            foreach ($script in $scripts) {
                Write-Host "     $($script.Name)" -ForegroundColor Gray
            }
        }
    } else {
        Write-Host " ⚠️" -ForegroundColor Yellow
        $results["Build Scripts"] = @{ Status = "WARN"; Output = "Directory exists but no scripts found"; ExitCode = 0 }
    }
} else {
    Write-Host "  🔍 Build Scripts... ❌" -ForegroundColor Red
    $results["Build Scripts"] = @{ Status = "MISSING"; Output = "Directory not found"; ExitCode = -1 }
    $issues += "Scripts directory missing"
}

# Summary
Write-Host "`n📊 Summary:" -ForegroundColor Cyan
$okCount = ($results.Values | Where-Object { $_.Status -eq "OK" }).Count
$failCount = ($results.Values | Where-Object { $_.Status -eq "FAIL" -or $_.Status -eq "ERROR" }).Count
$warnCount = ($results.Values | Where-Object { $_.Status -eq "WARN" }).Count
$missingCount = ($results.Values | Where-Object { $_.Status -eq "MISSING" }).Count

Write-Host "   ✅ OK: $okCount" -ForegroundColor Green
Write-Host "   ❌ Failed: $failCount" -ForegroundColor Red
Write-Host "   ⚠️  Warnings: $warnCount" -ForegroundColor Yellow
Write-Host "   🔍 Missing: $missingCount" -ForegroundColor Gray

if ($issues.Count -gt 0) {
    Write-Host "`n🚨 Issues Found:" -ForegroundColor Red
    foreach ($issue in $issues) {
        Write-Host "   • $issue" -ForegroundColor Red
        Write-Log $issue "ERROR"
    }

    if ($FixIssues) {
        Write-Host "`n🔧 Attempting Auto-Fixes:" -ForegroundColor Yellow

        # Try to fix common issues
        if ($issues -contains "Trunk config error:*") {
            Write-Host "   • Regenerating trunk configuration..." -NoNewline
            try {
                trunk init --force
                Write-Host " ✅" -ForegroundColor Green
            } catch {
                Write-Host " ❌" -ForegroundColor Red
            }
        }
    }
} else {
    Write-Host "`n🎉 All checks passed!" -ForegroundColor Green
}

$endTime = Get-Date
$duration = $endTime - $startTime

Write-Host "`n⏱️  Verification completed in $([math]::Round($duration.TotalSeconds, 1)) seconds" -ForegroundColor Cyan
Write-Host "📝 Detailed log saved to: $LogFile" -ForegroundColor Cyan

Write-Log "Verification completed. Duration: $([math]::Round($duration.TotalSeconds, 1))s"

# Export results for potential use by other scripts
$results | ConvertTo-Json -Depth 3 | Out-File "cicd-results.json" -Encoding UTF8
Write-Host "💾 Results exported to: cicd-results.json" -ForegroundColor Cyan
