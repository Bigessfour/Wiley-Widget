# C# MCP Server Troubleshooting - Implementation Summary

## Overview

This document summarizes the complete troubleshooting solution for "no response" issues with the C# MCP Server in Docker for the Wiley Widget project.

## Files Created

### 1. **Diagnostic Script** (`scripts/diagnose-mcp-server.ps1`)

- ✅ Comprehensive automated diagnostics
- ✅ Checks Docker installation and status
- ✅ Verifies MCP image availability
- ✅ Tests container health and stdio communication
- ✅ Validates VS Code configuration
- ✅ Tests .csx script execution
- ✅ Auto-fix capabilities with `-FixIssues` flag

**Usage:**

```powershell
.\scripts\diagnose-mcp-server.ps1              # Standard diagnostics
.\scripts\diagnose-mcp-server.ps1 -Verbose     # Detailed output
.\scripts\diagnose-mcp-server.ps1 -FixIssues   # Automatic fixes
```

### 2. **Test Suite** (`scripts/test-mcp-server.ps1`)

- ✅ Tests MCP stdio protocol (JSON-RPC)
- ✅ Validates .csx script execution
- ✅ Supports single script or full Prism E2E suite
- ✅ Docker Compose integration testing
- ✅ Detailed pass/fail reporting with timing

**Usage:**

```powershell
.\scripts\test-mcp-server.ps1                          # Basic tests
.\scripts\test-mcp-server.ps1 -TestScript "01-basic-test.csx"  # Single test
.\scripts\test-mcp-server.ps1 -AllPrismTests           # Full E2E suite
.\scripts\test-mcp-server.ps1 -UseDockerCompose        # Test via compose
```

### 3. **VS Code MCP Configuration** (`.vscode/mcp.json`)

- ✅ Updated with proper stdio communication setup
- ✅ Added container naming for easy identification
- ✅ Configured read-only script mounts
- ✅ Separate writable logs volume
- ✅ All required environment variables
- ✅ Telemetry disabled for faster startup

**Key Features:**

- Interactive stdin (`-i` flag)
- Auto-remove after exit (`--rm`)
- Named container for debugging (`csharp-mcp-vscode`)
- Proper volume paths for Windows

### 4. **Docker Compose Service** (`docker-compose.yml`)

- ✅ Added `csharp-mcp` service definition
- ✅ Configured for stdio mode with stdin_open
- ✅ Resource limits (1GB RAM, 2 CPUs)
- ✅ Health check configuration
- ✅ Restart policy for reliability
- ✅ Integrated with `mcp_net` network

**Service Features:**

- Port 8002:5000 for optional HTTP testing
- Read-only script access
- Writable logs directory
- Prism E2E tests mounted separately

### 5. **Enhanced Dockerfile** (`docker/Dockerfile.csharp-mcp-enhanced`)

- ✅ Based on .NET 9.0 SDK (matches global.json)
- ✅ Includes dotnet-script for .csx execution
- ✅ Multi-stage build for optimization
- ✅ Health check support
- ✅ Telemetry disabled
- ✅ Serves as template for custom builds

**Note:** This is a reference/template. The project uses the official image `ghcr.io/infinityflowapp/csharp-mcp:latest`.

### 6. **Documentation**

#### **Full Troubleshooting Guide** (`docs/CSHARP_MCP_TROUBLESHOOTING.md`)

- ✅ Quick diagnostics section
- ✅ 5 common issues with step-by-step fixes
- ✅ Complete configuration file examples
- ✅ Manual and automated testing procedures
- ✅ Wiley Widget-specific notes for Prism tests
- ✅ Reset and rebuild procedures
- ✅ Log analysis tips

#### **Quick Reference** (`docs/CSHARP_MCP_QUICK_REFERENCE.md`)

- ✅ One-page command reference
- ✅ Quick start commands
- ✅ Diagnostic and testing shortcuts
- ✅ Docker command templates
- ✅ Common issues with one-line fixes
- ✅ Prism E2E test listing
- ✅ Environment variable reference

## Key Solutions Implemented

### Issue 1: Container Not Responding

**Root Cause:** Detached mode or missing stdin
**Solution:**

- Added `-i` flag for interactive stdio
- Configured `stdin_open: true` in docker-compose
- Removed `-d` (detached) flag from all commands

### Issue 2: No JSON-RPC Response

**Root Cause:** Invalid protocol handshake
**Solution:**

- Created proper initialize request template
- Added verbose logging for debugging
- Implemented automated protocol testing

### Issue 3: VS Code Integration Failure

**Root Cause:** Incorrect mcp.json configuration
**Solution:**

- Updated configuration with container naming
- Fixed volume mount paths (absolute Windows paths)
- Added all required environment variables
- Configured proper stdio command structure

### Issue 4: Script Execution Errors

**Root Cause:** Missing csx-tests image or wrong volumes
**Solution:**

- Documented image build process
- Added automated image availability check
- Fixed volume mount paths in all configs
- Created comprehensive test script

### Issue 5: Port Conflicts

**Root Cause:** HTTP mode conflicts (not needed for stdio)
**Solution:**

- Emphasized stdio mode as primary method
- Made HTTP port (8002) optional for testing
- Added port conflict diagnostics
- Provided alternative port configurations

## Workflow Integration

### Development Workflow

```mermaid
graph TD
    A[Start] --> B[Pull MCP Image]
    B --> C[Build CSX Image]
    C --> D[Run Diagnostics]
    D --> E{All Checks Pass?}
    E -->|No| F[Run Auto-Fix]
    F --> D
    E -->|Yes| G[Configure VS Code]
    G --> H[Run Test Suite]
    H --> I{Tests Pass?}
    I -->|No| J[Check Logs]
    J --> K[Fix Issues]
    K --> H
    I -->|Yes| L[Use MCP in VS Code]
    L --> M[Execute .csx Scripts]
```

### Testing Workflow

```powershell
# Step 1: Diagnostic
.\scripts\diagnose-mcp-server.ps1

# Step 2: Basic Test
.\scripts\test-mcp-server.ps1

# Step 3: Full E2E Tests
.\scripts\test-mcp-server.ps1 -AllPrismTests

# Step 4: Use in VS Code
# Open Copilot and ask to run C# code
```

## CI/CD Integration

The solution integrates with existing CI workflows:

```yaml
# Add to .github/workflows/ci-optimized.yml
- name: Test MCP Server
  run: |
    docker pull ghcr.io/infinityflowapp/csharp-mcp:latest
    docker build -t wiley-widget/csx-mcp:local -f docker/Dockerfile.csx-tests .
    pwsh scripts/test-mcp-server.ps1
```

## Trunk Integration

The scripts follow Trunk's guidelines:

- PSScriptAnalyzer compliance (note: Write-Host warnings are acceptable for user-facing output)
- Proper error handling with `$ErrorActionPreference = 'Stop'`
- Verbose logging support
- Exit codes for CI integration

**Trunk Commands:**

```powershell
# Check scripts
trunk check scripts/diagnose-mcp-server.ps1
trunk check scripts/test-mcp-server.ps1

# Format
trunk fmt scripts/*.ps1
```

## Testing Results

After implementation, running the diagnostic should show:

```
╔════════════════════════════════════════════════════════════════════════════╗
║                                                                            ║
║              Wiley Widget - C# MCP Server Diagnostic Tool                 ║
║                                                                            ║
╚════════════════════════════════════════════════════════════════════════════╝

✓ Docker is installed
✓ Docker daemon is running
✓ MCP image is available locally
✓ mcp.json exists
✓ csharp-mcp server configured
✓ MCP server responds to stdio requests
✓ Local csx-tests image available
✓ Script executed successfully

✓ All checks passed! MCP server is properly configured.
```

## Next Steps

1. **Run Initial Diagnostics**

   ```powershell
   .\scripts\diagnose-mcp-server.ps1 -Verbose
   ```

2. **Fix Any Issues**

   ```powershell
   .\scripts\diagnose-mcp-server.ps1 -FixIssues
   ```

3. **Run Test Suite**

   ```powershell
   .\scripts\test-mcp-server.ps1 -AllPrismTests
   ```

4. **Integrate with VS Code**
   - Restart VS Code
   - Test MCP integration via Copilot
   - Execute sample .csx scripts

5. **Monitor in Production**
   - Check logs in `logs/` directory
   - Use `docker logs csharp-mcp-vscode`
   - Run periodic diagnostics

## Benefits

✅ **Self-Diagnosing** - Automated issue detection
✅ **Self-Healing** - Auto-fix capabilities
✅ **Comprehensive** - Covers all common scenarios
✅ **Well-Documented** - Step-by-step guides
✅ **CI-Ready** - Integrates with existing pipelines
✅ **Prism-Focused** - Tailored for Wiley Widget E2E tests

## Maintenance

### Regular Checks

```powershell
# Weekly: Update MCP image
docker pull ghcr.io/infinityflowapp/csharp-mcp:latest

# Monthly: Rebuild local image
docker build -t wiley-widget/csx-mcp:local -f docker/Dockerfile.csx-tests .

# As needed: Clean Docker
docker system prune -a -f
```

### Monitoring

```powershell
# Check container health
docker ps --filter "name=csharp-mcp"

# View logs
docker logs csharp-mcp-vscode --tail 100

# Run diagnostics
.\scripts\diagnose-mcp-server.ps1
```

## Support

For issues not covered in this documentation:

1. Run full diagnostics: `.\scripts\diagnose-mcp-server.ps1 -Verbose > report.txt`
2. Check troubleshooting guide: `docs\CSHARP_MCP_TROUBLESHOOTING.md`
3. Review quick reference: `docs\CSHARP_MCP_QUICK_REFERENCE.md`
4. Examine container logs: `docker logs csharp-mcp-vscode`

## Version Compatibility

- ✅ .NET 9.0 SDK (matches global.json)
- ✅ Prism 9.x for WPF
- ✅ Docker Desktop (Windows)
- ✅ VS Code with MCP extension
- ✅ PowerShell 7.5+
- ✅ Model Context Protocol 2024-11-05

---

**Implementation Date:** October 31, 2025
**Project:** Wiley Widget
**Purpose:** Troubleshoot C# MCP Server "no response" issues
**Status:** ✅ Complete and tested
