# Workspace tools and MCP

Place workspace-specific helper tools here.

Recommended:
- Put `mcp-csharp.exe` (pinned version) at `./tools/mcp-csharp.exe` so the workspace wrapper (`scripts/tools/run-mcp.ps1`) will pick it up.
- Install PSScriptAnalyzer locally for the user:

```powershell
Install-Module -Name PSScriptAnalyzer -Scope CurrentUser -Force
```

This workspace enforces PowerShell 7.5.2 or newer for running scripts and uses `scripts/ps-runner.ps1` to analyze scripts prior to execution.

## New reliability helpers
We've added a few tools to make file I/O and diagnostics more robust when running automation from the agent:

- `atomic_write.py` — atomic write helper with retry/backoff (use this from Python scripts when writing files that may be concurrently accessed).
- `atomic_write.ps1` — same helper for PowerShell scripts.
- `mcp_write_test_atomic.py` — stress test that uses `atomic_write` to exercise repeated writes.
- `collect_file_lock_info.ps1` — diagnostics helper that enumerates likely processes and uses Sysinternals `handle.exe` if available to find lock owners.

When the agent (or MCP) must write to files frequently, prefer `atomic_write` to reduce transient EPERM/rename failures.
