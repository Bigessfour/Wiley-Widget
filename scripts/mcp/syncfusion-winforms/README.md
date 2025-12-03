# Syncfusion WinForms MCP (Local Stdio)

Minimal MCP server with a single purpose tool:

- Tool `fetch-syncfusion-docs`: Fetches Syncfusion WinForms documentation by direct URL, or by control name (mapped to its overview URL).

## Install & Run

```powershell
# From repo root
Push-Location .\scripts\mcp\syncfusion-winforms
npm install
npm start
# (for MCP clients, VS Code will spawn this via stdio using node path below)
Pop-Location
```

## VS Code MCP Config
- Registered in `.vscode/mcp.json` and `.vscode/settings.json` as `syncfusion-winforms` with `command: node` and entry `scripts/mcp/syncfusion-winforms/src/index.mjs`.
- Uses stdio transport; no ports exposed.

## Usage examples (Copilot Chat)
- By control name: `Call tool syncfusion-winforms:fetch-syncfusion-docs { control: "SfDataGrid" }`
- By URL: `Call tool syncfusion-winforms:fetch-syncfusion-docs { url: "https://help.syncfusion.com/windowsforms/datagrid/overview" }`
- Limit content size: `Call tool syncfusion-winforms:fetch-syncfusion-docs { control: "SfChart", maxChars: 20000 }`

## Extend
- Add or adjust the small control mapping in `src/index.mjs`.
- For richer parsing, replace the naive HTML->text conversion with a proper HTML reader.
