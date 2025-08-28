# GitHub MCP Server Setup for WileyWidget

## Overview
This document describes the GitHub Model Context Protocol (MCP) server setup for the WileyWidget project, including both local and remote server configurations.

## Configuration Options

### Environment Variables
The GitHub MCP server uses the following environment variables:
- `GITHUB_PERSONAL_ACCESS_TOKEN`: Your GitHub Personal Access Token (updated in .env)
- `GITHUB_API_URL`: GitHub API endpoint (default: https://api.github.com)
- `GITHUB_REPOSITORY`: Target repository (Bigessfour/Wiley-Widget)
- `GITHUB_BASE_URL`: GitHub base URL for GitHub Enterprise (optional)
- `GITHUB_API_VERSION`: API version to use (default: 2022-11-28)
- `GITHUB_REQUEST_TIMEOUT`: Request timeout in milliseconds (default: 30000)
- `GITHUB_MAX_RETRIES`: Maximum number of retries for failed requests (default: 3)
- `GITHUB_RETRY_DELAY`: Delay between retries in milliseconds (default: 1000)

## Server Configuration Types

### Local Server Setup (Current)
The MCP server is configured in `.vscode/settings.json`:

```json
{
  "mcp": {
    "servers": {
      "github": {
        "command": "npx",
        "args": ["-y", "@modelcontextprotocol/server-github"],
        "env": {
          "GITHUB_PERSONAL_ACCESS_TOKEN": "${env:GITHUB_PERSONAL_ACCESS_TOKEN}"
        }
      }
    }
  }
}
```

### Remote Server Setup

#### Option 1: HTTP/HTTPS Remote Server
For production deployments, configure a remote MCP server:

```json
{
  "mcp": {
    "servers": {
      "github": {
        "url": "https://your-mcp-server.example.com/github",
        "headers": {
          "Authorization": "Bearer ${env:GITHUB_PERSONAL_ACCESS_TOKEN}",
          "X-API-Key": "${env:MCP_API_KEY}"
        },
        "timeout": 30000
      }
    }
  }
}
```

#### Option 2: WebSocket Remote Server (Recommended for Real-time Collaboration)
For real-time collaboration and enhanced performance:

```json
{
  "mcp": {
    "servers": {
      "github": {
        "url": "wss://your-mcp-server.example.com/github",
        "headers": {
          "Authorization": "Bearer ${env:GITHUB_PERSONAL_ACCESS_TOKEN}",
          "X-API-Key": "${env:MCP_API_KEY}"
        },
        "reconnect": true,
        "reconnectInterval": 5000,
        "timeout": 30000
      }
    }
  }
}
```

**WebSocket Advantages:**
- ✅ Real-time collaboration
- ✅ Automatic reconnection
- ✅ Better performance for frequent operations
- ✅ Persistent connections
- ✅ Lower latency for interactive workflows

**Setup Script:**
```powershell
# Quick setup for WebSocket configuration
.\scripts\Setup-WebSocket-MCP.ps1 -ServerUrl "wss://your-server.com/github" -ApiKey "your_key"

# Test the configuration
.\scripts\Test-WebSocket-MCP.ps1 -Detailed
```

#### Option 3: Docker Container Remote Server
Deploy as a containerized service:

```json
{
  "mcp": {
    "servers": {
      "github": {
        "url": "http://localhost:3001/github",
        "env": {
          "GITHUB_PERSONAL_ACCESS_TOKEN": "${env:GITHUB_PERSONAL_ACCESS_TOKEN}",
          "MCP_SERVER_PORT": "3001",
          "MCP_SERVER_HOST": "0.0.0.0"
        }
      }
    }
  }
}
```

## Advanced Configuration

### Custom Server Parameters
Pass additional configuration through environment variables:

```json
{
  "mcp": {
    "servers": {
      "github": {
        "command": "npx",
        "args": ["-y", "@modelcontextprotocol/server-github"],
        "env": {
          "GITHUB_PERSONAL_ACCESS_TOKEN": "${env:GITHUB_PERSONAL_ACCESS_TOKEN}",
          "GITHUB_API_URL": "${env:GITHUB_API_URL}",
          "GITHUB_REPOSITORY": "${env:GITHUB_REPOSITORY}",
          "GITHUB_BASE_URL": "${env:GITHUB_BASE_URL}",
          "GITHUB_API_VERSION": "2022-11-28",
          "GITHUB_REQUEST_TIMEOUT": "30000",
          "GITHUB_MAX_RETRIES": "3",
          "GITHUB_RETRY_DELAY": "1000"
        }
      }
    }
  }
}
```

### Load Balancing Configuration
For high-availability setups:

```json
{
  "mcp": {
    "servers": {
      "github": {
        "urls": [
          "https://mcp-server-1.example.com/github",
          "https://mcp-server-2.example.com/github",
          "https://mcp-server-3.example.com/github"
        ],
        "loadBalancing": "round-robin",
        "headers": {
          "Authorization": "Bearer ${env:GITHUB_PERSONAL_ACCESS_TOKEN}"
        }
      }
    }
  }
}
```

## Usage

### Starting the Server
The GitHub MCP server runs automatically when VS Code starts with the configured settings.

### Available Tools
The GitHub MCP server provides tools for:
- Repository management
- Issue tracking
- Pull request management
- File operations
- Git operations
- And more...

## Testing

To test the MCP server connection:

1. Ensure your GitHub PAT is set in the environment
2. Restart VS Code to reload MCP configuration
3. Check VS Code output panel for MCP server logs

## Troubleshooting

### Common Issues
1. **Token not found**: Ensure `GITHUB_PERSONAL_ACCESS_TOKEN` is set in your environment
2. **Permission denied**: Verify your GitHub PAT has the required permissions
3. **Server not starting**: Check VS Code MCP logs for error messages
4. **Connection timeout**: Adjust `GITHUB_REQUEST_TIMEOUT` for slower networks
5. **Rate limiting**: Implement retry logic with `GITHUB_MAX_RETRIES`

### Logs
MCP server logs can be found in:
- VS Code Output panel (select "MCP" from dropdown)
- Terminal output when running manually
- Server logs (for remote deployments)

## Security Notes
- Never commit GitHub tokens to version control
- Regularly rotate your Personal Access Tokens
- Use tokens with minimal required permissions
- Store tokens securely using environment variables
- Use HTTPS for remote server communication
- Implement proper authentication and authorization

## Integration with WileyWidget
The GitHub MCP server enables:
- Automated repository management
- Issue tracking integration
- CI/CD pipeline management
- Code review workflows
- Release management

## Migration from Local to Remote

### Step 1: Deploy Remote Server
```bash
# Using Docker
docker run -d \
  --name github-mcp-server \
  -p 3001:3001 \
  -e GITHUB_PERSONAL_ACCESS_TOKEN=your_token_here \
  mcp/github-server:latest

# Using Kubernetes
kubectl apply -f github-mcp-deployment.yaml
```

### Step 2: Update VS Code Configuration
Replace the local server configuration with remote server configuration in `.vscode/settings.json`.

### Step 3: Test Connection
1. Restart VS Code
2. Verify MCP tools are available
3. Test a few operations to ensure connectivity

### Step 4: Update Environment Variables
Ensure all required environment variables are available to the remote server.

Last Updated: August 28, 2025
