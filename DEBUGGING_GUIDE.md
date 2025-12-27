# WileyWidget Debugging Guide

## Overview

This guide covers the comprehensive debugging ecosystem for the WileyWidget WinForms application, including automated diagnostics, performance monitoring, and memory analysis tools.

## Quick Start Debugging

### 1. Basic WinForms Debugging

- **Launch Config**: `WinForms: Launch & Debug`
- **Features**: Full symbol loading, exception tracking, performance logging
- **Hot Reload**: `WinForms: Debug with Hot Reload` for faster development cycles

### 2. Test Debugging

- **Unit Tests**: `Debug C# Tests` with full .NET debugging
- **E2E Tests**: `Debug E2E: Run & Attach` for integration testing
- **Hanging Tests**: Use `--blame-hang` options in test tasks

### 3. Performance Debugging

- **Launch Config**: `WinForms: Debug with Performance Monitoring`
- **Tasks**: `Debug: Performance Trace (30s)`, `Debug: Runtime Counters Monitor`

## Advanced Diagnostic Tools

### Automated Monitoring

#### Process Monitor (`Debug: Monitor Processes (Background)`)

```powershell
# Monitors for:
# - Non-responsive processes (auto-captures dumps)
# - High CPU usage (>95% threshold)
# - Memory usage patterns
# - Automatic dump collection on issues
```

#### Watch Mode Diagnostics (`Debug: Watch with Auto-Capture`)

```powershell
# Runs dotnet watch with automatic diagnostics:
# - Captures dumps on hangs/crashes
# - Performance traces on high CPU
# - Runtime counters monitoring
# - Job output logging
```

### Memory Analysis

#### Memory Dump Capture (`Debug: Capture Memory Dump`)

- Captures full process memory dumps
- Automatic timestamping and organization
- Stored in `tmp/dumps/` directory

#### GC Heap Analysis (`Debug: GC Heap Analysis`)

- Captures .NET garbage collection heap dumps
- Useful for memory leak investigation
- Requires `dotnet-gcdump` tool

#### Dump Analysis (`Debug: Analyze Dump File`)

- Analyzes captured memory dumps
- Provides stack traces, heap statistics
- Identifies root causes of crashes/hangs

### Performance Monitoring

#### Runtime Counters (`Debug: Runtime Counters Monitor`)

```powershell
# Monitors .NET runtime metrics:
# - GC collections and pauses
# - JIT compilation stats
# - Thread pool usage
# - Exception rates
```

#### Performance Tracing (`Debug: Performance Trace (30s)`)

- Captures detailed performance traces
- CPU profiling, memory allocations
- Method-level timing analysis
- Compatible with Visual Studio/PerfView

#### Thread Analysis (`Debug: Thread Analysis`)

- Shows managed thread stacks
- Identifies deadlocks and blocking
- Thread pool starvation detection

### Exception & Log Analysis

#### Exception Analysis (`Debug: Exception Analysis`)

- Scans recent log files for exceptions
- Aggregates error patterns
- Provides context around failures

#### Comprehensive Diagnostics (`Debug: Run Diagnostics`)

- System information collection
- Process health monitoring
- Log file analysis
- Performance metrics gathering
- Automatic issue detection

## Directory Structure

```
tmp/
├── dumps/           # Memory dumps (.dmp files)
├── traces/          # Performance traces (.nettrace files)
├── logs/            # Diagnostic logs and counters
└── diagnostics/     # Comprehensive diagnostic reports
```

## Environment Variables

Add these to your `.env` file for enhanced debugging:

```bash
# Enhanced debugging
DOTNET_EnableDiagnostics=1
DOTNET_EnableDiagnostics_Profiler=1
DOTNET_DbgEnableMiniDump=1
DOTNET_DbgMiniDumpType=4

# Performance monitoring
DOTNET_EnableEventLog=1
DOTNET_EventLogSource=Microsoft-Windows-DotNETRuntime

# Exception handling
WW_DEBUG_EXCEPTIONS=1
COREHOST_TRACE=1
```

## Common Debugging Scenarios

### Application Hanging

1. Run `Debug: Monitor Processes (Background)`
2. Wait for automatic dump capture
3. Analyze with `Debug: Analyze Dump File`

### High CPU Usage

1. Start `Debug: Performance Trace (30s)`
2. Run `Debug: Runtime Counters Monitor`
3. Analyze traces in Visual Studio/PerfView

### Memory Leaks

1. Use `Debug: GC Heap Analysis`
2. Monitor with runtime counters
3. Analyze heap dumps for retained objects

### Test Timeouts

1. Use E2E debug configurations
2. Enable `--blame-hang` in test tasks
3. Attach debugger to hanging test process

### UI Thread Blocking

1. Run `Debug: Thread Analysis`
2. Look for blocked UI threads
3. Use WinDbg for detailed thread analysis

## Best Practices

### Development Workflow

1. Use `WinForms: Debug with Hot Reload` for rapid iteration
2. Enable background process monitoring during development
3. Run diagnostics before commits: `Debug: Run Diagnostics`

### Performance Investigation

1. Start with runtime counters for quick insights
2. Use performance traces for detailed analysis
3. Capture memory dumps for memory issues

### Production Debugging

1. Use `tmp/capture-dump.ps1` for automated monitoring
2. Enable diagnostic environment variables
3. Collect traces during performance issues

## Troubleshooting

### Tools Not Found

```bash
# Install .NET diagnostic tools
dotnet tool install -g dotnet-dump
dotnet tool install -g dotnet-trace
dotnet tool install -g dotnet-counters
dotnet tool install -g dotnet-stack
dotnet tool install -g dotnet-gcdump
```

### Permission Issues

- Run VS Code as Administrator for system-level diagnostics
- Ensure write permissions to `tmp/` directories
- Check antivirus exclusions for diagnostic tools

### High Resource Usage

- Adjust monitoring intervals in scripts
- Use targeted monitoring instead of comprehensive diagnostics
- Clean up old dump/trace files regularly

## Integration with CI/CD

### Automated Diagnostics

```yaml
# Add to GitHub Actions or CI pipeline
- name: Run Diagnostics
  run: pwsh scripts/diagnostics.ps1

- name: Capture Performance Baseline
  run: pwsh -Command "dotnet-trace collect -p (Get-Process dotnet | Select -First 1).Id -o perf-trace.nettrace --duration 00:01:00"
```

### Failure Analysis

```yaml
- name: Analyze Test Failures
  if: failure()
  run: |
    pwsh tmp/capture-dump.ps1 -ProcessName "testhost*"
    pwsh scripts/diagnostics.ps1
```

This debugging ecosystem provides comprehensive tools for diagnosing issues at all levels of the WileyWidget application, from development to production environments.
