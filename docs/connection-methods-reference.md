# Connection Methods Configuration Reference

## 📋 Quick Reference Guide

This document provides a comprehensive reference for all database connection methods supported by WileyWidget, including configuration examples, setup commands, and troubleshooting.

---

## 🎯 Connection Method Overview

| Method                | Environment | Setup Time | Security     | Use Case                   |
| --------------------- | ----------- | ---------- | ------------ | -------------------------- |
| **LocalDB**           | Development | 5-10 min   | Windows Auth | Local development, testing |
| **Azure SQL**         | Production  | 15-30 min  | SQL Auth     | Cloud production           |
| **Managed Identity**  | Production  | 20-40 min  | Azure AD     | Secure cloud deployments   |
| **Service Principal** | CI/CD       | 30-60 min  | Azure AD     | Automated deployments      |

---

## 🏠 **Method 1: LocalDB (Development)**

### Quick Setup

```powershell
# One-command setup
choco install sql-server-localdb -y
sqllocaldb start MSSQLLocalDB
```

### Configuration

```json
// appsettings.json
{
    "ConnectionStrings": {
        "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WileyWidgetDb;Trusted_Connection=True;MultipleActiveResultSets=true"
    }
}
```

### Test Commands

```powershell
# Test connection
.\scripts\test-database-connection.ps1 -UseLocalDB

# Check LocalDB status
sqllocaldb info

# View database files
sqllocaldb info MSSQLLocalDB
```

### Common Issues

```powershell
# Fix: LocalDB not found
sqllocaldb start MSSQLLocalDB

# Fix: Recreate instance
sqllocaldb delete MSSQLLocalDB
sqllocaldb create MSSQLLocalDB
```

---

## ☁️ **Method 2: Azure SQL Database (Production)**

### Quick Setup

```powershell
# Automated setup
.\scripts\setup-azure.ps1 -AzureSubscriptionId "your-subscription-id"
```

### Manual Setup

```powershell
# Login and create resources
az login
az group create --name "wileywidget-rg" --location "East US"
az sql server create --name "wileywidget-sql" --resource-group "wileywidget-rg" --admin-user "adminuser" --admin-password "SecurePass123!"
az sql db create --name "WileyWidgetDb" --resource-group "wileywidget-rg" --server "wileywidget-sql" --service-objective "Basic"
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "AllowAzureServices" --start-ip-address "0.0.0.0" --end-ip-address "0.0.0.0"
```

### Environment Configuration

```env
# .env file
AZURE_SQL_SERVER=wileywidget-sql.database.windows.net
AZURE_SQL_DATABASE=WileyWidgetDb
AZURE_SQL_USER=adminuser
AZURE_SQL_PASSWORD=SecurePass123!
AZURE_SQL_RETRY_ATTEMPTS=3
```

### App Settings

```json
// appsettings.json
{
    "ConnectionStrings": {
        "AzureConnection": "Server=tcp:${AZURE_SQL_SERVER},1433;Initial Catalog=${AZURE_SQL_DATABASE};Persist Security Info=False;User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
    }
}
```

### Test Commands

```powershell
# Test Azure connection
.\scripts\test-database-connection.ps1

# Check Azure resources
az sql db list --resource-group "wileywidget-rg" --server "wileywidget-sql"

# View connection string
az sql db show-connection-string --server "wileywidget-sql" --name "WileyWidgetDb" --client "ado.net"
```

### Service Tiers

```powershell
# Change to Standard S0
az sql db update --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "WileyWidgetDb" --service-objective "S0"

# Change to Premium P1
az sql db update --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "WileyWidgetDb" --service-objective "P1"
```

### Common Issues

```powershell
# Fix: Firewall blocked
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "MyIP" --start-ip-address "your-ip" --end-ip-address "your-ip"

# Fix: Authentication failed
az sql server update --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "wileywidget-sql" --admin-password "NewSecurePassword123!"

# Fix: Database not found
az sql db create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "WileyWidgetDb" --service-objective "Basic"
```

---

## 🔐 **Method 3: Azure Managed Identity**

### Prerequisites

- Azure App Service or Azure Functions
- Managed Identity enabled

### Setup

```powershell
# Enable managed identity
az webapp identity assign --name "wileywidget-app" --resource-group "wileywidget-rg"

# Get managed identity object ID
$objectId = az webapp identity show --name "wileywidget-app" --resource-group "wileywidget-rg" --query principalId -o tsv

# Grant SQL Server access
az sql server ad-admin create --server "wileywidget-sql" --resource-group "wileywidget-rg" --display-name "WileyWidget App" --object-id $objectId
```

### Configuration

```json
// appsettings.json
{
    "ConnectionStrings": {
        "AzureConnection": "Server=${AZURE_SQL_SERVER};Database=${AZURE_SQL_DATABASE};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;"
    }
}
```

### Test Commands

```powershell
# Test managed identity
az webapp identity show --name "wileywidget-app" --resource-group "wileywidget-rg"

# Check SQL Server AD admin
az sql server ad-admin list --server "wileywidget-sql" --resource-group "wileywidget-rg"
```

### Advantages

- ✅ No password management
- ✅ Automatic token rotation
- ✅ Enhanced security
- ✅ Azure AD integration

---

## 🔧 **Method 4: Service Principal (CI/CD)**

### Setup

```powershell
# Create service principal
az ad sp create-for-rbac --name "wileywidget-sp" --role "Contributor" --scopes "/subscriptions/your-subscription-id"

# Grant SQL Server access
$spObjectId = az ad sp show --id "http://wileywidget-sp" --query objectId -o tsv
az sql server ad-admin create --server "wileywidget-sql" --resource-group "wileywidget-rg" --display-name "WileyWidget Service Principal" --object-id $spObjectId
```

### Configuration

```env
# .env file
AZURE_CLIENT_ID=your-service-principal-id
AZURE_CLIENT_SECRET=your-service-principal-secret
AZURE_TENANT_ID=your-tenant-id
```

### Connection String

```csharp
// Programmatic construction
var connectionString = $"Server={server};Database={database};Authentication=Active Directory Service Principal;User Id={clientId};Password={clientSecret};Encrypt=True;";
```

### Test Commands

```powershell
# Test service principal
az ad sp show --id "http://wileywidget-sp"

# Login with service principal
az login --service-principal --username $clientId --password $clientSecret --tenant $tenantId
```

---

## ⚙️ **Advanced Configuration Options**

### Connection Pooling

```json
// appsettings.json
{
    "Database": {
        "MaxPoolSize": 100,
        "MinPoolSize": 5,
        "ConnectionTimeout": 30,
        "CommandTimeout": 30,
        "Pooling": true
    }
}
```

### Retry Logic

```json
// appsettings.json
{
    "Database": {
        "MaxRetryCount": 3,
        "MaxRetryDelay": "00:00:30",
        "EnableRetryOnFailure": true,
        "RetryOnFailureDelay": "00:00:05"
    }
}
```

### Performance Monitoring

```json
// appsettings.json
{
    "Logging": {
        "LogLevel": {
            "Microsoft.EntityFrameworkCore": "Warning",
            "Microsoft.EntityFrameworkCore.Database.Command": "Information"
        }
    }
}
```

---

## 🧪 **Testing & Validation**

### Automated Testing

```powershell
# Test all connection methods
.\scripts\test-database-connection.ps1 -UseLocalDB
.\scripts\test-database-connection.ps1
.\scripts\test-database-connection.ps1 -CreateTestData
```

### Manual Testing

```powershell
# PowerShell connection test
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=WileyWidgetDb;Trusted_Connection=True;"
$connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
$connection.Open()
"✅ Connection successful"
$connection.Close()
```

### Health Checks

```csharp
// Add to Program.cs or Startup.cs
services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "Database");
```

---

## 🚨 **Troubleshooting Quick Reference**

### Connection Issues

```powershell
# Test network connectivity
Test-NetConnection -ComputerName "your-server.database.windows.net" -Port 1433

# Check Azure CLI login
az account show

# Verify LocalDB
sqllocaldb info
```

### Authentication Issues

```powershell
# Reset Azure SQL password
az sql server update --resource-group "wileywidget-rg" --server "wileywidget-sql" --admin-password "NewSecurePassword123!"

# Check managed identity
az webapp identity show --name "your-app" --resource-group "wileywidget-rg"
```

### Performance Issues

```powershell
# Check database performance
az sql db show --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "WileyWidgetDb"

# Monitor connection pool
# (Check application logs for EF Core connection events)
```

### Firewall Issues

```powershell
# Add current IP
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "MyIP" --start-ip-address "your-ip" --end-ip-address "your-ip"

# Allow Azure services
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "AllowAzureServices" --start-ip-address "0.0.0.0" --end-ip-address "0.0.0.0"
```

---

## 📊 **Monitoring & Diagnostics**

### Azure SQL Monitoring

```powershell
# Enable diagnostic settings
az monitor diagnostic-settings create --name "sql-diagnostics" --resource "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Sql/servers/server" --logs '[{"category": "SQLSecurityAuditEvents", "enabled": true}]' --metrics '[{"category": "AllMetrics", "enabled": true}]' --workspace "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.OperationalInsights/workspaces/workspace"
```

### Query Performance

```sql
-- Top 10 slowest queries
SELECT TOP 10
    qs.total_worker_time / qs.execution_count AS avg_worker_time,
    qs.execution_count,
    qs.total_worker_time,
    qs.last_execution_time,
    SUBSTRING(qt.text, qs.statement_start_offset / 2 + 1,
        (CASE WHEN qs.statement_end_offset = -1
            THEN LEN(CONVERT(NVARCHAR(MAX), qt.text)) * 2
            ELSE qs.statement_end_offset END - qs.statement_start_offset) / 2 + 1) AS query_text
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) qt
ORDER BY avg_worker_time DESC;
```

### Connection Statistics

```sql
-- Monitor connection usage
SELECT * FROM sys.dm_exec_connections;
SELECT * FROM sys.dm_exec_sessions;
```

---

## 🔒 **Security Best Practices**

### Credential Management

- **Environment Variables**: Store in `.env` files (development)
- **Azure Key Vault**: Store secrets in production
- **Managed Identity**: Use for Azure-hosted applications
- **Regular Rotation**: Rotate passwords every 90 days

### Network Security

- **SSL/TLS**: Always use `Encrypt=True`
- **Firewall Rules**: Restrict to specific IP ranges
- **Private Endpoints**: Use for enhanced security
- **Azure Defender**: Enable for SQL Server protection

### Access Control

- **Least Privilege**: Grant minimum required permissions
- **Azure AD**: Use for authentication when possible
- **Audit Logging**: Enable SQL Server auditing
- **MFA**: Require multi-factor authentication

---

## 📚 **Additional Resources**

### Documentation

- [Azure SQL Connection Strings](https://learn.microsoft.com/en-us/azure/azure-sql/database/connect-query-content-reference-guide)
- [EF Core Configuration](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
- [Azure Managed Identity](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview)
- [LocalDB Documentation](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb)

### Tools

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/)
- [SQL Server Management Studio](https://learn.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms)
- [Azure Data Studio](https://learn.microsoft.com/en-us/sql/azure-data-studio/download-azure-data-studio)

### Community

- [Stack Overflow - Azure SQL](https://stackoverflow.com/questions/tagged/azure-sql-database)
- [EF Core GitHub](https://github.com/dotnet/efcore)
- [Azure Community](https://techcommunity.microsoft.com/t5/azure/bd-p/Azure)

---

## 🚀 **Quick Start Commands**

### Development Setup

```powershell
# Install LocalDB
choco install sql-server-localdb -y

# Start LocalDB
sqllocaldb start MSSQLLocalDB

# Test connection
.\scripts\test-database-connection.ps1 -UseLocalDB
```

### Production Setup

```powershell
# Login to Azure
az login

# Complete setup
.\scripts\setup-azure.ps1 -AzureSubscriptionId "your-subscription-id"

# Test connection
.\scripts\test-database-connection.ps1
```

### CI/CD Setup

```powershell
# Create service principal
az ad sp create-for-rbac --name "wileywidget-sp" --role "Contributor" --scopes "/subscriptions/your-subscription-id"

# Setup database
.\scripts\setup-azure.ps1 -AzureSubscriptionId "your-subscription-id"
```

---

_Last updated: August 28, 2025_
