# MCP Server Setup Guide for Wiley-Widget Project

**Last Updated:** 2025-01-15  
**Applies To:** Visual Studio 2026, VS Code with GitHub Copilot

---

## 🎯 Overview

This project uses **Model Context Protocol (MCP) servers** to enhance AI-assisted development. MCP servers provide specialized context and tooling for:

- **Filesystem operations** (file reading, searching, editing)
- **Syncfusion WinForms components** (API docs, code generation)
- **Debug instrumentation** (MCP traffic logging)
- **Custom UI tooling** (WileyWidget-specific helpers)

---

## 📋 Available MCP Servers

### 1. **Filesystem Server** (`@modelcontextprotocol/server-filesystem`)

- **Purpose:** Enables MCP-compliant file operations (read, write, search, list)
- **Required By:** `.vscode/copilot-mcp-rules.md` enforcement
- **Usage:** All file operations should use MCP tools instead of direct shell commands

### 2. **MCP Debugger** (`@debugmcp/mcp-debugger`)

- **Purpose:** Logs MCP protocol traffic to diagnose tool invocation issues
- **Log Location:** `logs/mcp-debugger.log`
- **Usage:** Troubleshooting when MCP tools aren't being called correctly

### 3. **Syncfusion WinForms Assistant** (`@syncfusion/winforms-assistant`)

- **Purpose:** Official Syncfusion documentation and code generation for WinForms
- **Required:** Syncfusion API Key (get from https://syncfusion.com/account/api-key)
- **Usage:** Query Syncfusion API docs, resolve package issues, generate control code

### 4. **WileyWidget UI MCP** (Custom)

- **Purpose:** Project-specific UI component helpers and patterns
- **Location:** `tools/SyncfusionMcpServer/tools/WileyWidgetMcpServer/`
- **Usage:** Project-specific scaffolding and UI conventions

---

## ⚙️ Configuration Files

### VS Code Configuration (`.vscode/mcp.json`)

```json
{
  "servers": {
    "filesystem": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "${workspaceFolder}"]
    },
    "mcp-debugger": {
      "command": "npx",
      "args": [
        "-y",
        "@debugmcp/mcp-debugger@0.17.0",
        "stdio",
        "--log-level",
        "info",
        "--log-file",
        "${workspaceFolder}\\logs\\mcp-debugger.log"
      ]
    },
    "syncfusion-winforms-assistant": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@syncfusion/winforms-assistant@latest"],
      "env": {
        "Syncfusion_API_Key": "${env:SYNCFUSION_MCP_API_KEY}"
      }
    },
    "wileywidget-ui-mcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/tools/SyncfusionMcpServer/tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj",
        "--no-build"
      ]
    }
  }
}
```

**Key Features:**

- ✅ Uses `${workspaceFolder}` variable (auto-resolved by VS Code)
- ✅ Trailing commas allowed (VS Code JSON parser is lenient)
- ✅ Environment variables: `${env:VARIABLE_NAME}`

---

### Visual Studio Configuration (`.vs/mcp.json`)

```json
{
  "servers": {
    "filesystem": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "C:\\Users\\biges\\Desktop\\Wiley-Widget"]
    },
    "mcp-debugger": {
      "type": "stdio",
      "command": "npx",
      "args": [
        "-y",
        "@debugmcp/mcp-debugger@0.17.0",
        "stdio",
        "--log-level",
        "info",
        "--log-file",
        "C:\\Users\\biges\\Desktop\\Wiley-Widget\\logs\\mcp-debugger.log"
      ]
    },
    "syncfusion-winforms-assistant": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@syncfusion/winforms-assistant@latest"],
      "env": {
        "Syncfusion_API_Key": "${env:SYNCFUSION_MCP_API_KEY}"
      }
    },
    "wileywidget-ui-mcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Users\\biges\\Desktop\\Wiley-Widget\\tools\\SyncfusionMcpServer\\tools\\WileyWidgetMcpServer\\WileyWidgetMcpServer.csproj",
        "--no-build"
      ]
    }
  }
}
```

**Key Differences from VS Code:**

- ⚠️ **MUST use absolute paths** (no `${workspaceFolder}` variable support)
- ⚠️ **No trailing commas** (strict JSON parser)
- ⚠️ **Explicit `"type": "stdio"`** required for all servers
- ✅ Environment variables still work: `${env:VARIABLE_NAME}`

---

## 🔐 Setting Up Syncfusion API Key

### Option 1: User Environment Variable (Recommended)

**Windows (PowerShell):**

```powershell
# Set for current user (persists across sessions)
[System.Environment]::SetEnvironmentVariable("SYNCFUSION_MCP_API_KEY", "your-api-key-here", "User")

# Restart Visual Studio/VS Code for changes to take effect
```

**Verify:**

```powershell
$env:SYNCFUSION_MCP_API_KEY
```

### Option 2: Session Environment Variable (Temporary)

**PowerShell:**

```powershell
# Set for current session only
$env:SYNCFUSION_MCP_API_KEY = "your-api-key-here"

# Launch VS Code/Visual Studio from same PowerShell session
code .
# or
devenv .
```

### Option 3: Direct Configuration (Not Recommended)

**⚠️ Security Warning:** Hardcoding API keys in JSON files exposes them in version control.

If you must use this method:

1. Add `.vs/mcp.json` to `.gitignore`
2. Replace `${env:SYNCFUSION_MCP_API_KEY}` with your key directly
3. Never commit this file

---

## 🚀 Activation & Usage

### In VS Code (GitHub Copilot)

1. **Open GitHub Copilot Chat** (Ctrl+Shift+I or Cmd+Shift+I)
2. **Invoke MCP servers:**
   - **Filesystem:** Automatically used by Copilot when following MCP rules
   - **Syncfusion:** `#SyncfusionWinFormsAssistant How do I add SfDataGrid?`
   - **Custom UI:** `@wileywidget-ui-mcp Generate a panel template`

3. **Check Server Status:**
   - Open Command Palette (Ctrl+Shift+P)
   - Search: "MCP: Show Servers"
   - Verify all servers show "Running"

4. **Debug MCP Traffic:**
   - Check `logs/mcp-debugger.log` for protocol messages
   - Filter by server name to isolate issues

### In Visual Studio 2026 (GitHub Copilot)

1. **Open GitHub Copilot Chat Window** (Alt+/ or View → GitHub Copilot Chat)
2. **Click "Ask" dropdown → "Agent"**
3. **Select MCP server from tools:**
   - `SyncfusionWinFormsAssistant`
   - `filesystem`
   - `mcp-debugger`
   - `wileywidget-ui-mcp`

4. **Ask questions:**

   ```
   @SyncfusionWinFormsAssistant What packages do I need for SfDataGrid in .NET 10?
   ```

5. **Check Server Status:**
   - Output window → "GitHub Copilot" pane
   - Look for MCP server connection messages

---

## 🩺 Troubleshooting

### Issue 1: "MCP server not found" or "Server failed to start"

**Cause:** npm package not installed or Node.js version mismatch

**Fix:**

```powershell
# Check Node.js version (must be >= 18)
node --version

# Manually install MCP servers
npx -y @modelcontextprotocol/server-filesystem --version
npx -y @syncfusion/winforms-assistant@latest --version
npx -y @debugmcp/mcp-debugger@0.17.0 --version

# If errors occur, clear npm cache
npm cache clean --force
```

---

### Issue 2: "Syncfusion API Key invalid" or "Authentication failed"

**Cause:** Environment variable not set or incorrect key

**Fix:**

```powershell
# Verify environment variable exists
Get-ChildItem Env: | Where-Object Name -like "*SYNCFUSION*"

# Should show:
# SYNCFUSION_MCP_API_KEY    your-api-key-here

# If missing, set it:
[System.Environment]::SetEnvironmentVariable("SYNCFUSION_MCP_API_KEY", "your-key", "User")

# Restart IDE
```

**Get a new API key:**

1. Visit https://syncfusion.com/account/api-key
2. Login with Syncfusion account
3. Generate new key
4. Copy and set as environment variable

---

### Issue 3: "WileyWidget UI MCP failed to start"

**Cause:** Custom MCP server project not built

**Fix:**

```powershell
# Build the custom MCP server
dotnet build tools/SyncfusionMcpServer/tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj

# Test it runs
dotnet run --project tools/SyncfusionMcpServer/tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj --no-build
```

---

### Issue 4: "Filesystem MCP not working" in VS Code

**Cause:** MCP rules require activator calls first

**Fix:** Copilot must call these functions before file operations:

```javascript
activate_file_reading_tools(); // Before reads
activate_directory_and_file_creation_tools(); // Before writes
```

**Check:** Look in `logs/mcp-debugger.log` for activator calls

---

### Issue 5: Visual Studio `.vs/mcp.json` not recognized

**Cause:** Absolute paths may differ across machines

**Fix:** Update paths in `.vs/mcp.json` to match your system:

```powershell
# Get current workspace path
$workspacePath = (Get-Location).Path

# Replace in .vs/mcp.json
(Get-Content .vs/mcp.json) -replace 'C:\\Users\\biges\\Desktop\\Wiley-Widget', $workspacePath | Set-Content .vs/mcp.json
```

**Better:** Create a script to generate `.vs/mcp.json` dynamically:

```powershell
# scripts/generate-vs-mcp-config.ps1
$workspacePath = (Get-Location).Path -replace '\\', '\\'
$template = Get-Content .vscode/mcp.json -Raw
$vsConfig = $template -replace '\$\{workspaceFolder\}', $workspacePath
$vsConfig | Set-Content .vs/mcp.json
```

---

## 📊 Usage Examples

### Example 1: Query Syncfusion API

**VS Code:**

```
#SyncfusionWinFormsAssistant
How do I bind data to SfDataGrid in .NET 10?
```

**Visual Studio:**

```
@SyncfusionWinFormsAssistant
Show me how to implement paging in SfDataGrid
```

---

### Example 2: File Operations via MCP

**Copilot follows MCP rules automatically:**

```
Read the WarRoomPanel.cs file and analyze its databinding approach
```

**Behind the scenes:**

1. Copilot calls `activate_file_reading_tools()`
2. Calls `mcp_filesystem_read_text_file(path: "src/WileyWidget.WinForms/Controls/WarRoomPanel.cs")`
3. Returns file content in MCP-compliant format

---

### Example 3: Debug MCP Traffic

**Check logs:**

```powershell
Get-Content logs/mcp-debugger.log -Tail 50
```

**Look for:**

- Tool invocations: `filesystem/read_text_file`
- Parameter values: `{"path": "..."}`
- Errors: `EISDIR`, `ENOENT`, etc.

---

## 🔄 Syncing Configurations Across Machines

### Git Strategy

**Commit to version control:**

- ✅ `.vscode/mcp.json` (uses variables, safe to share)
- ❌ `.vs/mcp.json` (contains absolute paths, user-specific)

**Add to `.gitignore`:**

```gitignore
.vs/mcp.json
```

**Document in README:**

```markdown
## Setup for New Developers

1. Clone repository
2. Run setup script: `.\scripts\setup-mcp-servers.ps1`
3. Set Syncfusion API key: `$env:SYNCFUSION_MCP_API_KEY = "your-key"`
4. Restart IDE
```

---

## 📚 Additional Resources

### MCP Protocol Documentation

- Official Docs: https://modelcontextprotocol.io/docs
- GitHub: https://github.com/modelcontextprotocol

### Syncfusion MCP Server

- NPM: https://www.npmjs.com/package/@syncfusion/winforms-assistant
- Docs: https://help.syncfusion.com/windowsforms/ai-coding-assistant/mcp-server

### VS Code MCP Integration

- GitHub Copilot MCP: https://code.visualstudio.com/docs/copilot/customization/mcp-servers

### Visual Studio MCP Integration

- MCP in VS 2026: https://learn.microsoft.com/visualstudio/ide/mcp-servers

---

## 🎓 Best Practices

1. **Always set API keys via environment variables** (never hardcode)
2. **Use absolute paths in `.vs/mcp.json`** (Visual Studio doesn't support variables)
3. **Keep `.vscode/mcp.json` in version control** (portable across machines)
4. **Exclude `.vs/mcp.json` from git** (user-specific paths)
5. **Test MCP servers individually** before expecting Copilot to use them
6. **Monitor `logs/mcp-debugger.log`** to verify MCP traffic
7. **Document custom MCP servers** in project README

---

**This setup enables powerful AI-assisted development with specialized tools for Syncfusion WinForms components and adheres to the project's MCP enforcement rules.**
