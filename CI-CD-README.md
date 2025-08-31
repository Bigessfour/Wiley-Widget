# Wiley Widget - CI/CD Pipeline (90% Success Rate Initiative)

## 🚀 Overview

This repository implements an enterprise-grade CI/CD pipeline designed to achieve and maintain a **90% success rate** through advanced reliability features, parallel execution, and comprehensive monitoring with **Trunk CLI integration**.

## 📊 Pipeline Features

### Core Reliability Features
- **Health Validation**: Pre-build environment #### **Terminal Commands for Hyperthreading**

**Quick Enable Hyperthreading:**
```batch
# Windows Batch (run in terminal)
enable-hyperthreading.bat
```

**PowerShell Environment Setup (Recommended):**
```powershell
# Simple environment setup (just sets variables)
.\scripts\trunk-env-setup.ps1

# Full setup with monitoring and execution
.\scripts\trunk-hyperthreading-setup.ps1 -EnableHyperthreading

# CI-optimized setup
.\scripts\trunk-hyperthreading-setup.ps1 -EnableHyperthreading -OptimizeForCI

# Custom thread count
.\scripts\trunk-hyperthreading-setup.ps1 -EnableHyperthreading -ThreadCount 8

# Quiet mode (no output, just setup)
.\scripts\trunk-env-setup.ps1 -Quiet
```

**Direct Trunk Commands with Hyperthreading:**
```powershell
# After running environment setup, use trunk normally with .env variables
trunk check --all --ci
trunk check --scope security
trunk fmt --ci

# Manual thread control
trunk check --jobs 12 --all --ci
```echanisms**: Exponential backoff for transient failures
- **Circuit Breaker Pattern**: Prevents cascade failures
- **Parallel Execution**: Matrix-based test execution
- **Success Rate Monitoring**: Real-time performance tracking
- **Emergency Mode**: Graceful degradation during issues
- **Trunk Integration**: Security scanning, code quality, and analytics

### Workflows

#### 1. `ci-optimized.yml` (Primary - ACTIVE)
- **Purpose**: Main CI/CD pipeline with 90% success rate features and Trunk integration
- **Triggers**: Push/PR to main/temp branches, merge_group, scheduled, manual dispatch
- **Features**:
  - Health validation before builds
  - Parallel test execution matrix
  - Retry logic with circuit breaker
  - **Trunk security scanning** (Gitleaks, Trufflehog, OSV-Scanner)
  - **Trunk code quality** (PSScriptAnalyzer, Prettier, SVGO)
  - **Test results upload** to Trunk analytics
  - Comprehensive success rate monitoring
  - Emergency mode support
  - **Merge queue compatible**

#### 2. `merge-queue-cicd.yml` (Merge Queue - ACTIVE)
- **Purpose**: Dedicated merge queue pipeline with Trunk quality gates
- **Triggers**: Push/PR to main/temp branches, merge_group, manual dispatch
- **Features**:
  - **Merge queue integration** for automated merging
  - **Trunk security scans** on every queue entry
  - **Parallel quality assurance** (Security, CodeQuality, Performance)
  - **Test coverage validation** (80% minimum)
  - **Automated release notes** generation
  - **GitHub release creation** with assets
  - **Test results upload** to Trunk for flaky test analysis

#### 3. `release-new.yml` (Release Pipeline - ACTIVE)
- **Purpose**: Automated release creation with quality gates
- **Triggers**: Push to main branch + Manual dispatch with tag
- **Features**:
  - **Trunk security scanning** for releases
  - Full test suite execution (excluding UI tests)
  - NuGet package creation
  - **Automated release notes** generation
  - **GitHub release creation** with assets
  - **Test results upload** to Trunk analytics

#### 4. `comprehensive-cicd.yml` (Enhanced)
- **Purpose**: Full-featured pipeline with advanced caching
- **Triggers**: Push/PR to main branch
- **Features**:
  - Intelligent dependency caching
  - Multi-stage deployment readiness
  - Extended health validation
  - Performance analytics

#### 5. `deploy.yml` (Production)
- **Purpose**: Production deployment with safety checks
- **Triggers**: Manual dispatch
- **Features**:
  - Pre-deployment health validation
  - Integration with optimized CI pipeline
  - Post-deployment health checks
  - Environment-specific configurations

#### 6. `maintenance.yml` (Scheduled)
- **Purpose**: Automated pipeline maintenance
- **Triggers**: Daily at 2 AM UTC + Manual
- **Features**:
  - Health checks and cleanup
  - Dependency optimization
  - Success rate analysis
  - Maintenance reporting

#### 5. `release-new.yml` (Release Pipeline)
- **Purpose**: Automated release creation with quality gates
- **Triggers**: Push to main branch + Manual dispatch with tag
- **Features**:
  - Trunk security scanning for releases
  - Full test suite execution (excluding UI tests)
  - NuGet package creation
  - Automated release notes generation
  - GitHub release creation with assets
  - Test results upload to Trunk for analysis

#### 6. `ci.yml` (Legacy - Deprecated)
- **Status**: Deprecated - Use `ci-optimized.yml`
- **Purpose**: Basic CI pipeline (kept for reference)

## 🛠️ CI/CD Commands Reference

### Primary CI Pipeline Commands (`ci-optimized.yml`)

#### Health Validation Commands
```powershell
# Run comprehensive health check
.\scripts\health-check.ps1

# Check Trunk daemon status
trunk daemon status

# Validate environment
dotnet --version
```

#### Trunk Security & Quality Commands
```bash
# Run Trunk security scan
trunk check --ci --upload --series=main-security --scope=security

# Run Trunk code quality scan
trunk check --ci --upload --series=main-quality --filter=psscriptanalyzer,prettier

# Run comprehensive Trunk check
trunk check --ci --all
```

#### Build Commands
```bash
# Restore NuGet packages with caching
dotnet restore

# Build with performance monitoring
dotnet build --configuration Release --verbosity minimal

# Build with binary logging
dotnet build -c Release --no-restore /bl:msbuild.binlog
```

#### Test Commands
```bash
# Run unit tests with coverage and multiple loggers
dotnet test --configuration Release --no-build \
  --collect:"XPlat Code Coverage" \
  --logger "trx;LogFileName=test-results.trx" \
  --logger "junit;LogFileName=test-results.xml" \
  --filter "Category!=UiSmokeTests"

# Upload test results to Trunk
trunk analytics upload \
  --junit-paths "TestResults/*.xml" \
  --org-slug ${TRUNK_ORG_URL_SLUG} \
  --token ${TRUNK_API_TOKEN}
```

### Merge Queue Commands (`merge-queue-cicd.yml`)

#### Quality Gate Commands
```bash
# Pre-merge quality checks
trunk check --ci --upload --series=merge-queue-${GITHUB_RUN_NUMBER} --scope=security

# Code quality validation
trunk check --ci --filter=psscriptanalyzer,prettier,svgo

# Coverage validation
dotnet test --collect:"XPlat Code Coverage" --filter "Category!=UiSmokeTests"
```

#### Release Commands
```bash
# Generate release notes
git log --oneline --pretty=format:"- %s" HEAD~1..HEAD

# Create GitHub release
gh release create ${TAG_NAME} \
  --title "Release ${TAG_NAME}" \
  --notes-file RELEASE_NOTES.md \
  --draft false
```

### Release Pipeline Commands (`release-new.yml`)

#### Build Commands
```bash
# Restore NuGet packages
dotnet restore

# Build in Release configuration with binary logging
dotnet build -c Release --no-restore /bl:msbuild.binlog

# Pack NuGet package
dotnet pack -c Release --no-build -o ./artifacts
```

#### Test Commands
```bash
# Run tests with code coverage and JUnit output (excluding UI tests)
dotnet test -c Release --no-build --verbosity normal \
  --collect:"XPlat Code Coverage" \
  --logger "junit;LogFileName=test-results.xml" \
  --filter "Category!=UiSmokeTests&Category!=HighInteraction&Category!=PostMigration"
```

#### Trunk Integration Commands
```bash
# Security scan for releases
trunk check --ci --upload --series=release-${GITHUB_REF_NAME} --scope=security

# Upload test results to Trunk
trunk analytics upload \
  --junit-paths "TestResults/*/test-results.xml" \
  --org-slug ${TRUNK_ORG_URL_SLUG} \
  --token ${TRUNK_API_TOKEN}
```

#### Release Commands
```bash
# Generate release notes
git log --oneline --pretty=format:"- %s" HEAD~1..HEAD

# Create GitHub release
gh release create ${TAG_NAME} \
  --title "Release ${TAG_NAME}" \
  --notes-file RELEASE_NOTES.md \
  --draft false \
  --prerelease false

# Upload release assets
gh release upload ${TAG_NAME} ./artifacts/*.nupkg
```

### Core CI Commands

#### Health Validation
```powershell
# Run health check script
.\scripts\health-check.ps1
```

#### Enhanced Testing
```powershell
# Run tests with retry logic
.\scripts\run-tests-enhanced.ps1 -ProjectPath "WileyWidget.csproj" -MaxRetries 3
```

#### Monitoring
```powershell
# Monitor CI/CD success rates
.\scripts\monitor-cicd.ps1 -AnalyzeHistory -Days 30
```

## 🛠️ Scripts

### Core CI/CD Scripts
- `scripts/health-check.ps1`: Environment validation and system health checks
- `scripts/run-tests-enhanced.ps1`: Enhanced test runner with retry logic and coverage
- `scripts/monitor-cicd.ps1`: Success rate monitoring and CI/CD analytics
- `scripts/run-tests.ps1`: Updated test runner with retry mechanisms

### Trunk Integration Scripts
- `scripts\trunk-maintenance.ps1`: Trunk configuration and maintenance
- `scripts\trunk-cicd-monitor.ps1`: Comprehensive Trunk CI/CD monitoring and reporting
- `scripts\merge-queue-manager.ps1`: Merge queue management and monitoring

### Usage Examples

#### Health Check
```powershell
.\scripts\health-check.ps1
```

#### Enhanced Test Runner
```powershell
.\scripts\run-tests-enhanced.ps1 -ProjectPath "WileyWidget.csproj" -MaxRetries 3
```

#### Monitor Success Rates
```powershell
.\scripts\monitor-cicd.ps1 -AnalyzeHistory -Days 30
```

#### Trunk CI/CD Monitoring
```powershell
# Check overall CI/CD health
.\scripts\trunk-cicd-monitor.ps1 -CheckHealth

# Generate performance report
.\scripts\trunk-cicd-monitor.ps1 -GenerateReport -Days 7

# Monitor in real-time
.\scripts\trunk-cicd-monitor.ps1 -PerformanceMetrics
```

#### Merge Queue Management
```powershell
# Check merge queue status
.\scripts\merge-queue-manager.ps1 -CheckStatus

# Add PR to merge queue
.\scripts\merge-queue-manager.ps1 -AddToQueue -PullRequestNumber 123

# Monitor queue in real-time
.\scripts\merge-queue-manager.ps1 -MonitorQueue

# Generate merge queue report
.\scripts\merge-queue-manager.ps1 -GenerateReport -Days 30
```

#### Trunk Maintenance
```powershell
# Diagnose and fix Trunk configuration
.\scripts\trunk-maintenance.ps1 -Diagnose -Fix

# Update Trunk tools and linters
.\scripts\trunk-maintenance.ps1 -UpdateTools
```

## 📈 Achieving 90% Success Rate

### Key Strategies

1. **Health Validation**
   - Pre-build environment checks
   - Dependency validation
   - Network connectivity tests

2. **Retry Mechanisms**
   - Exponential backoff (1s, 2s, 4s, 8s)
   - Maximum retry limits (3-5 attempts)
   - Circuit breaker for persistent failures

3. **Parallel Execution**
   - Matrix-based test execution
   - Concurrent job processing
   - Resource optimization

4. **Monitoring & Alerting**
   - Real-time success rate tracking
   - Failure pattern analysis
   - Automated alerts for issues

5. **Emergency Mode**
   - Graceful degradation
   - Reduced test suites during issues
   - Priority-based execution

### Success Metrics

- **Target**: 90%+ pipeline success rate
- **Monitoring**: Daily success rate reports
- **Alerting**: Notifications for <85% success rates
- **Recovery**: Automated retry and circuit breaker reset

## 🔧 Configuration

### Environment Variables
```yaml
# Core CI/CD Variables
DOTNET_VERSION: '9.0.x'
CI: true
GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

# Trunk CLI Configuration
TRUNK_API_TOKEN: ${{ secrets.TRUNK_API_TOKEN }}
TRUNK_ORG_URL_SLUG: ${{ secrets.TRUNK_ORG_URL_SLUG }}
TRUNK_VERSION: '1.25.0'

# Merge Queue Configuration
MERGE_QUEUE_ENABLED: true
MERGE_QUEUE_MAX_WAIT_TIME: '30m'
MERGE_QUEUE_REQUIRED_CHECKS: 'build,test,security-scan'

# Build Configuration
BUILD_CONFIGURATION: 'Release'
TEST_PARALLELISM: 4
COVERAGE_THRESHOLD: 85
```

### Required Secrets
- `GITHUB_TOKEN`: For GitHub API access and workflow triggers
- `TRUNK_API_TOKEN`: For Trunk CLI authentication and analytics upload
- `TRUNK_ORG_URL_SLUG`: Organization identifier for Trunk analytics
- `AZURE_CREDENTIALS`: For Azure deployments (if applicable)

### Trunk Configuration (`trunk.yaml`)
```yaml
version: 0.1
cli:
  version: 1.25.0
  # Performance optimization for hyperthreading support
  performance:
    enable_hyperthreading: true
    threads: auto
    parallel_processing: true
    memory_limit: "4GB"
    cpu_affinity: true
  # Environment configuration
  env:
    dotenv: true
    dotenv_path: "../.env"
    dotenv_override: false
```
    - id: trunk
      ref: v1.25.0
      uri: https://github.com/trunk-io/plugins
actions:
  definitions:
    - id: test
      runtime: node
      packages_file: package.json
      run: npm test
  disabled: []
```

### Hyperthreading Support & Performance Optimization

#### 🚀 **Automatic Hyperthreading Detection**
The CI/CD pipeline automatically detects and optimizes for hyperthreading-capable CPUs:

```yaml
# Performance configuration in trunk.yaml
cli:
  version: 1.25.0
  performance:
    enable_hyperthreading: true
    threads: auto
    parallel_processing: true
    memory_limit: "4GB"
    cpu_affinity: true
```

#### ⚡ **Terminal Commands for Hyperthreading**

**Quick Enable (with .env support):**
```batch
# Windows Batch (loads .env + enables hyperthreading)
enable-hyperthreading.bat
```

**Load .env file only:**
```powershell
# Load environment variables from .env file
.\scripts\load-env-for-trunk.ps1

# Load with override (replace existing variables)
.\scripts\load-env-for-trunk.ps1 -OverrideExisting

# Quiet mode (no output)
.\scripts\load-env-for-trunk.ps1 -Quiet
```

**PowerShell with Full Control:**
```powershell
# Enable hyperthreading with performance monitoring
.\scripts\trunk-hyperthreading-setup.ps1 -EnableHyperthreading -MonitorPerformance

# CI-optimized hyperthreading setup
.\scripts\trunk-hyperthreading-setup.ps1 -EnableHyperthreading -OptimizeForCI

# Custom thread count
.\scripts\trunk-hyperthreading-setup.ps1 -EnableHyperthreading -ThreadCount 8
```

#### 📊 **Performance Benefits**
- **Hyperthreading CPUs**: Utilizes all logical processors (typically 2x physical cores)
- **Non-Hyperthreading CPUs**: Optimizes for physical cores + overhead
- **Memory Management**: 4GB limit prevents memory exhaustion
- **CPU Affinity**: Consistent core allocation for stable performance
- **Parallel Processing**: Concurrent linting and scanning operations

#### 🔧 **Environment Variables Set**
```powershell
# Automatically configured by hyperthreading script
TRUNK_NUM_THREADS=auto          # Based on CPU detection
TRUNK_MEMORY_LIMIT=4GB          # Memory optimization
TRUNK_ENABLE_PARALLEL=true      # Parallel processing
TRUNK_HYPERTHREADING_ENABLED=true  # Hyperthreading support
TRUNK_CPU_AFFINITY=true         # CPU core affinity
TRUNK_MAX_CONCURRENT_JOBS=auto  # Concurrent job limit
```

#### 📈 **Performance Monitoring**
```powershell
# Monitor hyperthreading performance
.\scripts\trunk-hyperthreading-setup.ps1 -EnableHyperthreading -MonitorPerformance

# Expected output includes:
# - CPU detection (physical cores, logical processors, hyperthreading status)
# - Optimal thread count calculation
# - Performance metrics (duration, CPU usage, exit codes)
# - Memory and resource utilization
```

### GitHub Actions Configuration
```yaml
# .github/workflows/ci-optimized.yml
name: CI/CD Pipeline
on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]
  merge_group:
    types: [checks_requested]
  workflow_dispatch:

permissions:
  contents: read
  pull-requests: write
  checks: write
  statuses: write
```

### PowerShell Configuration
```powershell
# scripts/config.ps1
param(
    [string]$Environment = "Development",
    [switch]$EnableDebug,
    [switch]$SkipTests
)

# CI/CD Configuration
$CICD_CONFIG = @{
    TrunkEnabled = $true
    MergeQueueEnabled = $true
    MonitoringEnabled = $true
    HealthChecksEnabled = $true
}
```

## 📋 Maintenance

### Daily Tasks (Automated)
- Health validation
- Build artifact cleanup
- Dependency updates check
- Success rate analysis

### Weekly Tasks
- Review success rate reports
- Update dependencies
- Security vulnerability scans

### Monthly Tasks
- Full pipeline audit
- Performance optimization
- Documentation updates

## 🚨 Troubleshooting

### Common Issues

#### Pipeline Failures
1. Check health validation logs
2. Review retry attempt details
3. Verify circuit breaker status
4. Analyze failure patterns

#### Low Success Rates
1. Run `monitor-cicd.ps1` for analysis
2. Check for environmental issues
3. Review recent changes
4. Consider emergency mode

#### Build Issues
1. Verify .NET version consistency
2. Check dependency conflicts
3. Review build logs for errors
4. Use health check script

### Emergency Procedures

1. **Enable Emergency Mode**
   ```yaml
   emergency_mode: true
   reduced_test_suite: true
   ```

2. **Manual Health Check**
   ```powershell
   .\scripts\health-check.ps1 -Verbose
   ```

3. **Force Pipeline Reset**
   - Clear build cache
   - Reset circuit breaker
   - Restart with minimal configuration

## 📊 Monitoring Dashboard

### Success Rate Tracking
- Daily success rate calculations
- Weekly trend analysis
- Monthly performance reports
- Failure pattern identification

### Key Metrics
- Pipeline success rate
- Average build time
- Test execution time
- Retry frequency
- Circuit breaker activations

## 🤝 Contributing

### Pipeline Changes
1. Test changes in staging environment
2. Update documentation
3. Ensure backward compatibility
4. Monitor success rates post-deployment

### Best Practices
- Always use health validation
- Implement retry logic for new operations
- Monitor performance impact
- Document emergency procedures

## 📞 Support

### Getting Help
1. Check pipeline logs
2. Review monitoring reports
3. Run health checks
4. Consult troubleshooting guide

### Escalation
- Success rate <85%: Immediate investigation
- Success rate <80%: Emergency mode activation
- Success rate <75%: Full pipeline review

---

## 🎯 Success Rate Achievement

This CI/CD pipeline is designed to achieve and maintain **90%+ success rates** through:

- ✅ **Health validation** before all operations
- ✅ **Retry mechanisms** with exponential backoff
- ✅ **Circuit breaker patterns** for failure prevention
- ✅ **Parallel execution** for faster, more reliable builds
- ✅ **Comprehensive monitoring** and alerting
- ✅ **Automated maintenance** and optimization
- ✅ **Emergency mode** for graceful degradation

**Current Status**: Ready for deployment with all 90% success rate features implemented.

---

*Automated CI/CD Pipeline - Enterprise Reliability Initiative*
