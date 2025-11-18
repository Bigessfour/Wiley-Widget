Continue.dev MCP Agent Setup for Wiley Widget

Summary

- This document explains how to configure Continue.dev to run MCP servers so the agent can read/edit files and run terminal commands similar to a Copilot-like agent.

Important security note

- DO NOT commit API keys or secrets into the repository. Keep them in environment variables or OS key stores. The `.continue/config.json` in this repository has been sanitized to reference environment variables instead of embedded API keys.

Prerequisites

- Node.js and npm installed
- PowerShell (pwsh) available (Windows default)
- `npx` available via npm
- Administrator or user permission to install global npm packages (or prefer `npx` per-run installs)

Steps

1. Set your xAI (Grok) API key in environment variables (PowerShell):

```powershell
# For current session (temporary)
$env:XAI_API_KEY = "xai-<your-api-key-here>"

# To persist for the current user (PowerShell 7+, persisted):
setx XAI_API_KEY "xai-<your-api-key-here>"
# Close and re-open terminals/VS Code after using setx
```

2. Install and/or start MCP servers (script provided):

```powershell
# Run the helper script which uses npm to install MCP servers
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup-mcp-servers.ps1

# OR run the commands manually if you prefer not to install globally
npx -y @modelcontextprotocol/server-filesystem .
npx -y @modelcontextprotocol/server-git --repository .
npx -y @modelcontextprotocol/server-everything .
```

3. Restart VS Code and the Continue.dev extension so it picks up the new MCP server settings from `.continue/config.json`.

4. In Continue.dev, verify the agent can access the workspace, run a simple command, and propose edits. Start small (e.g., ask it to "list files under src/") and confirm expected behaviors.

If you see linter/Trunk warnings about secrets

- That means some file contains a high-entropy string that looks like a secret. Use the environment variable approach shown above and remove any direct `apiKey` values from files. The repo's `.continue/config.json` has been updated to use `apiKeyEnv`.

Troubleshooting

- If Continue.dev still complains it cannot execute commands, verify MCP servers are running and that Continue.dev is configured to use them (see `.continue/config.json -> mcpServers`).
- If permissions prevent `npm install -g`, use `npx` per-run as shown above.

Notes

- The MCP servers expose powerful capabilities; only enable them in trusted repositories and on developer machines you control.
- The `scripts/setup-mcp-servers.ps1` script is idempotent and uses `Write-Information` to satisfy PowerShell analyzers.

Done — after these steps the Continue.dev agent will have filesystem, git, and command-execution capabilities similar to an editor-integrated agent.
