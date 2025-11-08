# C# MCP Server Quick Reference

## 🚀 Quick Start

```powershell
# Pull MCP image
docker pull ghcr.io/infinityflowapp/csharp-mcp:latest

# Build local CSX image
docker build -t wiley-widget/csx-mcp:local -f docker\Dockerfile.csx-tests .

# Run diagnostics
.\scripts\diagnose-mcp-server.ps1

# Run tests
.\scripts\test-mcp-server.ps1
```

## 🔍 Diagnostics

| Command                                        | Purpose                |
| ---------------------------------------------- | ---------------------- |
| `.\scripts\diagnose-mcp-server.ps1`            | Full diagnostic scan   |
| `.\scripts\diagnose-mcp-server.ps1 -Verbose`   | Detailed diagnostics   |
| `.\scripts\diagnose-mcp-server.ps1 -FixIssues` | Auto-fix common issues |
| `docker ps -a --filter "name=csharp-mcp"`      | Check container status |
| `docker logs csharp-mcp-vscode`                | View container logs    |

## 🧪 Testing

| Command                                                         | Purpose                 |
| --------------------------------------------------------------- | ----------------------- |
| `.\scripts\test-mcp-server.ps1`                                 | Run basic tests         |
| `.\scripts\test-mcp-server.ps1 -TestScript "01-basic-test.csx"` | Test specific script    |
| `.\scripts\test-mcp-server.ps1 -AllPrismTests`                  | Test all Prism E2E      |
| `.\scripts\test-mcp-server.ps1 -UseDockerCompose`               | Test via docker-compose |

## 🐳 Docker Commands

### Start MCP Server (stdio mode)

```powershell
docker run -i --rm `
  --name csharp-mcp-manual `
  -v "C:\Users\biges\Desktop\Wiley_Widget:/scripts:ro" `
  -v "C:\Users\biges\Desktop\Wiley_Widget\logs:/logs" `
  -e CSX_ALLOWED_PATH=/scripts `
  -e WW_REPO_ROOT=/scripts `
  -e WW_LOGS_DIR=/logs `
  ghcr.io/infinityflowapp/csharp-mcp:latest
```

### Execute .csx Script

```powershell
docker run --rm `
  -v "C:\Users\biges\Desktop\Wiley_Widget\scripts\examples\csharp:/app:ro" `
  -v "C:\Users\biges\Desktop\Wiley_Widget\logs:/logs" `
  -e WW_REPO_ROOT=/app `
  -e WW_LOGS_DIR=/logs `
  wiley-widget/csx-mcp:local 01-basic-test.csx
```

### Docker Compose

```powershell
# Start all MCP services
docker-compose up -d

# Start only csharp-mcp
docker-compose up -d csharp-mcp

# View logs
docker-compose logs csharp-mcp

# Stop services
docker-compose down
```

## 📝 VS Code Tasks

| Task                      | Command                          |
| ------------------------- | -------------------------------- |
| `mcp:start-csharp-mcp`    | Start MCP server in VS Code      |
| `csx:run-20`              | Run Prism container E2E test     |
| `csx:run-all-prism-tests` | Run all Prism tests              |
| `mcp: analyze`            | Run MCP analysis (default build) |

Run tasks: `Ctrl+Shift+B` or Terminal → Run Task

## 🔧 Configuration Files

| File                                    | Purpose                    |
| --------------------------------------- | -------------------------- |
| `.vscode/mcp.json`                      | VS Code MCP server config  |
| `docker-compose.yml`                    | Docker service definitions |
| `docker/Dockerfile.csx-tests`           | CSX test image             |
| `docker/Dockerfile.csharp-mcp-enhanced` | Enhanced MCP server        |

## 🐛 Common Issues

### Container Exits Immediately

```powershell
# Check logs
docker logs <container_id>

# Ensure -i flag (interactive)
docker run -i --rm ...
```

### No JSON-RPC Response

```powershell
# Test stdio manually
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}' | docker run -i --rm -e CSX_ALLOWED_PATH=/scripts ghcr.io/infinityflowapp/csharp-mcp:latest
```

### VS Code Not Connecting

```powershell
# Verify config
Get-Content .vscode\mcp.json

# Restart VS Code
# Check MCP extension logs (Output panel)
```

### Script Execution Fails

```powershell
# Build csx image
docker build -t wiley-widget/csx-mcp:local -f docker\Dockerfile.csx-tests .

# Check volume mounts (absolute paths)
# Verify script exists in mounted path
```

## 🔄 Reset & Rebuild

```powershell
# Stop all MCP containers
docker stop $(docker ps -a -q --filter "name=csharp-mcp")
docker rm $(docker ps -a -q --filter "name=csharp-mcp")

# Remove images
docker rmi ghcr.io/infinityflowapp/csharp-mcp:latest
docker rmi wiley-widget/csx-mcp:local

# Clean Docker
docker system prune -a -f

# Rebuild
docker pull ghcr.io/infinityflowapp/csharp-mcp:latest
docker build -t wiley-widget/csx-mcp:local -f docker\Dockerfile.csx-tests .

# Test
.\scripts\diagnose-mcp-server.ps1
```

## 📊 Environment Variables

| Variable                      | Purpose              | Default    |
| ----------------------------- | -------------------- | ---------- |
| `CSX_ALLOWED_PATH`            | Restrict file access | `/scripts` |
| `WW_REPO_ROOT`                | Workspace root       | `/scripts` |
| `WW_LOGS_DIR`                 | Logs directory       | `/logs`    |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | Disable telemetry    | `1`        |
| `DOTNET_NOLOGO`               | Hide .NET logo       | `1`        |

## 🎯 Prism E2E Tests

| Test                 | Script                                       | Purpose                  |
| -------------------- | -------------------------------------------- | ------------------------ |
| Container Health     | `20-prism-container-e2e-test.csx`            | Validate container setup |
| Modules              | `21-prism-modules-e2e-test.csx`              | Test module loading      |
| DI Registration      | `22-prism-di-registration-e2e-test.csx`      | Test DI setup            |
| Module Lifecycle     | `23-prism-module-lifecycle-e2e-test.csx`     | Test lifecycle           |
| Container Resolution | `24-prism-container-resolution-e2e-test.csx` | Test resolution          |
| Region Adapters      | `25-prism-region-adapters-e2e-test.csx`      | Test regions             |

## 📚 Documentation

- [Full Troubleshooting Guide](./CSHARP_MCP_TROUBLESHOOTING.md)
- [MCP Setup Guide](./mcp-setup.md)
- [CI Integration](./CSHARP_MCP_CI_INTEGRATION.md)

## 🆘 Get Help

```powershell
# Generate diagnostic report
.\scripts\diagnose-mcp-server.ps1 -Verbose > diagnostic-report.txt

# Collect logs
docker logs csharp-mcp-vscode > container-logs.txt

# Test minimal setup
docker run -i --rm ghcr.io/infinityflowapp/csharp-mcp:latest
```

---

**Wiley Widget** | .NET 9.0 | Prism 9.x | MCP Protocol 2024-11-05
