# Database Setup Guide

## 🔗 Connection Methods Overview

WileyWidget supports multiple database connection methods for different environments and use cases. This guide covers setup, configuration, and troubleshooting for all supported connection methods.

### Supported Connection Methods

| Method                | Environment | Security     | Setup Complexity | Use Case                     |
| --------------------- | ----------- | ------------ | ---------------- | ---------------------------- |
| **LocalDB**           | Development | Windows Auth | Low              | Local development, testing   |
| **Azure SQL**         | Production  | SQL Auth     | Medium           | Cloud production, enterprise |
| **Managed Identity**  | Production  | Azure AD     | High             | Secure cloud deployments     |
| **Service Principal** | CI/CD       | Azure AD     | High             | Automated deployments        |

---

## 🏠 **Method 1: LocalDB Setup (Development)**

### Prerequisites

### SQL Server LocalDB Installation

SQL Server LocalDB is required for development and testing. Choose one of the installation methods below:

#### Option 1: Install via SQL Server Express (Recommended)

1. **Download SQL Server Express**:
   - Go to: <https://www.microsoft.com/en-us/sql-server/sql-server-downloads>
   - Download "SQL Server Express" (free edition)

2. **Run the installer**:
   - Select "Basic" installation type
   - Choose default instance name (SQLEXPRESS)
   - Complete the installation

3. **Verify installation**:
   ```powershell
   sqllocaldb info
   ```
   Should show available LocalDB instances.

#### Option 2: Install via Chocolatey (Alternative)

```powershell
# Install Chocolatey first (if not already installed)
Set-ExecutionPolicy Bypass -Scope Process -Force
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))

# Install SQL Server LocalDB
choco install sql-server-localdb -y

# Verify installation
sqllocaldb info
```

#### Option 3: Manual Download (Offline)

1. Download SqlLocalDB.msi from Microsoft:
   - Search for "SQL Server LocalDB" on Microsoft Download Center
   - Download the latest version (2022 recommended)

2. Install the MSI package

### LocalDB Configuration

#### Connection String

The application is pre-configured to use SQL Server LocalDB with the following connection string (in `appsettings.json`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WileyWidgetDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

#### Database Initialization

The application automatically:

1. Creates the database if it doesn't exist
2. Applies any pending migrations
3. Seeds initial data (if configured)

### LocalDB Management Commands

```powershell
# List LocalDB instances
sqllocaldb info

# Start LocalDB instance
sqllocaldb start MSSQLLocalDB

# Stop LocalDB instance
sqllocaldb stop MSSQLLocalDB

# Delete and recreate database (for testing)
sqllocaldb delete MSSQLLocalDB
sqllocaldb create MSSQLLocalDB

# Check database files location
sqllocaldb info MSSQLLocalDB
```

### LocalDB Database Location

LocalDB databases are stored in: `%USERPROFILE%\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB`

---

## ☁️ **Method 2: Azure SQL Database Setup (Production)**

### Prerequisites

- Azure subscription
- Azure CLI installed
- Appropriate permissions

### Automated Setup (Recommended)

```powershell
# Navigate to scripts directory
cd scripts

# Complete Azure setup with SQL database
.\setup-azure.ps1 -AzureSubscriptionId "your-subscription-id"

# Setup with custom parameters
.\setup-azure.ps1 -AzureResourceGroup "wileywidget-prod-rg" -AzureLocation "East US"
```

### Manual Setup

#### 1. Azure CLI Login

```powershell
# Login to Azure
az login

# Set active subscription
az account set --subscription "your-subscription-id"
```

#### 2. Create Resource Group

```powershell
az group create --name "wileywidget-rg" --location "East US"
```

#### 3. Create SQL Server

```powershell
az sql server create `
    --name "wileywidget-sql" `
    --resource-group "wileywidget-rg" `
    --location "East US" `
    --admin-user "wileyadmin" `
    --admin-password "SecurePassword123!"
```

#### 4. Create Database

```powershell
az sql db create `
    --resource-group "wileywidget-rg" `
    --server "wileywidget-sql" `
    --name "WileyWidgetDb" `
    --service-objective "Basic"
```

#### 5. Configure Firewall

```powershell
# Allow Azure services
az sql server firewall-rule create `
    --resource-group "wileywidget-rg" `
    --server "wileywidget-sql" `
    --name "AllowAzureServices" `
    --start-ip-address "0.0.0.0" `
    --end-ip-address "0.0.0.0"

# Add specific IP (recommended for security)
az sql server firewall-rule create `
    --resource-group "wileywidget-rg" `
    --server "wileywidget-sql" `
    --name "MyIP" `
    --start-ip-address "your-ip-address" `
    --end-ip-address "your-ip-address"
```

### Azure SQL Configuration

#### Environment Variables (.env)

```env
# Azure SQL Database Configuration
AZURE_SQL_SERVER=wileywidget-sql.database.windows.net
AZURE_SQL_DATABASE=WileyWidgetDb
AZURE_SQL_USER=wileyadmin
AZURE_SQL_PASSWORD=SecurePassword123!
AZURE_SQL_RETRY_ATTEMPTS=3

# Azure Subscription Information
AZURE_SUBSCRIPTION_ID=your-subscription-id
AZURE_TENANT_ID=your-tenant-id
```

#### App Settings Configuration

```json
// appsettings.json
{
  "ConnectionStrings": {
    "AzureConnection": "Server=tcp:${AZURE_SQL_SERVER},1433;Initial Catalog=${AZURE_SQL_DATABASE};Persist Security Info=False;User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "Database": {
    "MaxRetryCount": "${AZURE_SQL_RETRY_ATTEMPTS}",
    "MaxRetryDelay": "00:00:30",
    "EnableRetryOnFailure": true
  }
}
```

### Azure SQL Service Tiers

| Tier            | Use Case            | Performance | Cost/Month |
| --------------- | ------------------- | ----------- | ---------- |
| **Basic**       | Development/Testing | 5 DTU       | ~$5        |
| **Standard S0** | Small Production    | 10 DTU      | ~$15       |
| **Standard S1** | Medium Production   | 20 DTU      | ~$30       |
| **Premium P1**  | High Performance    | 125 DTU     | ~$465      |

```powershell
# Change service tier
az sql db update `
    --resource-group "wileywidget-rg" `
    --server "wileywidget-sql" `
    --name "WileyWidgetDb" `
    --service-objective "S0"
```

---

## 🔐 **Method 3: Azure Managed Identity (Secure Production)**

### Prerequisites

- Azure App Service or Azure Functions
- Managed Identity enabled
- Azure AD admin configured for SQL Server

### Setup Steps

#### 1. Enable Managed Identity

```powershell
# For App Service
az webapp identity assign `
    --name "wileywidget-app" `
    --resource-group "wileywidget-rg"

# For Function App
az functionapp identity assign `
    --name "wileywidget-functions" `
    --resource-group "wileywidget-rg"
```

#### 2. Configure SQL Server AD Admin

```powershell
# Get managed identity object ID
$objectId = az webapp identity show `
    --name "wileywidget-app" `
    --resource-group "wileywidget-rg" `
    --query principalId -o tsv

# Set as SQL Server AD admin
az sql server ad-admin create `
    --server "wileywidget-sql" `
    --resource-group "wileywidget-rg" `
    --display-name "WileyWidget App" `
    --object-id $objectId
```

### Configuration

#### App Settings

```json
// appsettings.json
{
  "ConnectionStrings": {
    "AzureConnection": "Server=${AZURE_SQL_SERVER};Database=${AZURE_SQL_DATABASE};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;"
  }
}
```

#### Code Implementation

```csharp
// DbContext configuration for managed identity
services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });
});
```

### Advantages

- ✅ No password management required
- ✅ Enhanced security with Azure AD
- ✅ Automatic token rotation
- ✅ Integrated with Azure monitoring
- ✅ Supports Azure AD conditional access

---

## 🔧 **Method 4: Service Principal (CI/CD)**

### Setup Steps

#### 1. Create Service Principal

```powershell
# Create service principal with Contributor role
az ad sp create-for-rbac `
    --name "wileywidget-sp" `
    --role "Contributor" `
    --scopes "/subscriptions/your-subscription-id"
```

#### 2. Grant SQL Server Access

```powershell
# Get service principal object ID
$spObjectId = az ad sp show `
    --id "http://wileywidget-sp" `
    --query objectId -o tsv

# Set as SQL Server AD admin
az sql server ad-admin create `
    --server "wileywidget-sql" `
    --resource-group "wileywidget-rg" `
    --display-name "WileyWidget Service Principal" `
    --object-id $spObjectId
```

### Configuration

#### Environment Variables

```env
# Service Principal Configuration
AZURE_CLIENT_ID=your-service-principal-id
AZURE_CLIENT_SECRET=your-service-principal-secret
AZURE_TENANT_ID=your-tenant-id
```

#### Connection String

```csharp
// Programmatic connection string
var connectionString = $"Server={server};Database={database};Authentication=Active Directory Service Principal;User Id={clientId};Password={clientSecret};Encrypt=True;";
```

### Use Cases

- CI/CD pipelines (GitHub Actions, Azure DevOps)
- Automated deployments
- Service-to-service authentication
- Batch processing applications

---

## 🧪 **Testing Database Connections**

### Automated Testing

```powershell
# Navigate to scripts directory
cd scripts

# Test LocalDB connection
.\test-database-connection.ps1 -UseLocalDB

# Test Azure SQL connection
.\test-database-connection.ps1

# Test with specific connection string
.\test-database-connection.ps1 -ConnectionString "Server=tcp:your-server.database.windows.net,1433;..."

# Create test data during testing
.\test-database-connection.ps1 -CreateTestData
```

### Manual Connection Testing

#### PowerShell Test

```powershell
# Test LocalDB
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=WileyWidgetDb;Trusted_Connection=True;"
$connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
$connection.Open()
"✅ LocalDB connection successful"
$connection.Close()
```

#### Azure SQL Test

```powershell
# Test Azure SQL
$connectionString = "Server=tcp:your-server.database.windows.net,1433;Database=WileyWidgetDb;User ID=your-user;Password=your-password;Encrypt=True;"
$connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
$connection.Open()
"✅ Azure SQL connection successful"
$connection.Close()
```

### Connection Validation Script

```powershell
# Comprehensive connection test
param(
    [string]$ConnectionString,
    [switch]$CreateTestTable
)

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    $connection.Open()

    Write-Host "✅ Connection successful" -ForegroundColor Green

    # Get server info
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT @@VERSION as Version, DB_NAME() as Database"
    $reader = $command.ExecuteReader()

    if ($reader.Read()) {
        Write-Host "📊 Server Version: $($reader["Version"])" -ForegroundColor Cyan
        Write-Host "📊 Database: $($reader["Database"])" -ForegroundColor Cyan
    }
    $reader.Close()

    # Create test table if requested
    if ($CreateTestTable) {
        $command.CommandText = @"
        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ConnectionTest' AND xtype='U')
        CREATE TABLE ConnectionTest (
            Id INT IDENTITY(1,1) PRIMARY KEY,
            TestTime DATETIME2 DEFAULT GETUTCDATE(),
            Message NVARCHAR(255)
        )
"@
        $command.ExecuteNonQuery()
        Write-Host "✅ Test table created" -ForegroundColor Green
    }

    $connection.Close()
} catch {
    Write-Host "❌ Connection failed: $($_.Exception.Message)" -ForegroundColor Red
}
```

---

## 🚨 **Troubleshooting Database Issues**

### Common Issues & Solutions

#### 1. **LocalDB Connection Failed**

**Symptoms:** "Unable to locate LocalDB installation" or "Login failed"

**Solutions:**

```powershell
# Check LocalDB status
sqllocaldb info

# Start LocalDB if stopped
sqllocaldb start MSSQLLocalDB

# Recreate LocalDB instance
sqllocaldb delete MSSQLLocalDB
sqllocaldb create MSSQLLocalDB

# Verify installation
Get-WindowsCapability -Online | Where-Object Name -like "*LocalDB*"
```

#### 2. **Azure SQL Authentication Failed**

**Symptoms:** "Login failed for user" or "Cannot open database"

**Solutions:**

```powershell
# Verify Azure CLI login
az account show

# Test connection string
az sql db show-connection-string `
    --server "your-server" `
    --name "WileyWidgetDb" `
    --client "ado.net"

# Reset admin password
az sql server update `
    --name "your-server" `
    --resource-group "your-rg" `
    --admin-password "NewSecurePassword123!"
```

#### 3. **Firewall Connection Blocked**

**Symptoms:** "Cannot connect to SQL Server" or timeout errors

**Solutions:**

```powershell
# Check firewall rules
az sql server firewall-rule list `
    --resource-group "wileywidget-rg" `
    --server "wileywidget-sql"

# Add current IP
az sql server firewall-rule create `
    --resource-group "wileywidget-rg" `
    --server "wileywidget-sql" `
    --name "MyIP" `
    --start-ip-address "your-current-ip" `
    --end-ip-address "your-current-ip"

# Allow Azure services
az sql server firewall-rule create `
    --resource-group "wileywidget-rg" `
    --server "wileywidget-sql" `
    --name "AllowAzureServices" `
    --start-ip-address "0.0.0.0" `
    --end-ip-address "0.0.0.0"
```

#### 4. **Timeout Issues**

**Symptoms:** "Timeout expired" or "Connection timeout"

**Solutions:**

```powershell
# Increase timeout in connection string
Connection Timeout=60;

# Check network connectivity
Test-NetConnection -ComputerName "your-server.database.windows.net" -Port 1433

# Update environment variable
$env:WILEY_WIDGET_DB_TIMEOUT=60
```

#### 5. **Managed Identity Issues**

**Symptoms:** "Cannot authenticate using managed identity"

**Solutions:**

```powershell
# Verify managed identity
az webapp identity show `
    --name "your-app" `
    --resource-group "wileywidget-rg"

# Check SQL Server AD admin
az sql server ad-admin list `
    --server "wileywidget-sql" `
    --resource-group "wileywidget-rg"

# Reassign managed identity
az webapp identity assign `
    --name "your-app" `
    --resource-group "wileywidget-rg"
```

### Database File Location Issues

#### LocalDB Database Location

- **Default Location**: `%USERPROFILE%\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB`
- **Custom Location**: Can be changed via connection string parameter

#### Azure SQL Database Location

- **Cloud-hosted**: Managed by Azure in the specified region
- **Backup Location**: Automatic backups stored in Azure storage
- **Log Files**: Managed by Azure SQL service

### Performance Issues

#### Slow Query Performance

```sql
-- Enable query execution plan
SET SHOWPLAN_ALL ON;
GO

-- Check for missing indexes
SELECT * FROM sys.dm_db_missing_index_details;
GO

-- Monitor query performance
SELECT * FROM sys.dm_exec_query_stats ORDER BY total_worker_time DESC;
GO
```

#### Connection Pool Exhaustion

```json
// appsettings.json - Connection pool settings
{
  "Database": {
    "MaxPoolSize": 100,
    "MinPoolSize": 5,
    "ConnectionTimeout": 30,
    "Pooling": true
  }
}
```

### Memory and Resource Issues

#### LocalDB Memory Limits

- **Default Limit**: 2GB RAM per instance
- **Configuration**: Can be adjusted via registry or startup parameters

#### Azure SQL Resource Limits

- **DTU Limits**: Based on service tier
- **Storage Limits**: Based on service tier
- **Monitoring**: Use Azure portal or CLI to monitor usage

---

## 📊 **Monitoring & Diagnostics**

### Connection Health Monitoring

#### Application-Level Monitoring

```csharp
// Add health checks
services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "Database");

// Health check endpoint
app.MapHealthChecks("/health");
```

#### Azure SQL Monitoring

```powershell
# Monitor database performance
az sql db show `
    --resource-group "wileywidget-rg" `
    --server "wileywidget-sql" `
    --name "WileyWidgetDb"

# View connection statistics
az sql db show-connection-string `
    --server "wileywidget-sql" `
    --name "WileyWidgetDb" `
    --client "ado.net"
```

### Logging Configuration

#### EF Core Logging

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

#### Azure SQL Auditing

```powershell
# Enable SQL auditing
az sql db audit-policy update `
    --resource-group "wileywidget-rg" `
    --server "wileywidget-sql" `
    --name "WileyWidgetDb" `
    --state Enabled `
    --storage-account "your-storage-account"
```

### Performance Diagnostics

#### Query Performance Insights

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

#### Index Usage Analysis

```sql
-- Check index usage
SELECT
    OBJECT_NAME(i.object_id) AS table_name,
    i.name AS index_name,
    ius.user_seeks,
    ius.user_scans,
    ius.user_lookups,
    ius.user_updates
FROM sys.indexes i
LEFT JOIN sys.dm_db_index_usage_stats ius
    ON i.object_id = ius.object_id AND i.index_id = ius.index_id
WHERE i.object_id > 100
ORDER BY table_name, index_name;
```

---

## 🔒 **Security Best Practices**

### 1. **Credential Management**

- Store passwords in environment variables (development)
- Use Azure Key Vault for production secrets
- Rotate credentials regularly (every 90 days)
- Use strong, complex passwords

### 2. **Network Security**

- Enable SSL/TLS encryption (`Encrypt=True`)
- Use specific IP ranges in firewall rules
- Implement Azure Private Link for enhanced security
- Enable Azure Defender for SQL

### 3. **Access Control**

- Implement principle of least privilege
- Use Azure AD authentication when possible
- Enable SQL Server auditing
- Regular security assessments

### 4. **Data Protection**

- Encrypt sensitive data at rest
- Use Transparent Data Encryption (TDE)
- Implement row-level security if needed
- Regular backup verification

---

## 📚 **Additional Resources**

- [SQL Server LocalDB Documentation](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb)
- [Azure SQL Database Documentation](https://learn.microsoft.com/en-us/azure/azure-sql/database/)
- [Azure Managed Identity](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview)
- [EF Core Connection Strings](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)

---

_Last updated: August 28, 2025_

#### Manual Database Management:

```powershell
# List LocalDB instances
sqllocaldb info

# Start LocalDB instance
sqllocaldb start MSSQLLocalDB

# Stop LocalDB instance
sqllocaldb stop MSSQLLocalDB

# Delete and recreate database (for testing)
sqllocaldb delete MSSQLLocalDB
sqllocaldb create MSSQLLocalDB
```

## Development Environment Setup

### Required Tools

1. **Visual Studio 2022** (or VS Code with C# Dev Kit)
2. **.NET 9.0 SDK**
3. **SQL Server LocalDB**
4. **Git** (for version control)

### Environment Variables

Set the following environment variable for optimal development:

```powershell
# Set database connection timeout (optional)
$env:WILEY_WIDGET_DB_TIMEOUT = "30"
```

## Testing Database Connectivity

### Run Database Tests

```powershell
# Run all tests (requires LocalDB)
dotnet test WileyWidget.Tests/WileyWidget.Tests.csproj

# Run only unit tests (no database required)
dotnet test WileyWidget.Tests/WileyWidget.Tests.csproj --filter "TestCategory!=DatabaseIntegration"
```

### Manual Connectivity Test

```powershell
# Test connection using sqlcmd
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "SELECT @@VERSION"
```

## Production Deployment

For production deployment, update the connection string in `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=your-production-server;Database=WileyWidgetDb;User Id=your-user;Password=your-password;MultipleActiveResultSets=true"
  }
}
```

## Support

If you encounter database setup issues:

1. Check the application logs in `%APPDATA%\WileyWidget\logs`
2. Verify LocalDB installation with `sqllocaldb info`
3. Test connectivity with `sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "SELECT 1"`
4. Ensure you're running with appropriate Windows permissions
