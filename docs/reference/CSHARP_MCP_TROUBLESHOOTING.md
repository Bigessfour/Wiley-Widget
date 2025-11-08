# C# MCP Server Troubleshooting Guide for Wiley Widget

## Overview

This guide provides comprehensive troubleshooting steps for resolving "no response" issues with the C# MCP (Model Context Protocol) Server in Docker for the Wiley Widget project. The MCP server enables execution of C# scripts (.csx) for E2E testing of Prism WPF modules.

## Table of Contents

1. [Quick Diagnostics](#quick-diagnostics)
2. [Common Issues](#common-issues)
3. [Configuration Files](#configuration-files)
4. [Testing Tools](#testing-tools)
5. [Step-by-Step Fixes](#step-by-step-fixes)

---

## Quick Diagnostics

### Run Automated Diagnostics

```powershell
# Comprehensive diagnostic scan
.\scripts\diagnose-mcp-server.ps1 -Verbose

# With automatic fixes
.\scripts\diagnose-mcp-server.ps1 -FixIssues

# Test execution only
.\scripts\diagnose-mcp-server.ps1 -TestOnly
```

### Manual Health Checks

```powershell
# 1. Check Docker status
docker --version
docker info

# 2. Check MCP image
docker images ghcr.io/infinityflowapp/csharp-mcp

# 3. Check running containers
docker ps -a --filter "name=csharp-mcp"

# 4. View container logs
docker logs csharp-mcp-vscode --tail 50
```

---

## Common Issues

### Issue 1: Container Exits Immediately

**Symptoms:**

- Container starts but exits with code 0 or 1
- No output in logs
- `docker ps` shows container as "Exited"

**Diagnosis:**

```powershell
docker logs <container_id>
```

**Fixes:**

1. **Missing volume mounts:**

   ```powershell
   docker run -i --rm \
     -v "C:\Users\biges\Desktop\Wiley_Widget:/scripts:ro" \
     -v "C:\Users\biges\Desktop\Wiley_Widget\logs:/logs" \
     -e CSX_ALLOWED_PATH=/scripts \
     ghcr.io/infinityflowapp/csharp-mcp:latest
   ```

2. **Wrong entrypoint:** The MCP server expects stdio input. Don't run detached (`-d`).

3. **Environment variables missing:** Ensure `CSX_ALLOWED_PATH`, `WW_REPO_ROOT`, and `WW_LOGS_DIR` are set.

---

### Issue 2: No Response from stdio Communication

**Symptoms:**

- Container running but no JSON-RPC response
- Hangs after sending request
- Empty output

**Diagnosis:**

```powershell
# Test stdio manually
$initRequest = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'

echo $initRequest | docker run -i --rm `
  -v "C:\Users\biges\Desktop\Wiley_Widget:/scripts:ro" `
  -e CSX_ALLOWED_PATH=/scripts `
  ghcr.io/infinityflowapp/csharp-mcp:latest
```

**Expected Response:**

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "protocolVersion": "2024-11-05",
    "serverInfo": { "name": "csharp-mcp", "version": "1.0.0" },
    "capabilities": { "tools": { "listChanged": false } }
  }
}
```

**Fixes:**

1. **Ensure interactive mode:** Always use `-i` flag, never `-d`
2. **Check stdin is open:** Use `stdin_open: true` in docker-compose
3. **Verify JSON format:** Request must be valid JSON-RPC 2.0

---

### Issue 3: VS Code MCP Extension Not Connecting

**Symptoms:**

- VS Code shows "MCP server not available"
- Copilot can't execute C# code
- No server listed in MCP extension

**Diagnosis:**
Check `.vscode/mcp.json` configuration:

```powershell
Get-Content .vscode\mcp.json | ConvertFrom-Json
```

**Fixes:**

1. **Verify configuration:**

   ```json
   {
     "mcpServers": {
       "csharp-mcp": {
         "command": "C:\\Program Files\\Docker\\Docker\\resources\\bin\\docker.exe",
         "args": [
           "run",
           "-i",
           "--rm",
           "--name",
           "csharp-mcp-vscode",
           "-v",
           "C:\\Users\\biges\\Desktop\\Wiley_Widget:/scripts:ro",
           "-v",
           "C:\\Users\\biges\\Desktop\\Wiley_Widget\\logs:/logs",
           "-e",
           "CSX_ALLOWED_PATH=/scripts",
           "-e",
           "WW_REPO_ROOT=/scripts",
           "-e",
           "WW_LOGS_DIR=/logs",
           "ghcr.io/infinityflowapp/csharp-mcp:latest"
         ]
       }
     }
   }
   ```

2. **Restart VS Code** after configuration changes

3. **Check MCP extension logs:**
   - Open VS Code Output panel
   - Select "MCP Extension" from dropdown
   - Look for connection errors

---

### Issue 4: Script Execution Fails

**Symptoms:**

- MCP server responds but script execution fails
- "File not found" errors
- Permission denied errors

**Diagnosis:**

```powershell
# Test direct script execution
docker run --rm `
  -v "C:\Users\biges\Desktop\Wiley_Widget\scripts\examples\csharp:/app:ro" `
  -v "C:\Users\biges\Desktop\Wiley_Widget\logs:/logs" `
  -e WW_REPO_ROOT=/app `
  -e WW_LOGS_DIR=/logs `
  wiley-widget/csx-mcp:local 01-basic-test.csx
```

**Fixes:**

1. **Build csx-tests image:**

   ```powershell
   docker build -t wiley-widget/csx-mcp:local -f docker\Dockerfile.csx-tests .
   ```

2. **Check volume mounts:** Ensure paths are correct and using absolute paths

3. **Verify file permissions:** Scripts must be readable (`:ro` is fine)

4. **Check script syntax:** Run `dotnet script` locally first

---

### Issue 5: Port Binding Failures

**Symptoms:**

- "Address already in use" error
- Container fails to start
- Port conflicts in logs

**Diagnosis:**

```powershell
# Check what's using port 5000
netstat -ano | findstr :5000
```

**Fixes:**

1. **Use different port:**

   ```powershell
   docker run -p 5001:5000 ...
   ```

2. **Stop conflicting services:**

   ```powershell
   # Find process ID
   $pid = (Get-NetTCPConnection -LocalPort 5000).OwningProcess
   # Stop process
   Stop-Process -Id $pid -Force
   ```

3. **Use stdio mode (recommended):** MCP protocol doesn't require HTTP ports

---

## Configuration Files

### .vscode/mcp.json

Complete working configuration:

```json
{
  "mcpServers": {
    "csharp-mcp": {
      "command": "C:\\Program Files\\Docker\\Docker\\resources\\bin\\docker.exe",
      "args": [
        "run",
        "-i",
        "--rm",
        "--name",
        "csharp-mcp-vscode",
        "-v",
        "C:\\Users\\biges\\Desktop\\Wiley_Widget:/scripts:ro",
        "-v",
        "C:\\Users\\biges\\Desktop\\Wiley_Widget\\logs:/logs",
        "-e",
        "CSX_ALLOWED_PATH=/scripts",
        "-e",
        "WW_REPO_ROOT=/scripts",
        "-e",
        "WW_LOGS_DIR=/logs",
        "-e",
        "DOTNET_CLI_TELEMETRY_OPTOUT=1",
        "-e",
        "DOTNET_NOLOGO=1",
        "ghcr.io/infinityflowapp/csharp-mcp:latest"
      ],
      "env": {
        "CSX_ALLOWED_PATH": "/scripts",
        "WW_REPO_ROOT": "/scripts",
        "WW_LOGS_DIR": "/logs"
      }
    }
  }
}
```

### docker-compose.yml

Complete csharp-mcp service configuration:

```yaml
version: "3.9"
services:
  csharp-mcp:
    image: ghcr.io/infinityflowapp/csharp-mcp:latest
    stdin_open: true
    tty: false
    networks: ["mcp_net"]
    ports: ["8002:5000"]
    mem_limit: 1g
    cpus: 2
    pids_limit: 256
    volumes:
      - ./scripts:/scripts:ro
      - ./logs:/logs:rw
      - ./scripts/examples/csharp:/app/tests:ro
    environment:
      CSX_ALLOWED_PATH: "/scripts"
      WW_REPO_ROOT: "/scripts"
      WW_LOGS_DIR: "/logs"
      DOTNET_CLI_TELEMETRY_OPTOUT: "1"
      DOTNET_NOLOGO: "1"
    restart: unless-stopped

networks:
  mcp_net:
    driver: bridge
```

---

## Testing Tools

### Automated Test Suite

Run comprehensive tests:

```powershell
# Full test suite
.\scripts\test-mcp-server.ps1

# Test specific script
.\scripts\test-mcp-server.ps1 -TestScript "01-basic-test.csx"

# Test all Prism E2E scenarios
.\scripts\test-mcp-server.ps1 -AllPrismTests

# Test with Docker Compose
.\scripts\test-mcp-server.ps1 -UseDockerCompose
```

### Manual Testing

#### Test stdio Protocol

```powershell
# Initialize request
$init = @{
    jsonrpc = "2.0"
    id = 1
    method = "initialize"
    params = @{
        protocolVersion = "2024-11-05"
        capabilities = @{}
        clientInfo = @{ name = "test"; version = "1.0" }
    }
} | ConvertTo-Json -Compress

# Send to MCP server
$init | docker run -i --rm `
  -v "C:\Users\biges\Desktop\Wiley_Widget:/scripts:ro" `
  -e CSX_ALLOWED_PATH=/scripts `
  ghcr.io/infinityflowapp/csharp-mcp:latest
```

#### Test Script Execution

```powershell
# Execute basic test
docker run --rm `
  -v "C:\Users\biges\Desktop\Wiley_Widget\scripts\examples\csharp:/app:ro" `
  -v "C:\Users\biges\Desktop\Wiley_Widget\logs:/logs" `
  -e WW_REPO_ROOT=/app `
  -e WW_LOGS_DIR=/logs `
  wiley-widget/csx-mcp:local 01-basic-test.csx
```

---

## Step-by-Step Fixes

### Complete Setup from Scratch

1. **Install Docker Desktop**

   ```powershell
   winget install Docker.DockerDesktop
   ```

2. **Pull MCP Image**

   ```powershell
   docker pull ghcr.io/infinityflowapp/csharp-mcp:latest
   ```

3. **Build Local CSX Image**

   ```powershell
   docker build -t wiley-widget/csx-mcp:local -f docker\Dockerfile.csx-tests .
   ```

4. **Configure VS Code**
   - Ensure `.vscode/mcp.json` exists with correct configuration
   - Restart VS Code

5. **Test Setup**
   ```powershell
   .\scripts\diagnose-mcp-server.ps1
   .\scripts\test-mcp-server.ps1
   ```

### Reset and Rebuild

If all else fails:

```powershell
# 1. Stop all MCP containers
docker stop $(docker ps -a -q --filter "name=csharp-mcp")
docker rm $(docker ps -a -q --filter "name=csharp-mcp")

# 2. Remove images
docker rmi ghcr.io/infinityflowapp/csharp-mcp:latest
docker rmi wiley-widget/csx-mcp:local

# 3. Clean Docker cache
docker system prune -a -f

# 4. Re-pull and rebuild
docker pull ghcr.io/infinityflowapp/csharp-mcp:latest
docker build -t wiley-widget/csx-mcp:local -f docker\Dockerfile.csx-tests .

# 5. Test
.\scripts\diagnose-mcp-server.ps1
```

---

## Wiley Widget Specific Notes

### Prism E2E Tests

The following .csx scripts test Prism container functionality:

- `20-prism-container-e2e-test.csx` - Container health validation
- `21-prism-modules-e2e-test.csx` - Module loading tests
- `22-prism-di-registration-e2e-test.csx` - DI registration tests
- `23-prism-module-lifecycle-e2e-test.csx` - Module lifecycle tests
- `24-prism-container-resolution-e2e-test.csx` - Container resolution tests
- `25-prism-region-adapters-e2e-test.csx` - Region adapter tests

Run all tests:

```powershell
.\scripts\test-mcp-server.ps1 -AllPrismTests
```

### Log Analysis

MCP server writes to `logs/` directory. Check:

```powershell
# View latest logs
Get-Content logs\*.log -Tail 50

# Search for errors
Select-String -Path logs\*.log -Pattern "error|exception|fail" -Context 2,2
```

### Syncfusion Integration

For Syncfusion-related diagnostics in logs:

```powershell
# Search for Syncfusion errors
Select-String -Path logs\*.log -Pattern "Syncfusion|SfLogAnalyzer" -Context 3,3
```

---

## Getting Help

If issues persist after following this guide:

1. **Collect diagnostics:**

   ```powershell
   .\scripts\diagnose-mcp-server.ps1 -Verbose > mcp-diagnostic-report.txt
   ```

2. **Check Docker logs:**

   ```powershell
   docker logs csharp-mcp-vscode > mcp-container-logs.txt
   ```

3. **Verify configuration:**

   ```powershell
   Get-Content .vscode\mcp.json
   Get-Content docker-compose.yml
   ```

4. **Test with minimal setup:**
   ```powershell
   docker run -i --rm ghcr.io/infinityflowapp/csharp-mcp:latest
   # Paste: {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}
   ```

---

## Additional Resources

- [MCP Setup Guide](./mcp-setup.md)
- [C# MCP CI Integration](./CSHARP_MCP_CI_INTEGRATION.md)
- [Docker Documentation](https://docs.docker.com/)
- [Model Context Protocol Spec](https://modelcontextprotocol.io/)

---

**Last Updated:** 2025-10-31
**Wiley Widget Version:** Compatible with .NET 9.0 and Prism 9.x
