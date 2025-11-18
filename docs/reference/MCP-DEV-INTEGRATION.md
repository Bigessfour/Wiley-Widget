# MCP Development Integration (workspace defaults)

This document explains how the repository is configured so the C# MCP tool (Model Context Protocol) is the preferred developer tool for C# diagnostics and automated fixes.

## Overview

- A small wrapper script is provided at `scripts/tools/run-mcp.ps1` which will run a bundled MCP binary found at `./tools/mcp-csharp.exe` (if present). If the binary is not present the script prints installation guidance and exits successfully.
- A default VS Code build task (`mcp: analyze`) runs the wrapper script. Press `Ctrl+Shift+B` to run it in VS Code.
- `assistant-preferences.yaml` documents the workspace preference for assistants and automation.

## Quick start (developer)

1. Clone the repo.
2. (Optional) If the team provides a pinned MCP tool, place it at `./tools/mcp-csharp.exe` or install locally with:

```powershell
dotnet tool install --local mcp-csharp --version 1.2.3
```

3. Run the wrapper (safe even if you haven't installed MCP):

```powershell
pwsh ./scripts/tools/run-mcp.ps1 -Project WileyWidget.csproj
```

4. In VS Code press `Ctrl+Shift+B` to run the default build task (the MCP analyzer).

## Making MCP the default for automation

- The wrapper script, tasks and `assistant-preferences.yaml` provide a consistent entry point.
- To enforce MCP checks on PRs, add a CI step that runs `pwsh ./scripts/tools/run-mcp.ps1` and fails on non-zero exit codes (when the tool is installed).

## Notes and best practices

- Pin the MCP version in a `dotnet-tools.json` manifest (or store the binary in `tools/`) so all developers use the same version.
- Ensure MCP output is either compatible with VS Code problem matchers or the wrapper can emit a JSON diagnostics file that CI/agents parse.
- Never commit real secrets. Use the repo's `secrets/` or local vaulting approach when executing analyzers that may read config files.
