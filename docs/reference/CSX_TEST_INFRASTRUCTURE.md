# Wiley Widget CSX Test Infrastructure

## Overview

The Wiley Widget project utilizes a sophisticated **C# Script (CSX) testing framework** running in a **Dockerized MCP (Model Context Protocol) environment** with aggressive **NuGet package caching** for rapid, reproducible integration and end-to-end testing.

## Technology Stack

### 1. C# Script (CSX) Files
- **Format**: `.csx` files - C# scripts that execute without compilation
- **Purpose**: Integration, E2E, and static analysis tests
- **Location**: `scripts/examples/csharp/`
- **Count**: 27+ test files covering critical application scenarios
- **Execution**: Via `dotnet-script` in Docker container

**Advantages**:
- ✅ No build/compilation step required
- ✅ Rapid iteration during development
- ✅ Isolated test scenarios with full .NET capabilities
- ✅ Direct access to production code for analysis
- ✅ Can load NuGet packages dynamically via `#r "nuget:` directives

### 2. Docker-based MCP Server

#### Container Image
- **Name**: `wiley-widget/csx-mcp:local`
- **Base**: .NET 9.0 SDK (`mcr.microsoft.com/dotnet/sdk:9.0`)
- **Dockerfile**: `docker/Dockerfile.csx-tests`
- **Tools**: dotnet-script, C# scripting runtime

#### Build Process
```bash
docker build -t wiley-widget/csx-mcp:local -f docker/Dockerfile.csx-tests .
```

#### Container Architecture
```
┌─────────────────────────────────────────┐
│  Docker Container                       │
│  ┌───────────────────────────────────┐  │
│  │ .NET 9.0 SDK                      │  │
│  │ • dotnet-script runtime           │  │
│  │ • C# compiler                     │  │
│  │ • NuGet package cache (pre-loaded)│  │
│  └───────────────────────────────────┘  │
│                                         │
│  Volume Mounts:                         │
│  • /app → Workspace (read-only)         │
│  • /logs → Test output (read-write)     │
│                                         │
│  Environment Variables:                 │
│  • WW_REPO_ROOT=/app                    │
│  • WW_LOGS_DIR=/logs                    │
└─────────────────────────────────────────┘
```

### 3. NuGet Package Caching Strategy

#### Pre-cached Packages (Docker Layer Caching)
The Docker image pre-downloads and caches critical NuGet packages to eliminate network latency:

**Core Frameworks**:
- `Prism.DryIoc` (9.0.x) - DI container and MVVM framework
- `Microsoft.EntityFrameworkCore` (9.0.x) - Database ORM
- `Microsoft.EntityFrameworkCore.InMemory` - Test database provider
- `Microsoft.EntityFrameworkCore.Sqlite` - SQLite provider
- `Microsoft.Extensions.DependencyInjection` - Core DI abstractions
- `Microsoft.Extensions.Hosting` - Application lifetime management
- `Microsoft.Extensions.Configuration` - Configuration system

**Logging & Telemetry**:
- `Serilog` (4.1.x) - Structured logging
- `Serilog.Sinks.Console` - Console output
- `Serilog.Sinks.File` - File-based logs
- `System.Diagnostics.DiagnosticSource` - Tracing/telemetry

**Testing Utilities**:
- `xunit.core` - Test framework concepts (assertions)
- `Moq` - Mocking library
- `FluentAssertions` - Readable assertions

**Performance Benefits**:
- ⚡ Test execution time: **<1 second** (vs 30+ seconds without caching)
- 🔒 Reproducible: Same package versions guaranteed
- 🌐 Offline-capable: No network required after initial build

#### Cache Location
```
/root/.nuget/packages/  # Inside Docker container
```

### 4. VS Code Task Integration

#### Task Configuration (`tasks.json`)
```json
{
  "label": "csx:run-50-startup-orchestrator-test",
  "type": "shell",
  "command": "docker",
  "args": [
    "run", "--rm", "-w", "/app",
    "-v", "${workspaceFolder}:/app:ro",
    "-v", "${workspaceFolder}/logs:/logs:rw",
    "-e", "WW_REPO_ROOT=/app",
    "-e", "WW_LOGS_DIR=/logs",
    "wiley-widget/csx-mcp:local",
    "scripts/examples/csharp/50-startup-orchestrator-test.csx"
  ],
  "dependsOn": ["csx:build-image", "ensure-logs-dir"]
}
```

#### Task Workflow
1. **Build Docker image** (if not exists or outdated)
2. **Ensure logs directory** exists
3. **Run container** with:
   - Read-only workspace mount
   - Read-write logs mount
   - Environment variables for path configuration
4. **Execute CSX script** via dotnet-script
5. **Capture output** to terminal and logs

### 5. Test Categories & Coverage

#### Database Tests (45-52 series)
- **45-dbcontextfactory-integration-test.csx**
  - DbContextFactory pattern validation
  - Connection string configuration
  - Factory registration in DI

- **46-dbcontext-configuration-test.csx**
  - DbContext options configuration
  - Provider selection (InMemory, SQLite, SQL Server)

- **47-inmemory-migration-test.csx**
  - EF Core 9.0 migration validation
  - InMemory provider testing

- **48-ef9-warnings-validation-test.csx**
  - EF Core 9.0 breaking changes detection
  - AOT compatibility checks

- **49-database-initializer-test.csx**
  - DatabaseInitializer hosted service
  - Auto-migration on startup
  - Syncfusion license validation

- **50-startup-orchestrator-test.csx**
  - 4-phase startup sequence
  - Telemetry integration
  - Error handling & rollback
  - **48 tests, 100% pass rate**

- **51-database-initializer-dependencies-test.csx**
  - Dependency injection validation
  - Retry policies (Polly integration)
  - Configuration integration

- **52-database-initializer-fluent-test.csx**
  - Fluent API configuration
  - Extension method patterns

#### Prism Framework Tests (20-25 series)
- **20-prism-container-e2e-test.csx** - DryIoc container registration
- **21-prism-modules-e2e-test.csx** - Module initialization lifecycle
- **22-prism-di-registration-e2e-test.csx** - Service registration patterns
- **23-prism-module-lifecycle-e2e-test.csx** - Module startup/shutdown
- **24-prism-container-resolution-e2e-test.csx** - Service resolution
- **25-prism-region-adapters-e2e-test.csx** - WPF region adapters

#### Navigation Tests (30-35 series)
- **31-navigation-async-test.csx** - Async navigation patterns
- **32-dashboard-navigation-analyzer.csx** - Navigation flow analysis
- **33-module-init-discovery.csx** - Module initialization discovery
- **34-error-constants-discovery.csx** - Error constant validation

#### XAML Analysis Tests (44 series)
- **44-xaml-binding-static-analyzer.csx**
  - Static XAML binding validation
  - DataContext analysis
  - Binding path verification
  - Nested property path resolution

#### Syncfusion Component Tests (26-28 series)
- **26-sfdatagrid-budget-binding-e2e.csx** - DataGrid binding
- **27-sfchart-trend-analysis.csx** - Chart components
- **28-sfdocking-regions.csx** - Docking manager integration

### 6. Execution Flow

```
Developer → VS Code Task → Docker Run → CSX Script Execution
                  ↓              ↓              ↓
            Build Image    Mount Volumes    Load NuGet (cached)
                                ↓              ↓
                          Set Env Vars    Execute Test Logic
                                               ↓
                                    Output → Console + Logs
                                               ↓
                                    Exit Code → Task Status
```

### 7. Test Output & Logging

#### Console Output Format
```
=== TEST 1: Constructor and Initialization ===
✓ StartupOrchestrator constructor succeeds
✓ CompletedPhases starts empty
✓ RollbackCalled starts false

=== TEST 2: Successful 4-Phase Startup ===
[01:20:20 INF] Starting ConfigurationLoad (1/4)
[01:20:20 DBG] Configuration loaded with 22 sections
[01:20:20 INF] ✅ ConfigurationLoad completed in 30ms
...

============================================================
TEST SUMMARY
============================================================
Total Tests: 48
Passed: 48 (100.0%)
Failed: 0
============================================================
```

#### Log Files
- **Location**: `logs/` directory
- **Format**: Serilog structured logs (JSON optional)
- **Retention**: Configurable via environment variables

### 8. Development Workflow

#### Run Single Test
```bash
# Via VS Code Task
Ctrl+Shift+P → "Tasks: Run Task" → "csx:run-50-startup-orchestrator-test"

# Via Command Line
docker run --rm -w /app \
  -v "$(pwd):/app:ro" \
  -v "$(pwd)/logs:/logs:rw" \
  -e WW_REPO_ROOT=/app \
  -e WW_LOGS_DIR=/logs \
  wiley-widget/csx-mcp:local \
  scripts/examples/csharp/50-startup-orchestrator-test.csx
```

#### Run All Database Tests
```bash
# Via VS Code Task
"Tasks: Run Task" → "csx:run-all-database-initializer-tests"
```

#### Debug CSX Script
```bash
# Interactive shell in container
docker run -it --rm -w /app \
  -v "$(pwd):/app:rw" \
  wiley-widget/csx-mcp:local \
  /bin/bash
```

### 9. CI/CD Integration

#### GitHub Actions Workflow (`ci-optimized.yml`)
```yaml
- name: Build CSX Test Image
  run: docker build -t wiley-widget/csx-mcp:local -f docker/Dockerfile.csx-tests .

- name: Run CSX Tests
  run: |
    docker run --rm -w /app \
      -v ${{ github.workspace }}:/app:ro \
      wiley-widget/csx-mcp:local \
      scripts/examples/csharp/50-startup-orchestrator-test.csx
```

### 10. Performance Metrics

| Metric | Value | Notes |
|--------|-------|-------|
| **Docker Image Size** | ~2.5 GB | Includes .NET SDK + cached packages |
| **Image Build Time** | ~5 minutes | Initial build with package downloads |
| **Subsequent Builds** | ~30 seconds | Docker layer caching |
| **Test Execution Time** | 0.5-2 seconds | Per test file |
| **Cold Start** | ~1 second | Container startup + script initialization |
| **Warm Start** | ~0.3 seconds | Cached container state |

### 11. Advantages of This Approach

#### vs Traditional xUnit Tests
| Feature | CSX Tests | xUnit Tests |
|---------|-----------|-------------|
| **Compilation** | ❌ Not required | ✅ Required |
| **Iteration Speed** | ⚡ Instant | 🐌 15-30 seconds |
| **Isolation** | ✅ Complete | ⚠️ Shared AppDomain |
| **Setup Complexity** | ✅ Minimal | ⚠️ Test fixtures needed |
| **Production Code Access** | ✅ Direct | ⚠️ Internal visibility |
| **Debugging** | ⚠️ Limited | ✅ Full IDE support |

#### vs PowerShell Tests
| Feature | CSX Tests | PowerShell Tests |
|---------|-----------|------------------|
| **Type Safety** | ✅ Strong typing | ❌ Weak typing |
| **.NET Integration** | ✅ Native | ⚠️ COM/Reflection |
| **Performance** | ✅ Fast | ⚠️ Moderate |
| **Tooling** | ✅ VS Code + C# | ✅ VS Code + PS |

### 12. Future Enhancements

- [ ] **Parallel Test Execution**: Run multiple CSX tests in parallel containers
- [ ] **Test Results Reporter**: Generate JUnit XML for CI/CD dashboards
- [ ] **Code Coverage**: Integrate with Coverlet for coverage analysis
- [ ] **Visual Studio Integration**: CSX test adapter for Test Explorer
- [ ] **Live Reload**: Watch mode for auto-rerun on file changes
- [ ] **Snapshot Testing**: Assert against known-good state snapshots

### 13. Troubleshooting

#### Common Issues

**Issue**: `docker: image not found`
```bash
# Solution: Build the image
docker build -t wiley-widget/csx-mcp:local -f docker/Dockerfile.csx-tests .
```

**Issue**: `Permission denied` on logs directory
```bash
# Solution: Ensure logs directory exists and is writable
mkdir -p logs && chmod 755 logs
```

**Issue**: NuGet package not found
```bash
# Solution: Rebuild Docker image to refresh package cache
docker build --no-cache -t wiley-widget/csx-mcp:local -f docker/Dockerfile.csx-tests .
```

**Issue**: Test hangs indefinitely
```bash
# Solution: Add timeout to Docker command
docker run --rm -w /app \
  --stop-timeout 30 \
  ... (rest of args)
```

### 14. References

- **C# Scripting Documentation**: https://github.com/dotnet-script/dotnet-script
- **Prism Documentation**: https://prismlibrary.com/docs/
- **Entity Framework Core 9.0**: https://learn.microsoft.com/ef/core/
- **Docker Best Practices**: https://docs.docker.com/develop/dev-best-practices/
- **MCP Specification**: https://modelcontextprotocol.io/

---

**Last Updated**: November 9, 2025  
**Maintainer**: Wiley Widget Development Team  
**Version**: 1.0.0
