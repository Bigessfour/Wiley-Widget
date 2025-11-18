# Workspace tools and MCP

Place workspace-specific helper tools here.

Recommended:
- Put `mcp-csharp.exe` (pinned version) at `./tools/mcp-csharp.exe` so the workspace wrapper (`scripts/tools/run-mcp.ps1`) will pick it up.
- Install PSScriptAnalyzer locally for the user:

```powershell
Install-Module -Name PSScriptAnalyzer -Scope CurrentUser -Force
```

This workspace enforces PowerShell 7.5.2 or newer for running scripts and uses `scripts/ps-runner.ps1` to analyze scripts prior to execution.
