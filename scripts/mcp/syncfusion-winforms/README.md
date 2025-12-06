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

### New tools: list and implement
- List available curated controls: `Call tool syncfusion-winforms:list-syncfusion-controls {}`
- Implement one control (C# WinForms):
  `Call tool syncfusion-winforms:implement-syncfusion-control { "control": "SfDataGrid" }`
- Implement all known curated controls (returns production-ready snippets and notes):
  `Call tool syncfusion-winforms:implement-syncfusion-control { "all": true }`

These tools are intended to be used in the AI chat window. For example, in GitHub Copilot Chat you could say: `@syncfusion-winforms implement all controls and create production-ready C# WinForms snippets for each control` and the tool will return a structured set of implementations.

For true production use make sure to validate code, include Syncfusion licensing initialization, and install the appropriate Syncfusion NuGet packages in your project. We recommend auditing and testing the generated snippets in CI before merging into a production codebase.

## Extend
- Add or adjust the small control mapping in `src/index.mjs`.
- For richer parsing, replace the naive HTML->text conversion with a proper HTML reader.

## Notes / Workarounds
There is a known packaging issue in the published `@syncfusion/winforms-assistant` package where the compiled CLI imports `zod` but the published package.json did not declare `zod` as a dependency. This can cause an `ERR_MODULE_NOT_FOUND: Cannot find package 'zod'` when running the package directly via `npx`.

To make this MCP project robust locally, this repository adds a postinstall script that ensures `zod` is declared and installed for the local `@syncfusion/winforms-assistant` package inside `node_modules`.

If you're consuming the published package directly via `npx` outside this repo, please report the upstream packaging issue to the package maintainer or install `zod` in the environment where you run `npx` as a temporary workaround.

(If a permanent fix is needed, the authoritative fix is to add `zod` to the published package's `dependencies` and publish a new version.)
