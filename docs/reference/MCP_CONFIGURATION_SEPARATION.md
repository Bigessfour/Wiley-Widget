# MCP Configuration Guide - Separated Workflows

This document explains the two distinct MCP (Model Context Protocol) configurations in this workspace and how they serve different development workflows.

## Overview

There are **two separate MCP implementations** configured for different purposes:

### 1. GitHub Copilot MCP Integration

**Purpose**: AI-assisted development and Copilot Chat interactions
**Configuration**: Top-level settings in `.vscode/settings.json`
**Tools**: `mcp.preferredTool`, `mcp.wrapperScript`, etc.

```json
{
  "mcp.preferredTool": "./tools/mcp-csharp.exe",
  "mcp.wrapperScript": "${workspaceFolder}/scripts/tools/run-mcp.ps1",
  "mcp.defaultServer": "csharp-mcp",
  "mcp.autoRunOnSave": false
}
```

**Usage**:

- Used by GitHub Copilot for AI-assisted coding
- Provides context and tools for Copilot Chat
- Integrated with VS Code's AI features

### 2. MCP Companion Extension

**Purpose**: VS Code extension for MCP server management
**Configuration**: `mcp.servers` section in `.vscode/settings.json`
**Tools**: Docker-based MCP servers

```json
{
  "mcp": {
    "servers": {
      "csharp-mcp-docker": {
        "type": "stdio",
        "command": "docker",
        "args": ["run", "...", "ghcr.io/infinityflowapp/csharp-mcp:latest"]
      }
    }
  }
}
```

**Usage**:

- Standalone MCP server management in VS Code
- Independent of Copilot integration
- Can be used for testing and development workflows

## Development Workflows

### For AI-Assisted Development (Copilot)

1. Use Copilot Chat with MCP context
2. The `mcp.preferredTool` and wrapper script provide AI assistance
3. Integrated with GitHub Copilot features

### For MCP Server Testing (Standalone)

1. Use MCP Companion extension
2. Docker-based servers for isolated testing
3. Independent of Copilot integration

## File Structure

```
.vscode/settings.json          # Both configurations
.mcp/config.json               # Legacy MCP config (may be deprecated)
scripts/tools/run-mcp.ps1     # Wrapper for Copilot integration
tools/mcp-csharp.exe          # Copilot MCP tool (if present)
```

## Troubleshooting

### MCP Companion Extension Issues

- Check Docker is running
- Verify `ghcr.io/infinityflowapp/csharp-mcp:latest` image is accessible
- Check VS Code MCP Companion extension logs

### Copilot MCP Integration Issues

- Verify `./tools/mcp-csharp.exe` exists or fallback to wrapper script
- Check `scripts/tools/run-mcp.ps1` for installation guidance
- Ensure proper tool permissions

## Future Considerations

Consider consolidating to a single MCP approach as the ecosystem matures. The current separation allows for:

- Independent testing of MCP features
- Gradual migration between implementations
- Maximum flexibility during development</content>
  <parameter name="filePath">c:\Users\biges\Desktop\Wiley_Widget\docs\MCP_CONFIGURATION_SEPARATION.md
