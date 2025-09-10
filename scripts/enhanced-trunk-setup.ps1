# Enhanced Trunk CI/CD Setup Script
# This script configures advanced Trunk CI/CD methods for the Wiley Widget project

[System.Diagnostics.CodeAnalysis.SuppressMessage("PSReviewUnusedParameter", "")]
param(
    [switch]$EnableAllActions,
    [switch]$SetupEnhancedHooks,
    [switch]$TestConfiguration,
    [switch]$NewQualityBaseline,
    [switch]$CreateDashboard,
    [switch]$Help
)

$ErrorActionPreference = "Stop"

# Display help information
function Show-Help {
    Write-Output "Enhanced Trunk CI/CD Setup Script"
    Write-Output "================================="
    Write-Output ""
    Write-Output "Usage: .\enhanced-trunk-setup.ps1 [options]"
    Write-Output ""
    Write-Output "Options:"
    Write-Output "  -EnableAllActions     Enable all recommended Trunk actions"
    Write-Output "  -SetupEnhancedHooks   Set up enhanced Git hooks with Trunk integration"
    Write-Output "  -TestConfiguration    Test the current Trunk configuration"
    Write-Output "  -NewQualityBaseline   Generate initial quality metrics baseline"
    Write-Output "  -CreateDashboard      Create quality dashboard infrastructure"
    Write-Output "  -Help                 Show this help message"
    Write-Output ""
    Write-Output "Examples:"
    Write-Output "  .\enhanced-trunk-setup.ps1 -EnableAllActions -SetupEnhancedHooks"
    Write-Output "  .\enhanced-trunk-setup.ps1 -TestConfiguration"
    Write-Output "  .\enhanced-trunk-setup.ps1 -NewQualityBaseline"
}

# Enable all recommended Trunk actions
function Enable-AllAction {
    Write-Output "🔧 Enabling recommended Trunk actions..."

    $actions = @(
        "trunk-check-pre-push",
        "trunk-fmt-pre-commit",
        "trunk-check-pre-commit",
        "trufflehog-pre-commit",
        "trunk-announce",
        "trunk-upgrade-available"
    )

    foreach ($action in $actions) {
        Write-Output "  Enabling $action..."
        try {
            trunk actions enable $action
            Write-Output "  ✅ $action enabled"
        }
        catch {
            Write-Output "  ⚠️ Failed to enable $action : $($_.Exception.Message)"
        }
    }
}

# Set up enhanced Git hooks
function Setup-EnhancedHook {
    Write-Output "🔗 Setting up enhanced Git hooks..."

    # Sync existing hooks
    Write-Output "  Syncing existing hooks..."
    trunk git-hooks sync

    # Create enhanced pre-commit hook
    $preCommitHook = @"
#!/usr/bin/env pwsh
# Enhanced pre-commit hook with comprehensive Trunk integration

`$ErrorActionPreference = "Stop"

Write-Output "🔍 Running enhanced pre-commit checks..."

# Auto-format code
Write-Output "  Auto-formatting code..."
trunk fmt --ci

# Run quality checks
Write-Output "  Running quality validation..."
`$result = trunk check --filter "psscriptanalyzer,prettier" --ci --print-failures
`$exitCode = `$LASTEXITCODE

if (`$exitCode -eq 0) {
    Write-Output "  ✅ Pre-commit checks passed"
} elseif (`$result -match "error|Error") {
    Write-Output "  ❌ Pre-commit errors found"
    exit 1
} else {
    Write-Output "  ⚠️ Pre-commit warnings found - proceeding"
}

# Stage any formatting changes
git add .
Write-Output "🎉 Pre-commit hook completed"
"@

    $preCommitPath = ".git\hooks\pre-commit"
    if (Test-Path $preCommitPath) {
        Write-Output "  Backing up existing pre-commit hook..."
        Copy-Item $preCommitPath "$preCommitPath.backup" -Force
    }

    $preCommitHook | Out-File $preCommitPath -Encoding UTF8 -Force
    Write-Output "  ✅ Enhanced pre-commit hook installed"

    # Create enhanced pre-push hook
    $prePushHook = @"
#!/usr/bin/env pwsh
# Enhanced pre-push hook with security-first validation

`$ErrorActionPreference = "Stop"

Write-Output "🔒 Running enhanced pre-push validation..."

# Security validation
Write-Output "  Running security checks..."
`$securityResult = trunk check --scope security --ci --print-failures
`$securityExitCode = `$LASTEXITCODE

if (`$securityExitCode -eq 0) {
    Write-Output "  ✅ Security validation passed"
} else {
    Write-Output "  ❌ Security issues found - push blocked"
    Write-Output `$securityResult
    exit 1
}

# Quality validation
Write-Output "  Running quality checks..."
`$qualityResult = trunk check --filter "psscriptanalyzer" --ci --print-failures
`$qualityExitCode = `$LASTEXITCODE

if (`$qualityExitCode -eq 0) {
    Write-Output "  ✅ Quality validation passed"
} else {
    Write-Output "  ❌ Quality issues found - push blocked"
    Write-Output `$qualityResult
    exit 1
}

Write-Output "🚀 Pre-push validation completed successfully"
"@

    $prePushPath = ".git\hooks\pre-push"
    if (Test-Path $prePushPath) {
        Write-Output "  Backing up existing pre-push hook..."
        Copy-Item $prePushPath "$prePushPath.backup" -Force
    }

    $prePushHook | Out-File $prePushPath -Encoding UTF8 -Force
    Write-Output "  ✅ Enhanced pre-push hook installed"

    # Make hooks executable on Windows
    try {
        icacls $preCommitPath /grant Everyone:F | Out-Null
        icacls $prePushPath /grant Everyone:F | Out-Null
        Write-Output "  ✅ Git hooks made executable"
    }
    catch {
        Write-Output "  ⚠️ Could not set executable permissions (normal on Windows)"
    }
}

# Test Trunk configuration
function Test-Configuration {
    Write-Output "🧪 Testing Trunk configuration..."

    # Test basic functionality
    Write-Output "  Testing basic check..."
    try {
        trunk check --ci --verbose | Out-Null
        Write-Output "  ✅ Basic check passed"
    }
    catch {
        Write-Output "  ❌ Basic check failed: $($_.Exception.Message)"
        return
    }

    # Test security scanning
    Write-Output "  Testing security scanning..."
    try {
        trunk check --scope security --ci | Out-Null
        Write-Output "  ✅ Security scanning works"
    }
    catch {
        Write-Output "  ⚠️ Security scanning has issues: $($_.Exception.Message)"
    }

    # Test specific linters
    $linters = @("psscriptanalyzer", "prettier", "trufflehog")
    foreach ($linter in $linters) {
        Write-Output "  Testing $linter..."
        try {
            trunk check --filter $linter --ci | Out-Null
            Write-Output "  ✅ $linter works"
        }
        catch {
            Write-Output "  ⚠️ $linter has issues: $($_.Exception.Message)"
        }
    }

    # Test actions
    Write-Output "  Testing actions..."
    try {
        $actionsOutput = trunk actions list
        $enabledCount = ($actionsOutput | Select-String "Enabled actions:" -Context 0, 10 | Out-String).Count
        Write-Output "  ✅ Actions system working ($enabledCount actions available)"
    }
    catch {
        Write-Output "  ⚠️ Actions system has issues: $($_.Exception.Message)"
    }

    Write-Output "🎉 Configuration test completed"
}

# Create quality baseline
function New-QualityBaseline {
    Write-Output "📊 Creating quality baseline..."

    $baselinePath = "quality-baseline.json"
    $timestamp = Get-Date -Format "o"

    # Run comprehensive quality check
    Write-Output "  Running comprehensive quality analysis..."
    try {
        $qualityOutput = trunk check --all --ci --print-failures
        $exitCode = $LASTEXITCODE

        # Parse results
        $issues = ($qualityOutput | Measure-Object).Count
        $errors = ($qualityOutput | Select-String "error|Error" | Measure-Object).Count
        $warnings = $issues - $errors

        # Generate baseline data
        $baseline = @{
            timestamp      = $timestamp
            exit_code      = $exitCode
            total_issues   = $issues
            errors         = $errors
            warnings       = $warnings
            quality_output = $qualityOutput
            configuration  = Get-Content ".trunk\trunk.yaml" -Raw
        }

        # Save baseline
        $baseline | ConvertTo-Json -Depth 10 | Out-File $baselinePath -Encoding UTF8
        Write-Output "  ✅ Quality baseline saved to $baselinePath"

        # Display summary
        Write-Output "  📈 Baseline Summary:"
        Write-Output "    • Total Issues: $issues"
        Write-Output "    • Errors: $errors"
        Write-Output "    • Warnings: $warnings"
        Write-Output "    • Exit Code: $exitCode"

    }
    catch {
        Write-Output "  ❌ Failed to generate baseline: $($_.Exception.Message)"
    }
}

# Create quality dashboard infrastructure
function New-Dashboard {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    Write-Output "📊 Creating quality dashboard infrastructure..."

    # Create metrics directory
    $metricsDir = "quality-metrics"
    if (-not (Test-Path $metricsDir)) {
        if ($PSCmdlet.ShouldProcess($metricsDir, "Create directory")) {
            New-Item -ItemType Directory -Path $metricsDir | Out-Null
        }
        Write-Output "  ✅ Created metrics directory"
    }

    # Create metrics collection script
    $metricsScript = @"
# Quality Metrics Collection Script
# Run this script to collect comprehensive quality metrics

param(
    [string]`$OutputPath = "quality-metrics/metrics-$(Get-Date -Format 'yyyy-MM-dd-HH-mm-ss').json"
)

Write-Output "📊 Collecting quality metrics..."

# Collect various metrics
`$metrics = @{
    timestamp = Get-Date -Format "o"
    commit_hash = git rev-parse HEAD
    branch = git branch --show-current

    # Trunk quality metrics
    trunk_exit_code = 0
    trunk_issues = 0
    trunk_errors = 0
    trunk_warnings = 0

    # Test coverage (placeholder - integrate with actual coverage tool)
    coverage_percentage = 0

    # Build metrics
    build_success = `$true
    build_duration_seconds = 0

    # Security metrics
    security_issues = 0
    critical_security_findings = 0
}

# Run trunk check and parse results
try {
    `$trunkOutput = trunk check --ci --print-failures
    `$metrics.trunk_exit_code = `$LASTEXITCODE
    `$metrics.trunk_issues = (`$trunkOutput | Measure-Object).Count
    `$metrics.trunk_errors = (`$trunkOutput | Select-String "error|Error" | Measure-Object).Count
    `$metrics.trunk_warnings = `$metrics.trunk_issues - `$metrics.trunk_errors
} catch {
    Write-Output "⚠️ Could not collect Trunk metrics: `$(`$_.Exception.Message)"
}

# Save metrics
`$metrics | ConvertTo-Json -Depth 10 | Out-File `$OutputPath -Encoding UTF8
Write-Output "✅ Metrics saved to `$OutputPath"

# Display summary
Write-Output "📈 Current Metrics:"
Write-Output "  • Trunk Issues: `$(`$metrics.trunk_issues)"
Write-Output "  • Errors: `$(`$metrics.trunk_errors)"
Write-Output "  • Warnings: `$(`$metrics.trunk_warnings)"
"@

    if ($PSCmdlet.ShouldProcess("$metricsDir\collect-metrics.ps1", "Create file")) {
        $metricsScript | Out-File "$metricsDir\collect-metrics.ps1" -Encoding UTF8
    }
    Write-Output "  ✅ Created metrics collection script"

    # Create dashboard generation script
    $dashboardScript = @"
# Quality Dashboard Generation Script
# Generates HTML dashboard from collected metrics

param(
    [string]`$MetricsDir = "quality-metrics",
    [string]`$OutputPath = "quality-dashboard.html",
    [int]`$DaysHistory = 30
)

Write-Output "📊 Generating quality dashboard..."

# Collect all metrics files
`$metricsFiles = Get-ChildItem `$MetricsDir -Filter "metrics-*.json" | Sort-Object LastWriteTime -Descending | Select-Object -First `$DaysHistory

if (`$metricsFiles.Count -eq 0) {
    Write-Output "⚠️ No metrics files found. Run collect-metrics.ps1 first."
    exit 1
}

# Parse metrics data
`$metricsData = `$metricsFiles | ForEach-Object {
    Get-Content `$_.FullName | ConvertFrom-Json
}

# Generate HTML dashboard
`$dashboard = @'
<!DOCTYPE html>
<html>
<head>
    <title>Wiley Widget - Quality Dashboard</title>
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .metric { background: #f5f5f5; padding: 10px; margin: 10px 0; border-radius: 5px; }
        .error { color: red; }
        .warning { color: orange; }
        .success { color: green; }
    </style>
</head>
<body>
    <h1>🔍 Wiley Widget - Code Quality Dashboard</h1>
    <p>Generated on: `$(Get-Date -Format "yyyy-MM-dd HH:mm:ss")</p>

    <div class="metric">
        <h2>📈 Quality Trends</h2>
        <canvas id="qualityChart" width="400" height="200"></canvas>
    </div>

    <div class="metric">
        <h2>🔒 Security Trends</h2>
        <canvas id="securityChart" width="400" height="200"></canvas>
    </div>

    <div class="metric">
        <h2>📊 Current Status</h2>
        <p>Latest metrics from: `$(`$metricsData[0].timestamp)</p>
        <p>Trunk Issues: <span class="`$(if (`$metricsData[0].trunk_errors -gt 0) { 'error' } elseif (`$metricsData[0].trunk_warnings -gt 0) { 'warning' } else { 'success' })">`$(`$metricsData[0].trunk_issues)</span></p>
        <p>Errors: <span class="error">`$(`$metricsData[0].trunk_errors)</span></p>
        <p>Warnings: <span class="warning">`$(`$metricsData[0].trunk_warnings)</span></p>
    </div>

    <script>
        // Chart.js implementation would go here
        // This is a placeholder for the actual chart rendering logic
        console.log('Dashboard loaded with', `$metricsData.Count, 'data points');
    </script>
</body>
</html>
'@

    `$dashboard | Out-File `$OutputPath -Encoding UTF8
    Write-Output "✅ Dashboard generated: `$OutputPath"
}
"@

    if ($PSCmdlet.ShouldProcess("$metricsDir\generate-dashboard.ps1", "Create file")) {
        $dashboardScript | Out-File "$metricsDir\generate-dashboard.ps1" -Encoding UTF8
    }
    Write-Output "  ✅ Created dashboard generation script at $metricsDir\generate-dashboard.ps1"
    Write-Output "  Running initial metrics collection..."
    try {
        if ($PSCmdlet.ShouldProcess("$metricsDir\collect-metrics.ps1", "Execute script")) {
            & "$metricsDir\collect-metrics.ps1"
        }
        Write-Output "  ✅ Initial metrics collected"
    }
    catch {
        Write-Output "  ⚠️ Could not collect initial metrics: $($_.Exception.Message)"
    }

    Write-Output "🎉 Quality dashboard infrastructure created"
    Write-Output "  📁 Metrics directory: $metricsDir"
    Write-Output "  📊 Run collect-metrics.ps1 to collect data"
    Write-Output "  📈 Run generate-dashboard.ps1 to create HTML dashboard"
}

# Main execution logic
if ($Help) {
    Show-Help
    exit 0
}

Write-Output "🚀 Enhanced Trunk CI/CD Setup for Wiley Widget"
Write-Output "=============================================="

$actions = @()

if ($EnableAllActions) { $actions += "EnableAllActions" }
if ($SetupEnhancedHooks) { $actions += "SetupEnhancedHooks" }
if ($TestConfiguration) { $actions += "TestConfiguration" }
if ($NewQualityBaseline) { $actions += "NewQualityBaseline" }
if ($CreateDashboard) { $actions += "CreateDashboard" }

if ($actions.Count -eq 0) {
    Write-Output "No actions specified. Use -Help for usage information."
    exit 1
}

foreach ($action in $actions) {
    try {
        switch ($action) {
            "EnableAllActions" { Enable-AllActions }
            "SetupEnhancedHooks" { Setup-EnhancedHooks }
            "TestConfiguration" { Test-Configuration }
            "NewQualityBaseline" { New-QualityBaseline }
            "CreateDashboard" { New-Dashboard }
        }
    }
    catch {
        Write-Output "❌ Failed to execute $action : $($_.Exception.Message)"
    }
}

Write-Output ""
Write-Output "🎉 Enhanced Trunk CI/CD setup completed!"
Write-Output "📚 See docs\trunk-cicd-integration-guide.md for detailed documentation"
