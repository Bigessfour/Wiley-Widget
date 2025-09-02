# Application Separation Guide

## 🎯 **Problem Solved**
This guide establishes clear separation between BusBuddy and Wiley Widget applications to prevent confusion when switching between projects.

## 📁 **Current Setup**
- **Wiley Widget**: `c:\Users\biges\Desktop\Wiley_Widget\`
- **BusBuddy**: `c:\Users\biges\Desktop\BusBuddy\` (assumed location)

## 🚀 **Implementation Steps**

### Step 1: Create Separate VS Code Workspaces

#### For Wiley Widget (Already Created):
- **File**: `WileyWidget.code-workspace`
- **Location**: `c:\Users\biges\Desktop\Wiley_Widget\`
- **Purpose**: Isolated Wiley Widget development environment

#### For BusBuddy (Create Manually):
1. Navigate to your BusBuddy project directory
2. Create `BusBuddy.code-workspace` with the following content:

```json
{
	"folders": [
		{
			"path": "."
		}
	],
	"settings": {
		// BusBuddy specific settings
		"azureResourceManagerTools.resourceGroupFilter": "BusBuddy-RG",
		"sql.databaseConnections": [
			{
				"connectionString": "Server=tcp:${env:BUSBUDDY_AZURE_SQL_SERVER},1433;Database=${env:BUSBUDDY_AZURE_SQL_DATABASE};User ID=${env:BUSBUDDY_AZURE_SQL_USER};Password=${env:BUSBUDDY_AZURE_SQL_PASSWORD};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
				"name": "BusBuddy Azure SQL Database",
				"providerName": "MSSQL"
			}
		],
		// MCP Configuration for BusBuddy
		"mcp": {
			"servers": {
				"github": {
					"url": "https://api.githubcopilot.com/mcp/",
					"type": "http",
					"headers": {
						"Authorization": "Bearer ${env:BUSBUDDY_GITHUB_TOKEN}"
					}
				},
				"azure": {
					"command": "azure-mcp-server",
					"type": "stdio",
					"env": {
						"AZURE_CLIENT_ID": "${env:BUSBUDDY_AZURE_CLIENT_ID}",
						"AZURE_CLIENT_SECRET": "${env:BUSBUDDY_AZURE_CLIENT_SECRET}",
						"AZURE_TENANT_ID": "${env:BUSBUDDY_AZURE_TENANT_ID}",
						"AZURE_SUBSCRIPTION_ID": "${env:BUSBUDDY_AZURE_SUBSCRIPTION_ID}"
					}
				},
				"microsoft-docs": {
					"url": "https://mcp.microsoft.com/docs/",
					"type": "http"
				}
			}
		}
	},
	"extensions": {
		"recommendations": [
			"ms-dotnettools.csharp",
			"ms-vscode.powershell",
			"ms-vscode.azure-account",
			"ms-vscode.vscode-json",
			"github.copilot",
			"github.copilot-chat"
		]
	},
	"name": "BusBuddy Workspace"
}
```

### Step 2: Environment Variable Separation

#### Wiley Widget Environment:
- **File**: `.env.wiley-widget` (template provided)
- **Variables**: Prefixed with standard names (AZURE_*, GITHUB_*)
- **Purpose**: Wiley Widget specific configuration

#### BusBuddy Environment:
- **File**: `.env.busbuddy-template` (copy to BusBuddy directory)
- **Variables**: Prefixed with `BUSBUDDY_*` to avoid conflicts
- **Purpose**: BusBuddy specific configuration

### Step 3: MCP Configuration Strategy

#### Global MCP (Updated):
- **File**: `%APPDATA%\Code\User\mcp.json`
- **Purpose**: Generic configuration with prompts for credentials
- **Behavior**: Prompts for subscription/tenant when switching workspaces

#### Workspace-Specific MCP:
- **Wiley Widget**: `.vscode\mcp.json` with Wiley Widget Azure context
- **BusBuddy**: `BusBuddy\.vscode\mcp.json` with BusBuddy Azure context
- **Purpose**: Project-specific MCP server configurations

## 🔄 **How to Switch Between Applications**

### Method 1: VS Code Workspaces (Recommended)
1. **Close current VS Code window**
2. **Double-click the appropriate `.code-workspace` file**:
   - `WileyWidget.code-workspace` for Wiley Widget
   - `BusBuddy.code-workspace` for BusBuddy
3. **VS Code opens with project-specific settings**

### Method 2: File Explorer Navigation
1. **Navigate to project directory**
2. **Right-click folder → "Open with Code"**
3. **Settings automatically apply based on workspace**

## ✅ **Benefits of This Approach**

### Clear Separation:
- ✅ **Azure Resources**: BusBuddy-RG vs WileyWidget-RG clearly separated
- ✅ **Database Connections**: Different SQL databases with clear names
- ✅ **Environment Variables**: Prefixed to prevent conflicts
- ✅ **MCP Servers**: Project-specific configurations

### No More Confusion:
- ✅ **Visual Indicators**: Workspace name shows current project
- ✅ **Resource Filtering**: Azure tools show only relevant resources
- ✅ **Database Connections**: Only relevant DB connections available
- ✅ **GitHub Context**: Project-specific repositories and tokens

### Easy Switching:
- ✅ **One-Click**: Double-click workspace file to switch
- ✅ **Automatic Setup**: All settings apply automatically
- ✅ **No Manual Configuration**: Everything configured per workspace

## 🔧 **Troubleshooting**

### If Azure Resources Show Wrong Project:
1. Check you're in the correct workspace
2. Verify `azureResourceManagerTools.resourceGroupFilter` setting
3. Reload VS Code window

### If MCP Servers Don't Connect:
1. Check environment variables are loaded
2. Verify Azure credentials are correct for the project
3. Check workspace-specific MCP configuration

### If Database Connections Don't Work:
1. Confirm you're using the correct `.env` file
2. Check connection string variables
3. Verify Azure SQL firewall rules

## 📋 **Quick Reference**

| Action | Wiley Widget | BusBuddy |
|--------|-------------|----------|
| **Workspace File** | `WileyWidget.code-workspace` | `BusBuddy.code-workspace` |
| **Resource Group** | `WileyWidget-RG` | `BusBuddy-RG` |
| **Env File** | `.env.wiley-widget` | `.env.busbuddy` |
| **DB Connection** | `Wiley Widget Azure SQL` | `BusBuddy Azure SQL` |
| **GitHub Token** | `GITHUB_PERSONAL_ACCESS_TOKEN` | `BUSBUDDY_GITHUB_TOKEN` |

## 🎯 **Next Steps**

1. **Create BusBuddy workspace file** using the template above
2. **Set up BusBuddy environment variables** using the template
3. **Test switching** between workspaces
4. **Verify Azure resources** show correctly in each workspace
5. **Confirm MCP servers** connect to correct Azure contexts

This separation ensures crystal-clear boundaries between your applications! 🚀
