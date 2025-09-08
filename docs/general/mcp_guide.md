# MCP (Model Context Protocol) Guide

## Overview

The Model Context Protocol (MCP) is an open protocol that standardizes how applications provide context to large language models (LLMs). Think of MCP like a USB-C port for AI applications - it provides a standardized way to connect AI models to different data sources and tools.

## VS Code MCP Integration

VS Code supports MCP through the `.vscode/mcp.json` configuration file, which allows you to connect various MCP servers to enhance AI assistant capabilities.

### Current MCP Configuration

Your current `.vscode/mcp.json` includes the following servers:

```json
{
  "servers": {
    "github": {
      "type": "stdio",
      "command": "npx",
      "args": ["@modelcontextprotocol/server-github"],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "${env:GITHUB_TOKEN}"
      }
    },
    "azure": {
      "command": "azmcp-win32-x64",
      "type": "stdio",
      "env": {
        "AZURE_CLIENT_ID": "${env:AZURE_CLIENT_ID}",
        "AZURE_CLIENT_SECRET": "${env:AZURE_CLIENT_SECRET}",
        "AZURE_TENANT_ID": "${env:AZURE_TENANT_ID}",
        "AZURE_SUBSCRIPTION_ID": "${env:AZURE_SUBSCRIPTION_ID}"
      }
    },
    "microsoft-docs": {
      "url": "https://learn.microsoft.com/mcp/",
      "type": "http"
    }
  },
  "inputs": []
}
```

## Setup Methods

### 1. Installing MCP Servers

#### Official MCP Servers (Recommended)
Use the official MCP servers from the [Model Context Protocol Servers Repository](https://github.com/modelcontextprotocol/servers):

**TypeScript/Node.js Servers:**
```bash
npx -y @modelcontextprotocol/server-filesystem
npx -y @modelcontextprotocol/server-github
npx -y @modelcontextprotocol/server-git
```

**Python Servers:**
```bash
# Using uvx (recommended)
uvx mcp-server-git
uvx mcp-server-sqlite

# Using pip
pip install mcp-server-git
pip install mcp-server-sqlite
```

#### Community MCP Servers
Install from npm or pip as specified in their documentation:

```bash
# Azure MCP
# Download from: https://github.com/Azure/azure-mcp
```

### 2. Configuring VS Code MCP

1. **Create/Edit `.vscode/mcp.json`** in your workspace root
2. **Add server configurations** following the format above
3. **Set environment variables** for authentication tokens
4. **Restart VS Code** to load the new configuration

### 3. Environment Variables Setup

Create a `.env` file or set system environment variables:

```bash
# GitHub Token
GITHUB_TOKEN=your_github_personal_access_token

# Azure Credentials
AZURE_CLIENT_ID=your_client_id
AZURE_CLIENT_SECRET=your_client_secret
AZURE_TENANT_ID=your_tenant_id
AZURE_SUBSCRIPTION_ID=your_subscription_id
```

## Server Types

### 1. STDIO Servers
- **Type**: `"stdio"`
- **Description**: Command-line servers that communicate via standard input/output
- **Examples**: Most official MCP servers, GitHub, filesystem servers

### 2. HTTP/SSE Servers
- **Type**: `"http"` or `"sse"`
- **Description**: Web-based servers using HTTP or Server-Sent Events
- **Examples**: Microsoft Docs, remote MCP servers

### 3. WebSocket Servers
- **Type**: `"ws"`
- **Description**: Real-time communication servers
- **Examples**: Some community servers

## Documentation Resources

### Official Resources
- **[MCP Specification](https://modelcontextprotocol.io/specification/)** - Complete protocol documentation
- **[MCP Servers Repository](https://github.com/modelcontextprotocol/servers)** - Official server implementations
- **[MCP SDKs](https://modelcontextprotocol.io/docs/sdk)** - SDKs for building custom servers
- **[Getting Started Guide](https://modelcontextprotocol.io/quickstart/user)** - Step-by-step setup for Claude Desktop

### VS Code Specific
- **[VS Code MCP Documentation](https://code.visualstudio.com/docs/copilot/chat/mcp)** - Official VS Code MCP integration docs
- **[GitHub Copilot MCP](https://github.com/microsoft/vscode-copilot-mcp)** - VS Code Copilot MCP implementation

### Community Resources
- **[Awesome MCP Servers](https://github.com/punkpeye/awesome-mcp-servers)** - Curated list of MCP servers
- **[MCP Servers Hub](https://mcpservers.com/)** - Directory with setup guides
- **[Smithery](https://smithery.ai/)** - Registry of MCP servers
- **[PulseMCP](https://www.pulsemcp.com/)** - Community hub and newsletter

### Learning Resources
- **[MCP Tutorials](https://modelcontextprotocol.io/docs/tutorials/)** - Step-by-step tutorials
- **[Discord Community](https://discord.gg/jHEGxQu2a5)** - Active community support
- **[Reddit r/mcp](https://www.reddit.com/r/mcp/)** - Community discussions

## Troubleshooting Tips

### Common Issues

#### 1. Server Not Loading
**Symptoms**: MCP server doesn't appear in VS Code
**Solutions**:
- Check `.vscode/mcp.json` syntax (validate JSON)
- Ensure server command is correct and executable
- Verify environment variables are set
- Restart VS Code completely
- Check VS Code output panel for MCP errors

#### 2. Authentication Errors
**Symptoms**: Server fails with auth-related errors
**Solutions**:
- Verify environment variables are set correctly
- Check token/API key validity and permissions
- Ensure tokens have required scopes
- Test authentication outside VS Code first

#### 3. Command Not Found
**Symptoms**: `npx` or server command not found
**Solutions**:
- Install Node.js (for npx servers)
- Install Python (for uvx/pip servers)
- Add server executables to PATH
- Use full paths in configuration

#### 4. Permission Errors
**Symptoms**: Server can't access files or resources
**Solutions**:
- Check file/directory permissions
- Run VS Code with appropriate permissions
- Configure server with correct paths
- Use absolute paths in server arguments

### Debugging Steps

1. **Check VS Code Output**:
   - Open Command Palette (`Ctrl+Shift+P`)
   - Select "Developer: Show Logs"
   - Look for MCP-related errors

2. **Test Server Independently**:
   ```bash
   # Test GitHub server
   npx @modelcontextprotocol/server-github

   # Test filesystem server
   npx @modelcontextprotocol/server-filesystem /path/to/test
   ```

3. **Validate Configuration**:
   ```bash
   # Check JSON syntax
   python -m json.tool .vscode/mcp.json

   # Test environment variables
   echo $GITHUB_TOKEN
   ```

4. **Check VS Code Version**:
   - Ensure VS Code is updated to latest version
   - Check Copilot extension is current

### Performance Issues

- **Large repositories**: Limit filesystem server to specific directories
- **Many servers**: Disable unused servers to improve performance
- **Network timeouts**: Increase timeout values in server configuration
- **Memory usage**: Monitor VS Code memory usage with many active servers

## Current Server Details

### GitHub Server
- **Purpose**: Access GitHub repositories, issues, pull requests
- **Requirements**: GitHub Personal Access Token with repo access
- **Capabilities**: Repository management, issue tracking, code search

### Azure Server
- **Purpose**: Azure cloud resource management
- **Requirements**: Azure service principal credentials
- **Capabilities**: Resource management, deployment, monitoring

### Microsoft Docs Server
- **Purpose**: Access Microsoft documentation
- **Type**: HTTP-based remote server
- **Capabilities**: Documentation search and retrieval

### Bright Data Server
- **Purpose**: Web scraping and data extraction
- **Requirements**: Bright Data API token
- **Capabilities**: Web crawling, data extraction, geo-targeted scraping

## Best Practices

### Security
- Use environment variables for sensitive credentials
- Limit server access to necessary directories
- Regularly rotate API tokens and keys
- Review server permissions periodically

### Performance
- Only enable servers you actively use
- Configure servers with specific paths rather than broad access
- Monitor VS Code performance with multiple servers
- Use remote servers when possible to reduce local resource usage

### Organization
- Group related servers in configuration
- Use descriptive server names
- Document server purposes and requirements
- Keep configuration file well-commented

### Maintenance
- Regularly update MCP servers to latest versions
- Monitor for deprecated servers or APIs
- Test server functionality after VS Code updates
- Backup working configurations

## Advanced Configuration

### Custom Server Development
- Use official MCP SDKs for your preferred language
- Follow the MCP specification for compatibility
- Test servers with multiple MCP clients
- Contribute to the community repository

### Remote MCP Servers
- Host servers on cloud infrastructure
- Use authentication and authorization
- Implement proper error handling and logging
- Monitor server performance and usage

### Integration with CI/CD
- Automate MCP server deployment
- Use configuration management tools
- Implement proper testing for MCP integrations
- Monitor MCP server health in production

## Support and Community

- **GitHub Issues**: Report bugs and request features
- **Discord**: Real-time community support
- **Reddit**: Community discussions and troubleshooting
- **Official Documentation**: Comprehensive guides and tutorials

---

*This guide is maintained as part of the Wiley Widget project documentation discipline system. For the latest updates, refer to the official MCP documentation and community resources.*
