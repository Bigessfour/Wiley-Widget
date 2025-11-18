# CSX Test Docker Container Setup

## Overview

The CSX test infrastructure uses Docker containers to provide a consistent, isolated testing environment for C# script execution.

## Container Configuration

### Image: `wiley-widget/csx-mcp:enhanced`

**Base Image**: `mcr.microsoft.com/dotnet/sdk:9.0`

**Pre-installed Tools**:

- dotnet-script (global tool)
- Pre-cached NuGet packages for common dependencies

### Volume Mounts

| Host Path               | Container Path | Mode              | Purpose            |
| ----------------------- | -------------- | ----------------- | ------------------ |
| `<workspace>`           | `/app`         | `ro` (read-only)  | Source code access |
| `<workspace>/logs`      | `/logs`        | `rw` (read-write) | Application logs   |
| `<workspace>/test-logs` | `/test-logs`   | `rw` (read-write) | Test result logs   |

### Environment Variables

| Variable           | Value                      | Purpose                           |
| ------------------ | -------------------------- | --------------------------------- |
| `WW_REPO_ROOT`     | `/app`                     | Root directory for workspace      |
| `WW_LOGS_DIR`      | `/logs`                    | Log output directory              |
| `CSX_ALLOWED_PATH` | `/app`                     | Restrict file access to workspace |
| `NUGET_PACKAGES`   | `/tmp/csharp-mcp-packages` | NuGet package cache location      |

## Security Considerations

### Read-Only Workspace Mount

The workspace is mounted as read-only (`ro`) to prevent:

- Accidental modification of source files
- Test contamination of the codebase
- Security vulnerabilities from malicious scripts

**Implication**: CSX tests cannot write to the workspace directory tree. All output must go to `/logs` or `/test-logs`.

### Allowed Path Restriction

The `CSX_ALLOWED_PATH=/app` environment variable restricts file system access to the workspace directory only.

## Building the Image

### Standard Build

```powershell
docker build -t wiley-widget/csx-mcp:enhanced -f docker/Dockerfile.csx-tests .
```

### Using VS Code Task

Use the task: `Docker: Build Enhanced CSX Image`

## Running CSX Tests

### Via Test Adapter Script

```powershell
# Discover all tests
pwsh scripts/tools/csx-test-adapter.ps1 -Action discover

# Run a specific test
pwsh scripts/tools/csx-test-adapter.ps1 -Action run -TestFile "44-xaml-binding-static-analyzer.csx"

# View test summary
pwsh scripts/tools/csx-test-adapter.ps1 -Action summary
```

### Via VS Code Tasks

Available tasks in `.vscode/tasks.json`:

- `csx:run-20` through `csx:run-25` - Individual Prism E2E tests
- `csx:run-30`, `csx:run-31` - Critical MCP tests
- `csx:run-44-xaml-binding-analyzer` - XAML binding analyzer

### Manual Docker Execution

```powershell
docker run --rm `
    -w /app `
    -v "${PWD}:/app:ro" `
    -v "${PWD}/logs:/logs:rw" `
    -e WW_REPO_ROOT=/app `
    -e WW_LOGS_DIR=/logs `
    wiley-widget/csx-mcp:enhanced `
    scripts/examples/csharp/your-test.csx
```

## Troubleshooting

### Image Not Found

**Error**: `Docker image 'wiley-widget/csx-mcp:enhanced' not found`

**Solution**: Build the image first:

```powershell
docker build -t wiley-widget/csx-mcp:enhanced -f docker/Dockerfile.csx-tests .
```

### Path Not Found Errors

**Error**: `Views folder not found: /app/WileyWidget.UI/Views`

**Cause**: Script looking for incorrect path structure

**Solution**: Update CSX script to use correct paths:

- ✅ Correct: `/app/src/WileyWidget.UI/Views`
- ❌ Wrong: `/app/WileyWidget.UI/Views`

### Compilation Errors in CSX

**Error**: `error CS8620`, `error CS1061`, etc.

**Cause**: Missing NuGet package references or incorrect API usage

**Solution**:

1. Add required `#r "nuget: PackageName, Version"` directives
2. Verify package versions match those in Dockerfile
3. Check that using statements are correct

### Write Permission Errors

**Error**: Cannot write to `/app/...`

**Cause**: Workspace mounted as read-only

**Solution**: Write output to `/logs` or `/test-logs` instead:

```csharp
string outputPath = Path.Combine(
    Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? "/logs",
    "test-output.json"
);
```

### Resource Constraints

If tests are slow or timing out:

**Check Container Resources**:

```powershell
docker stats
```

**Add Resource Limits** (if needed):

```powershell
docker run --rm `
    --memory="2g" `
    --cpus="2" `
    # ... other args
```

## Best Practices

### 1. Use Environment Variables for Paths

```csharp
string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? "/app";
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? "/logs";
```

### 2. Explicit NuGet Package References

Always specify exact versions:

```csharp
#r "nuget: Microsoft.EntityFrameworkCore, 9.0.10"
```

### 3. Error Handling

Provide clear error messages with context:

```csharp
if (!Directory.Exists(viewsPath)) {
    Console.WriteLine($"Views folder not found: {viewsPath}");
    Console.WriteLine($"Current directory: {Directory.GetCurrentDirectory()}");
    Environment.Exit(2);
}
```

### 4. Log Output Location

Write logs to the designated directory:

```csharp
string logFile = Path.Combine(logsDir, "test-results.json");
File.WriteAllText(logFile, jsonOutput);
```

### 5. Exit Codes

Use meaningful exit codes:

- `0` - Success
- `1` - Test failure
- `2` - Configuration/setup error
- `3+` - Specific error conditions

## Integration with CI/CD

The CSX Docker setup integrates with the CI/CD pipeline via:

- GitHub Actions workflow: `ci-optimized.yml`
- Trunk validation: Security and quality scans
- Automated test execution: All CSX tests run in isolated containers

See: `docs/reference/AI_E2E_TESTING_SETUP.md` for complete CI/CD integration details.

## Related Documentation

- [AI E2E Testing Setup](./AI_E2E_TESTING_SETUP.md)
- [Docker Compose Configuration](../../docker-compose.dev.yml)
- [Dockerfile Reference](../../docker/Dockerfile.csx-tests)
- [MCP Server Setup](../../scripts/init-mcp-servers.ps1)
