# Bright Data Hosted MCP Usage

## Modes
- REST: direct HTTPS POST
- MCP: local @brightdata/mcp spawn
- HOSTED: hosted Bright Data MCP (adds --hosted automatically)
- AUTO: MCP first then REST fallback per query

## Quick Start (Hosted)
```
# .env
BRIGHTDATA_API_KEY=REPLACE_WITH_REAL_KEY
BRIGHTDATA_MODE=HOSTED
BRIGHTDATA_MCP_DEBUG=1
BRIGHTDATA_MCP_TIMEOUT=25000
```
Run:
```
node ./brightdata-startup-diagnostics.js
```

If npx is missing, optionally set:
```
BRIGHTDATA_MCP_CMD=node
BRIGHTDATA_MCP_ARGS=["node_modules/@brightdata/mcp/dist/index.js"]
```

## Troubleshooting
- "Failed to spawn MCP": ensure either npx in PATH or direct module path exists.
- Tool not found: set BRIGHTDATA_MCP_TOOL to the server-advertised tool name.
- Timeout: increase BRIGHTDATA_MCP_TIMEOUT (ms).
