# Syncfusion Windows Forms MCP Assistant

An MCP (Model Context Protocol) server that provides intelligent assistance for developing Windows Forms applications using Syncfusion components.

## Overview

The Syncfusion Windows Forms Assistant is an official npm package that integrates with MCP-compatible clients (like VS Code with GitHub Copilot) to provide:

- **Intelligent code generation** for Syncfusion Windows Forms components
- **Detailed documentation** and usage examples
- **Troubleshooting assistance** for common integration challenges
- **Unlimited access** with no restrictions on requests, components, or usage

## Prerequisites

- **Node.js** >= 18 ([Download](https://nodejs.org/))
- **Active Syncfusion License** (Commercial, Community, or Trial)
- **Syncfusion API Key** ([Get yours](https://syncfusion.com/account/api-key))
- **MCP-compatible client** (VS Code with GitHub Copilot, Cursor, etc.)

## Quick Setup

### 1. Run the Setup Script

```powershell
.\scripts\tools\setup-syncfusion-mcp.ps1
```

The script will:

- ✓ Validate Node.js installation (>= v18)
- ✓ Check npm package availability
- ✓ Configure API key environment variable
- ✓ Verify MCP configuration

### 2. Set Your API Key

If you haven't set your API key yet:

```powershell
# Option 1: Use the setup script interactively
.\scripts\tools\setup-syncfusion-mcp.ps1

# Option 2: Provide API key directly
.\scripts\tools\setup-syncfusion-mcp.ps1 -ApiKey "your-api-key-here"

# Option 3: Set environment variable manually
$env:SYNCFUSION_API_KEY = "your-api-key-here"
[Environment]::SetEnvironmentVariable("SYNCFUSION_API_KEY", "your-api-key-here", "User")
```

### 3. Restart VS Code

After setting the API key, restart VS Code for changes to take effect.

## Usage

### In VS Code with GitHub Copilot

The MCP server is configured in [.vscode/mcp.json](./.vscode/mcp.json) and will start automatically when invoked.

#### Activation Methods

Use any of these prefixes in your prompts:

- `#SyncfusionWinFormsAssistant`
- `SyncfusionWinFormsAssistant`
- `/syncfusion-winforms-assistant`
- `/syncfusion-winforms`
- `@syncfusion-winforms`
- `@ask_syncfusion_winforms`
- `winforms`

#### Example Queries

**Component Creation:**

```
#SyncfusionWinFormsAssistant Create a DataGrid component with paging, sorting, and filtering
```

**Data Binding:**

```
How do I implement data binding with Syncfusion Windows Forms Scheduler?
```

**Configuration:**

```
@syncfusion-winforms Show me how to configure a Chart control with multiple series
```

**Troubleshooting:**

```
SyncfusionWinFormsAssistant Why is my RibbonControl not showing themes correctly?
```

### Best Practices

1. **Be Specific**: Mention both platform and component
   - ✓ "Create a Syncfusion Windows Forms DataGrid with paging"
   - ✗ "Create a grid"

2. **Provide Context**: Include details about your use case
   - ✓ "I need a chart to display monthly sales data with drill-down"
   - ✗ "How do I make a chart?"

3. **Use Descriptive Queries**: Avoid vague questions
   - ✓ "How do I bind a DataGrid to an Entity Framework DbSet?"
   - ✗ "How do I bind data?"

4. **Start Fresh**: Begin a new chat session when switching topics
   - Helps maintain clean context
   - Provides more accurate responses

## Configuration

The MCP server is configured in `.vscode/mcp.json`:

```json
{
  "servers": {
    "syncfusion-winforms-assistant": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@syncfusion/winforms-assistant@latest"],
      "env": {
        "Syncfusion_API_Key": "${env:SYNCFUSION_API_KEY}"
      },
      "autoStart": false
    }
  }
}
```

### Configuration Options

- **type**: `stdio` - Standard input/output transport
- **command**: `npx` - Uses npx to run the package
- **args**: `-y @syncfusion/winforms-assistant@latest` - Auto-accept and use latest version
- **env**: Environment variables (requires `SYNCFUSION_API_KEY`)
- **autoStart**: `false` - Start only when invoked (recommended)

## Validation

Check your setup:

```powershell
# Validate setup
.\scripts\tools\setup-syncfusion-mcp.ps1 -ValidateOnly

# Check Node.js version
node --version  # Should be >= v18

# Verify npm package
npm view @syncfusion/winforms-assistant version

# Check API key
$env:SYNCFUSION_API_KEY
```

## Troubleshooting

### Server Not Starting

1. **Check Node.js version**

   ```powershell
   node --version  # Must be >= v18
   ```

2. **Verify API key is set**

   ```powershell
   $env:SYNCFUSION_API_KEY
   ```

3. **Test npm package**

   ```powershell
   npx -y @syncfusion/winforms-assistant@latest --help
   ```

### API Key Issues

- Ensure your API key is valid at [Syncfusion Account](https://syncfusion.com/account/api-key)
- Check that you have an active license (Commercial, Community, or Trial)
- Verify the environment variable is set for the User scope

### Connection Issues

- Restart VS Code after setting environment variables
- Check VS Code's MCP output panel for errors
- Ensure your firewall allows Node.js network access

## Features

### Component Code Generation

Generate initialization code for:

- DataGrid (sorting, filtering, grouping)
- Charts (line, bar, pie, etc.)
- Ribbon Controls
- Buttons and UI elements
- And 100+ other Syncfusion components

### Documentation Access

- Quick access to component documentation
- Usage examples and best practices
- Common patterns and scenarios

### Troubleshooting Support

- Common integration issues
- Configuration problems
- Performance optimization tips

## Package Information

- **Package Name**: `@syncfusion/winforms-assistant`
- **NPM Registry**: [npmjs.com](https://www.npmjs.com/package/@syncfusion/winforms-assistant)
- **Official Docs**: [Syncfusion MCP Documentation](https://help.syncfusion.com/windowsforms/ai-coding-assistant/mcp-server)

## License Requirements

To use the Syncfusion Windows Forms MCP Assistant, you need one of:

- [Commercial License](https://www.syncfusion.com/sales/unlimitedlicense)
- [Free Community License](https://www.syncfusion.com/products/communitylicense)
- [Free Trial](https://www.syncfusion.com/account/manage-trials/start-trials)

Register your license key and get your API key at [Syncfusion Account](https://syncfusion.com/account/api-key).

## Unlimited Access

✓ No request limits
✓ No component restrictions
✓ No query type limitations
✓ No usage duration limits

## Support

- [Syncfusion Support Tickets](https://support.syncfusion.com/support/tickets/create) - Guaranteed response in 24 hours
- [Community Forum](https://www.syncfusion.com/forums/windowsforms)
- [Report Bug or Feature Request](https://www.syncfusion.com/feedback/winforms)
- [Windows Forms Documentation](https://help.syncfusion.com/windowsforms/overview)

## Related Scripts

- [setup-syncfusion-mcp.ps1](../../scripts/tools/setup-syncfusion-mcp.ps1) - Setup and validation script
- [init-mcp-servers.ps1](../../scripts/setup/init-mcp-servers.ps1) - Initialize all MCP servers

## See Also

- [MCP Configuration](./.vscode/mcp.json)
- [Syncfusion AI Coding Assistant Overview](https://help.syncfusion.com/windowsforms/ai-coding-assistant/overview)
- [Model Context Protocol](https://modelcontextprotocol.io/)
