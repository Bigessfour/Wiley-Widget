# MCP Tools & Validation Integration

This project uses **SyncfusionMcpServer v1.1** (via git submodule) for headless form validation, theme compliance, and AI-powered remediation.

## SyncfusionMcpServer

**Location:** `tools/SyncfusionMcpServer/` (git submodule)
**Repository:** https://github.com/Bigessfour/syncfusion-winforms-mcp
**Version:** 1.1.0
**License:** MIT

### Features

- **🎨 Theme Validation** - Enforce Office2019Colorful/Office2016 themes
- **🔍 Manual Color Detection** - Find hardcoded colors bypassing SkinManager
- **📊 Grid Inspection** - Analyze SfDataGrid columns, bindings, and data
- **🏗️ Control Hierarchy Export** - Export form structure as JSON/text
- **⚡ Headless Testing** - Instantiate forms without UI rendering
- **🧪 C# Eval** - Execute dynamic C# code with full WinForms context
- **📦 Batch Validation** - Validate dozens of forms in one operation
- **🤖 AI Remediation** - xAI Grok-4 integration for fix suggestions

### Getting Started

```bash
cd tools/SyncfusionMcpServer
dotnet run --project tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj
```

### Running Tests

```bash
cd tools/SyncfusionMcpServer
npm run test:unit
```

### Documentation

- [Full README](tools/SyncfusionMcpServer/README.md)
- [CHANGELOG](tools/SyncfusionMcpServer/CHANGELOG.md)
- [Quick Start Guide](tools/SyncfusionMcpServer/QUICK_START.md)
- [Implementation Status](tools/SyncfusionMcpServer/MCP_IMPLEMENTATION_STATUS.md)

### Updating the Submodule

To get the latest version of SyncfusionMcpServer:

```bash
git submodule update --remote
```

### Why Submodule?

The MCP server is maintained as a separate project and integrated here as a git submodule because:

1. **Separation of Concerns** - MCP tools are independent of wiley-widget business logic
2. **Reusability** - The server can be used in other .NET projects
3. **Version Control** - Each wiley-widget commit pins a specific MCP server version
4. **Easy Updates** - Update independently or sync when needed
