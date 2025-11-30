# Python MCP Server Setup (GitHub Copilot Chat Integration)

## Overview

The Wiley Widget workspace includes **Python-based MCP (Model Context Protocol) servers** that extend GitHub Copilot Chat with custom workspace tools. These servers run locally and provide tools for inspecting C# code, running builds, navigating the project structure, and fetching web content.

> NOTE: This workspace expects Python 3.14+ for the MCP servers and tools. If you upgraded Python recently, recreate the workspace venv with your 3.14 interpreter (python -m venv .continue\venv). Using a workspace venv avoids relying on a system-installed Store Python path.

**Why Python for MCP?**

- ✅ **Official SDK support** - `@modelcontextprotocol` is natively Python/TypeScript
- ✅ **FastAPI/uvicorn** - Modern async HTTP server framework
- ✅ **Cross-platform** - Works on Windows, Linux, macOS
- ✅ **Easy GitHub API integration** - Native JSON/REST support
- ✅ **Maintainable** - Standard Python patterns, rich ecosystem

**PowerShell scripts** in this repo are just **wrappers to start/manage** the Python servers, not MCP servers themselves.

---

## Configured MCP Servers

### 1. **Wiley Widget GitHub Server** (Custom)

- **Port**: 6723
- **Purpose**: Workspace-specific tools for C# code inspection and .NET operations
- **Tools**:
  - `list-cs-files` → List all C# source files
  - `search-code` → Search codebase with patterns
  - `dotnet-build` → Run dotnet build
  - `dotnet-test` → Run dotnet test
  - `project-tree` → Show directory structure

### 2. **Official Fetch Server** (modelcontextprotocol/servers)

- **Purpose**: Web content fetching and conversion to Markdown for efficient LLM usage
- **Tools**:
  - `fetch` → Fetches URLs and extracts content as Markdown
    - Supports pagination via `start_index` for large pages
    - Respects robots.txt by default
    - Configurable user-agent and proxy support

---

## Architecture

```
GitHub Copilot Chat (VS Code)
        ↓
  .vscode/mcp-settings.json (points to Python servers)
        ↓
  Python Servers:
    • Wiley Widget GitHub (port 6723) → Workspace tools
    • Official Fetch (on-demand)      → Web content retrieval
        ↓
  Workspace Operations & Web Fetching
```

---

## Quick Start

### 1. **Verify Python Environment**

The Python virtual environment should already exist at `.continue/venv/` with required dependencies:

```powershell
# Check if venv exists
Test-Path .continue\venv\Scripts\python.exe

# Verify packages are installed
.continue\venv\Scripts\python.exe -m pip list
# Should show: fastapi, uvicorn, pydantic
```

If venv is missing, create it:

```powershell
python -m venv .continue\venv
.continue\venv\Scripts\pip install fastapi uvicorn pydantic
```

### 2. **Configure GitHub Token (Optional)**

For enhanced GitHub features, set your GitHub personal access token:

**Option A: Environment Variable**

```powershell
$env:GITHUB_TOKEN = "ghp_yourTokenHere"
```

**Option B: Secrets File** (Recommended for persistence)

```powershell
# Create secrets/github_token file (one line, token only)
"ghp_yourTokenHere" | Out-File -FilePath secrets\github_token -NoNewline -Encoding UTF8
```

The `.vscode/profile.ps1` automatically loads this on workspace startup.

### 3. **Start the MCP Server**

**Option A: VS Code Task** (Recommended)

```
Ctrl+Shift+P → Tasks: Run Task → mcp:start-github-server
```

**Option B: PowerShell Script** (Manual)

```powershell
# Foreground (see logs, Ctrl+C to stop)
.\scripts\tools\start-wiley-gh-mcp.ps1

# Background (detached process)
.\scripts\tools\start-wiley-gh-mcp.ps1 -Background

# Kill existing + restart
.\scripts\tools\start-wiley-gh-mcp.ps1 -KillExisting
```

**Option C: Direct Python** (Debug)

```powershell
.continue\venv\Scripts\python.exe .continue\mcpServers\wiley-widget-gh-mcp.py
```

### 4. **Verify Server is Running**

```powershell
# Check port 6723 is listening
netstat -ano | findstr :6723

# Test manifest endpoint
curl http://127.0.0.1:6723/.well-known/ai-plugin.json

# Test list-cs-files endpoint
curl http://127.0.0.1:6723/list-cs-files
```

Expected output:

```
INFO:     Uvicorn running on http://127.0.0.1:6723 (Press CTRL+C to quit)
```

### 5. **Use in GitHub Copilot Chat**

In VS Code Copilot Chat panel, the server tools are automatically available:

```
@github list all ViewModels in this project

@github search for "ServiceProvider" in C# files

@github run dotnet build and show me any errors

@github show the project structure excluding bin/obj
```

GitHub Copilot Chat will automatically discover and use the MCP server tools based on `.vscode/mcp-settings.json`.

---

## Configuration Files

### `.vscode/mcp-settings.json`

Main configuration file for GitHub Copilot Chat MCP integration:

```json
{
  "mcpServers": {
    "wiley-widget-gh": {
      "command": "${workspaceFolder}\\.continue\\venv\\Scripts\\python.exe",
      "args": ["${workspaceFolder}\\.continue\\mcpServers\\wiley-widget-gh-mcp.py"],
      "env": {
        "WW_REPO_ROOT": "${workspaceFolder}",
        "GITHUB_TOKEN": "${env:GITHUB_TOKEN}"
      }
    }
  }
}
```

**Key points:**

- Uses **absolute paths** to Python executable and script
- Passes `WW_REPO_ROOT` to server (required)
- Inherits `GITHUB_TOKEN` from environment (optional)

### `.continue/mcpServers/wiley-widget-gh-mcp.py`

Python FastAPI server implementation (171 lines). Exposes:

| Endpoint                      | Method | Description                              |
| ----------------------------- | ------ | ---------------------------------------- |
| `/.well-known/ai-plugin.json` | GET    | MCP manifest (required by Copilot)       |
| `/openapi.json`               | GET    | OpenAPI spec for tools                   |
| `/list-cs-files`              | GET    | List all `*.cs` files (excludes bin/obj) |
| `/search-code`                | POST   | Search code with regex pattern           |
| `/dotnet-build`               | POST   | Run `dotnet build` and capture output    |
| `/dotnet-test`                | POST   | Run `dotnet test` and capture results    |
| `/project-tree`               | GET    | Show directory tree structure            |

### `.continue/mcpServers/wiley-widget-gh.yaml`

Alternative YAML-based configuration (for Continue.dev compatibility):

```yaml
name: wiley-widget-gh
command:
  - 'C:/Users/biges/Desktop/Wiley-Widget/.continue/venv/Scripts/python.exe'
  - wiley-widget-gh-mcp.py
workingDirectory: .continue/mcpServers
```

**Note:** GitHub Copilot Chat uses `.vscode/mcp-settings.json`, not this YAML file.

---

## Management Scripts

### `scripts/tools/start-wiley-gh-mcp.ps1`

Production-ready launcher with features:

- ✅ Pre-flight checks (Python venv, port availability)
- ✅ Kill existing processes with `-KillExisting`
- ✅ Background mode with log file redirection
- ✅ Environment variable setup
- ✅ Health check after startup

**Parameters:**

```powershell
-Background      # Run as detached process
-Port 6723       # Custom port (default: 6723)
-KillExisting    # Terminate existing server before start
```

### `scripts/tools/validate-mcp-setup.ps1`

Comprehensive validation script that checks:

- ✅ Python venv and package installation
- ✅ Server script syntax and structure
- ✅ Port availability and binding
- ✅ Docker MCP containers (C#, GitHub, etc.)
- ✅ Environment variables
- ✅ Endpoint health checks with `-HealthCheck`

**Parameters:**

```powershell
-HealthCheck     # Test all server endpoints
-UpdateImages    # Pull latest Docker images
-StartServers    # Auto-start stopped servers
```

**Usage:**

```powershell
# Basic validation
.\scripts\tools\validate-mcp-setup.ps1

# Full health check
.\scripts\tools\validate-mcp-setup.ps1 -HealthCheck

# Auto-fix and start servers
.\scripts\tools\validate-mcp-setup.ps1 -StartServers -HealthCheck
```

---

## Troubleshooting

### Server Won't Start

**Problem:** Port 6723 already in use

```powershell
# Find process on port 6723
netstat -ano | findstr :6723

# Kill specific PID
Stop-Process -Id <PID> -Force

# Or use the startup script
.\scripts\tools\start-wiley-gh-mcp.ps1 -KillExisting
```

**Problem:** Python venv not found

```powershell
# Recreate venv
python -m venv .continue\venv
.continue\venv\Scripts\pip install fastapi uvicorn pydantic
```

**Problem:** Missing dependencies

```powershell
# Reinstall packages
.continue\venv\Scripts\pip install --upgrade fastapi uvicorn pydantic
```

### Server Runs But Copilot Can't Connect

**Problem:** `.vscode/mcp-settings.json` not recognized

```powershell
# Reload VS Code window
Ctrl+Shift+P → Developer: Reload Window
```

**Problem:** Wrong Python path in config

```powershell
# Verify Python executable exists
Test-Path "C:/Users/biges/Desktop/Wiley-Widget/.continue/venv/Scripts/python.exe"

# Update mcp-settings.json with correct absolute path
```

### Test Endpoints Manually

```powershell
# Test manifest (should return JSON)
Invoke-RestMethod http://127.0.0.1:6723/.well-known/ai-plugin.json

# Test list C# files (should return array)
Invoke-RestMethod http://127.0.0.1:6723/list-cs-files

# Test project tree
Invoke-RestMethod http://127.0.0.1:6723/project-tree

# Test dotnet build (POST with JSON body)
Invoke-RestMethod http://127.0.0.1:6723/dotnet-build `
  -Method POST `
  -ContentType "application/json" `
  -Body '{"configuration":"Debug"}'
```

### View Server Logs

**Foreground mode:**

```powershell
# See live output
.\scripts\tools\start-wiley-gh-mcp.ps1
```

**Background mode:**

```powershell
# Check log file
Get-Content logs\mcp-github-server.log -Tail 50 -Wait
```

### Validate Full Setup

```powershell
# Comprehensive validation
.\scripts\tools\validate-mcp-setup.ps1 -HealthCheck -Verbose

# Expected output:
# ✅ Python venv found
# ✅ FastAPI/uvicorn installed
# ✅ Port 6723 listening
# ✅ Manifest endpoint responding
# ✅ List-cs-files endpoint responding
```

---

## VS Code Tasks

The following tasks are available via `Ctrl+Shift+P → Tasks: Run Task`:

| Task Label                | Description                                  |
| ------------------------- | -------------------------------------------- |
| `mcp:start-github-server` | Start Python MCP server in background        |
| `mcp:validate-setup`      | Run comprehensive validation                 |
| `mcp:init-servers`        | Initialize all MCP servers (Docker + Python) |

---

## Development Workflow

### Modifying the MCP Server

1. **Edit Python script:**

   ```powershell
   code .continue\mcpServers\wiley-widget-gh-mcp.py
   ```

2. **Stop existing server:**

   ```powershell
   # Find and kill Python process
   Get-Process python | Where-Object {$_.CommandLine -match "wiley-widget"} | Stop-Process -Force
   ```

3. **Test changes:**

   ```powershell
   # Run in foreground to see output
   .continue\venv\Scripts\python.exe .continue\mcpServers\wiley-widget-gh-mcp.py
   ```

4. **Verify endpoints:**

   ```powershell
   curl http://127.0.0.1:6723/list-cs-files
   ```

5. **Restart for Copilot:**

   ```powershell
   # Background mode
   .\scripts\tools\start-wiley-gh-mcp.ps1 -Background -KillExisting

   # Reload VS Code
   Ctrl+Shift+P → Developer: Reload Window
   ```

### Adding New Tools

1. Add new FastAPI endpoint in `wiley-widget-gh-mcp.py`:

   ```python
   @app.get("/my-new-tool")
   def my_new_tool():
       return {"result": "data"}
   ```

2. Update OpenAPI spec (auto-generated by FastAPI)

3. Restart server and test:

   ```powershell
   curl http://127.0.0.1:6723/my-new-tool
   ```

4. Tool is automatically available in Copilot Chat:

   ```
   @github use my-new-tool to get data
   ```

---

## Why This Architecture?

### Python MCP Server (Core)

- **Standard Protocol:** Aligns with MCP SDK patterns
- **FastAPI:** Modern async framework with automatic OpenAPI docs
- **Maintainable:** Easy to add new tools, debug, and test
- **Cross-platform:** Same code works on all OSes

### PowerShell Management Scripts (Wrappers)

- **Windows Integration:** Native process management, port checking
- **Automation:** VS Code tasks, validation, health checks
- **Developer UX:** Simple commands, clear output, error handling

### Separation of Concerns

```
┌─────────────────────────────────────┐
│  GitHub Copilot Chat (VS Code)      │  ← User Interface
└─────────────┬───────────────────────┘
              │
┌─────────────▼───────────────────────┐
│  .vscode/mcp-settings.json          │  ← Configuration
└─────────────┬───────────────────────┘
              │
┌─────────────▼───────────────────────┐
│  Python FastAPI Server (port 6723)  │  ← MCP Protocol
└─────────────┬───────────────────────┘
              │
┌─────────────▼───────────────────────┐
│  PowerShell Management Scripts      │  ← Automation
└─────────────────────────────────────┘
```

---

## References

- **MCP Specification:** <https://spec.modelcontextprotocol.io/>
- **FastAPI Documentation:** <https://fastapi.tiangolo.com/>
- **GitHub Copilot Chat MCP:** <https://docs.github.com/copilot/customizing-copilot/using-mcp-servers>
- **Wiley Widget MCP Setup:** `docs/integration/mcp-server-setup.md`

---

## Next Steps

1. ✅ Verify server is running: `netstat -ano | findstr :6723`
2. ✅ Test in Copilot Chat: `@github list all C# files`
3. ✅ Add custom tools to `wiley-widget-gh-mcp.py` as needed
4. ✅ Monitor logs: `Get-Content logs\mcp-github-server.log -Tail 50 -Wait`
5. ✅ Run validation regularly: `.\scripts\tools\validate-mcp-setup.ps1 -HealthCheck`

**The Python MCP server is production-ready. PowerShell scripts are just management wrappers.**
