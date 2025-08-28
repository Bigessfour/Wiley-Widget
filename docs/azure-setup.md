# Azure Setup and Database Connectivity Guide

## Overview

This guide provides comprehensive instructions for setting up Azure resources and configuring database connectivity for the WileyWidget application. The setup includes Azure SQL Database, Azure CLI configuration, and MCP server integration.

## Prerequisites

### Required Software

- **Azure CLI**: Latest version (2.76.0 or higher)
- **PowerShell**: Version 7.0 or higher (pwsh)
- **.NET 9.0**: For the WileyWidget application
- **Visual Studio Code**: With Azure extensions

### Azure Account

- Valid Azure subscription
- Appropriate permissions to create resources
- Access to Azure SQL Database service

## Quick Start

### 1. Automated Azure Setup

Run the automated setup script to create all necessary Azure resources:

```powershell
# Navigate to the scripts directory
cd "c:\Users\biges\Desktop\Wiley_Widget\scripts"

# Run the Azure setup script
.\setup-azure.ps1 -AzureSubscriptionId "your-subscription-id" -AzureResourceGroup "wileywidget-rg" -InstallMCP
```

**Parameters:**

- `-AzureSubscriptionId`: Your Azure subscription ID (optional, will prompt if not provided)
- `-AzureResourceGroup`: Resource group name (optional, defaults to "wileywidget-rg")
- `-AzureLocation`: Azure region (optional, defaults to "East US")
- `-SkipLogin`: Skip Azure login (use if already logged in)
- `-InstallMCP`: Install Azure MCP Server extension

### 2. Test Database Connectivity

After setup, test the database connection:

```powershell
# Test Azure SQL Database connection
.\test-database-connection.ps1

# Test LocalDB connection (fallback option)
.\test-database-connection.ps1 -UseLocalDB

# Test with specific connection string
.\test-database-connection.ps1 -ConnectionString "your-connection-string"

# Create test data during testing
.\test-database-connection.ps1 -CreateTestData
```

## Manual Setup (Alternative)

If you prefer manual setup or need more control:

### 1. Install Azure CLI

```powershell
# Using winget (recommended)
winget install Microsoft.AzureCLI

# Or download from: https://aka.ms/installazurecliwindows
```

### 2. Login to Azure

```powershell
# Interactive login
az login

# Device code login (for headless environments)
az login --use-device-code
```

### 3. Set Active Subscription

```powershell
# List available subscriptions
az account list --output table

# Set active subscription
az account set --subscription "your-subscription-id"
```

### 4. Create Resource Group

```powershell
az group create --name "wileywidget-rg" --location "East US"
```

### 5. Create Azure SQL Server

```powershell
# Generate a unique server name
$serverName = "wileywidget-sql-" + (Get-Random -Minimum 1000 -Maximum 9999)

# Create SQL Server
az sql server create `
    --name $serverName `
    --resource-group "wileywidget-rg" `
    --location "East US" `
    --admin-user "wileyadmin" `
    --admin-password "YourSecurePassword123!"
```

### 6. Configure Firewall

```powershell
# Allow Azure services to access the server
az sql server firewall-rule create `
    --resource-group "wileywidget-rg" `
    --server $serverName `
    --name "AllowAzureServices" `
    --start-ip-address "0.0.0.0" `
    --end-ip-address "0.0.0.0"
```

### 7. Create Database

```powershell
az sql db create `
    --resource-group "wileywidget-rg" `
    --server $serverName `
    --name "WileyWidgetDB" `
    --service-objective "Basic"
```

## Environment Configuration

### .env File Setup

The setup script automatically creates a `.env` file with Azure configuration:

```env
# Azure Configuration
AZURE_SUBSCRIPTION_ID=your-subscription-id
AZURE_RESOURCE_GROUP=wileywidget-rg
AZURE_SQL_SERVER=wileywidget-sql-1234.database.windows.net
AZURE_SQL_DATABASE=WileyWidgetDB
AZURE_SQL_ADMIN_USER=wileyadmin
AZURE_SQL_ADMIN_PASSWORD=your-secure-password
AZURE_SQL_CONNECTION_STRING=Server=tcp:wileywidget-sql-1234.database.windows.net,1433;Initial Catalog=WileyWidgetDB;Persist Security Info=False;User ID=wileyadmin;Password=your-secure-password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

### Application Configuration

Update your `appsettings.json` to use environment variables:

```json
{
    "ConnectionStrings": {
        "DefaultConnection": "%AZURE_SQL_CONNECTION_STRING%",
        "LocalDB": "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Database=WileyWidgetDB;"
    },
    "Azure": {
        "SubscriptionId": "%AZURE_SUBSCRIPTION_ID%",
        "ResourceGroup": "%AZURE_RESOURCE_GROUP%",
        "SqlServer": "%AZURE_SQL_SERVER%",
        "Database": "%AZURE_SQL_DATABASE%"
    }
}
```

## MCP Server Integration

### Installing Azure MCP Server

The setup script can automatically install the Azure MCP Server extension:

```powershell
# Install during setup
.\setup-azure.ps1 -InstallMCP

# Or install manually
code --install-extension ms-azuretools.vscode-azure-mcp-server
```

### MCP Server Features

The Azure MCP Server provides:

- **Azure Resource Management**: Create, update, and delete Azure resources
- **Database Operations**: Execute queries against Azure SQL Database
- **Monitoring**: Access Azure Monitor and Application Insights
- **Security**: Manage Azure Active Directory and security policies

### Using MCP Server

Once installed, the MCP server integrates with GitHub Copilot to provide Azure-specific assistance:

```powershell
# Example MCP server commands (available through Copilot)
@azure create sql database
@azure list resources
@azure monitor database performance
@azure backup database
```

## Database Schema

### Automatic Schema Creation

The test script can create a sample database schema:

```powershell
.\test-database-connection.ps1 -CreateTestData
```

This creates:

- **Users** table: User management
- **Widgets** table: Widget inventory with foreign key to Users

### Manual Schema Creation

If you need to create the schema manually:

```sql
-- Create Users table
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    Email NVARCHAR(100) NOT NULL UNIQUE,
    CreatedDate DATETIME2 DEFAULT GETUTCDATE(),
    IsActive BIT DEFAULT 1
);

-- Create Widgets table
CREATE TABLE Widgets (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500),
    Price DECIMAL(10,2),
    CreatedDate DATETIME2 DEFAULT GETUTCDATE(),
    UserId INT,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);
```

## Troubleshooting

### Common Issues

#### 1. Azure CLI Not Found

```powershell
# Check if Azure CLI is installed
az --version

# If not found, install it
winget install Microsoft.AzureCLI
```

#### 2. Authentication Issues

```powershell
# Clear Azure CLI cache
az account clear

# Re-login
az login
```

#### 3. Firewall Connection Issues

```powershell
# Check firewall rules
az sql server firewall-rule list --resource-group "wileywidget-rg" --server "your-server-name"

# Add your IP address
az sql server firewall-rule create `
    --resource-group "wileywidget-rg" `
    --server "your-server-name" `
    --name "AllowMyIP" `
    --start-ip-address "your-ip-address" `
    --end-ip-address "your-ip-address"
```

#### 4. Database Connection Issues

```powershell
# Test connection with Azure CLI
az sql db show-connection-string `
    --server "your-server-name" `
    --name "WileyWidgetDB" `
    --client "ado.net"
```

### Performance Optimization

#### Azure SQL Database Tiers

Choose the appropriate service tier based on your needs:

- **Basic**: Development and testing
- **Standard**: Small production applications
- **Premium**: High-performance applications
- **Hyperscale**: Large-scale applications

```powershell
# Change service tier
az sql db update `
    --resource-group "wileywidget-rg" `
    --server "your-server-name" `
    --name "WileyWidgetDB" `
    --service-objective "S0"
```

## Security Best Practices

### 1. Secure Connection Strings

- Store connection strings in environment variables
- Use Azure Key Vault for production secrets
- Enable encryption and SSL

### 2. Firewall Configuration

- Use specific IP ranges instead of 0.0.0.0
- Regularly review and update firewall rules
- Enable Azure Defender for SQL

### 3. Authentication

- Use Azure Active Directory authentication
- Avoid storing passwords in configuration files
- Implement proper role-based access control

## Monitoring and Maintenance

### Azure Monitor Integration

```powershell
# Enable diagnostic settings
az monitor diagnostic-settings create `
    --name "sql-diagnostics" `
    --resource "/subscriptions/your-subscription/resourceGroups/wileywidget-rg/providers/Microsoft.Sql/servers/your-server" `
    --logs '[{"category": "SQLSecurityAuditEvents", "enabled": true}]' `
    --metrics '[{"category": "AllMetrics", "enabled": true}]' `
    --workspace "/subscriptions/your-subscription/resourceGroups/DefaultResourceGroup/providers/Microsoft.OperationalInsights/workspaces/DefaultWorkspace"
```

### Backup Configuration

```powershell
# Configure automated backups
az sql db backup-policy set `
    --resource-group "wileywidget-rg" `
    --server "your-server-name" `
    --name "WileyWidgetDB" `
    --backup-interval-hours 24 `
    --backup-retention-days 7
```

## Cost Management

### Estimating Costs

- **Basic Tier**: ~$5/month
- **Standard S0**: ~$15/month
- **Data Transfer**: ~$0.12/GB
- **Backup Storage**: ~$0.05/GB

### Cost Optimization

```powershell
# Scale down during off-hours
az sql db update `
    --resource-group "wileywidget-rg" `
    --server "your-server-name" `
    --name "WileyWidgetDB" `
    --service-objective "Basic"
```

## Support and Resources

### Documentation

- [Azure SQL Database Documentation](https://docs.microsoft.com/en-us/azure/azure-sql/)
- [Azure CLI Reference](https://docs.microsoft.com/en-us/cli/azure/)
- [MCP Server Documentation](https://github.com/microsoft/vscode-azure-mcp-server)

### Support Channels

- **Azure Portal**: Support tickets and documentation
- **GitHub Issues**: Report bugs and request features
- **Stack Overflow**: Community support
- **Azure Community**: Forums and user groups

---

## Next Steps

1. **Complete Setup**: Run the automated setup script
2. **Test Connectivity**: Verify database connections work
3. **Configure Application**: Update connection strings in your app
4. **Deploy**: Deploy your application to Azure
5. **Monitor**: Set up monitoring and alerts

For additional assistance, refer to the troubleshooting section or create an issue in the project repository.
