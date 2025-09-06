# Model Context Protocol (MCP) Configuration Guide

## Overview

This guide documents the Model Context Protocol (MCP) servers configured for the Wiley Widget project, enabling seamless integration with GitHub, Azure, and Microsoft documentation services.

## Current MCP Server Configuration

### GitHub MCP Server
**Purpose**: Repository management, workflow monitoring, and code analysis
```json
{
  "github": {
    "url": "https://api.githubcopilot.com/mcp/",
    "type": "http",
    "headers": {
      "Authorization": "Bearer ${env:GITHUB_PERSONAL_ACCESS_TOKEN}"
    }
  }
}
```

**Capabilities**:
- Repository operations (create, update, delete)
- Workflow monitoring and status checks
- Pull request management
- Issue tracking and management
- Code search and analysis

### Azure MCP Server
**Purpose**: Cloud resource management and Azure service operations
```json
{
  "azure": {
    "command": "azure-mcp-server",
    "type": "stdio",
    "env": {
      "AZURE_CLIENT_ID": "${env:AZURE_CLIENT_ID}",
      "AZURE_CLIENT_SECRET": "${env:AZURE_CLIENT_SECRET}",
      "AZURE_TENANT_ID": "${env:AZURE_TENANT_ID}",
      "AZURE_SUBSCRIPTION_ID": "${env:AZURE_SUBSCRIPTION_ID}"
    }
  }
}
```

**Capabilities**:
- Resource group management
- SQL Database operations
- Managed Identity configuration
- Resource monitoring and diagnostics
- Security and access management

### Microsoft Docs MCP Server
**Purpose**: Technical documentation and troubleshooting reference
```json
{
  "microsoft-docs": {
    "url": "https://mcp.microsoft.com/docs/",
    "type": "http"
  }
}
```

**Capabilities**:
- .NET documentation access
- Azure service documentation
- CI/CD pipeline documentation
- Troubleshooting guides
- Best practices and patterns

### Bright Data MCP Server
**Purpose**: Web data collection, search, and market research
```json
{
  "brightdata": {
    "url": "https://api.brightdata.com/mcp/",
    "type": "http",
    "headers": {
      "Authorization": "Bearer ${env:BRIGHTDATA_API_KEY}",
      "Content-Type": "application/json"
    }
  }
}
```

**Capabilities**:
- Web search and scraping
- Data extraction from websites
- Market research and competitive analysis
- Real-time data collection
- Structured data parsing

## Configuration Files

### Workspace MCP Configuration
**Location**: `.vscode/mcp.json`
- Contains server definitions for the current workspace
- Environment variable substitution supported
- Automatically loaded by VS Code MCP extension

### VS Code Settings Integration
**Location**: `.vscode/settings.json`
- MCP server configurations integrated into VS Code settings
- Supports multiple server types (HTTP, stdio, WebSocket)
- Environment variable resolution for secure credential management

## Environment Variables Required

### GitHub MCP
```bash
GITHUB_PERSONAL_ACCESS_TOKEN=your_github_token_here
```

### Azure MCP
```bash
AZURE_CLIENT_ID=your_client_id
AZURE_CLIENT_SECRET=your_client_secret
AZURE_TENANT_ID=your_tenant_id
AZURE_SUBSCRIPTION_ID=your_subscription_id
```

### Bright Data MCP
```bash
BRIGHTDATA_API_KEY=your_brightdata_api_key
```

## Usage Examples

### Querying GitHub Workflows
```javascript
// Get latest workflow runs
mcp_github_list_workflow_runs({
  owner: "Bigessfour",
  repo: "Wiley-Widget",
  workflow_id: "ci-optimized.yml"
})
```

### Azure Resource Management
```javascript
// List Azure resources
azure_resources-query_azure_resource_graph({
  arg_intent: "list all resources in wileywidget-rg",
  useDefaultSubscriptionFilter: true
})
```

### Documentation Reference
```javascript
// Search Microsoft Docs
microsoft_docs-search({
  query: ".NET dependency injection best practices",
  category: "dotnet"
})
```

## Troubleshooting

### Common Issues

1. **MCP Server Connection Failed**
   - Verify environment variables are set
   - Check Azure CLI authentication status
   - Ensure GitHub token has proper permissions

2. **Authentication Errors**
   - Refresh Azure CLI login: `az login`
   - Regenerate GitHub token with required scopes
   - Verify tenant and subscription IDs

3. **VS Code Extension Issues**
   - Reload VS Code window
   - Check MCP extension is enabled
   - Verify configuration file syntax

### Health Checks

```powershell
# Test MCP server connectivity
.\scripts\test-mcp-connectivity.ps1

# Verify Azure MCP authentication
.\scripts\azure-setup.ps1 -TestConnection

# Check GitHub MCP token validity
.\scripts\github-setup.ps1 -VerifyToken
```

## Security Considerations

- **Credential Management**: All sensitive credentials stored as environment variables
- **Token Rotation**: Regularly rotate GitHub tokens and Azure service principals
- **Access Control**: MCP servers respect Azure RBAC and GitHub permissions
- **Audit Logging**: All MCP operations are logged for security monitoring

## Integration with CI/CD

MCP servers are integrated with the CI/CD pipeline for:
- Automated resource validation
- Security scanning integration
- Documentation updates
- Compliance monitoring

## Support and Resources

- **MCP Documentation**: https://modelcontextprotocol.io/
- **Azure MCP Server**: https://github.com/microsoft/azure-mcp
- **GitHub MCP**: https://docs.github.com/en/copilot
- **Microsoft Docs MCP**: https://learn.microsoft.com/
- **Bright Data Documentation**: https://docs.brightdata.com/

---

**Last Updated**: Current configuration reflects Azure resources in East US region with active MCP server integration.
