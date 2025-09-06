# Database Connection Methods Reference

## Overview

Wiley Widget supports multiple database providers with **Azure Managed Identity** established as the **RECOMMENDED** production authentication method. This document provides comprehensive guidance for configuring and migrating between different database connection methods.

## 🔑 Authentication Methods

### 1. Azure Managed Identity (RECOMMENDED - Production)

**Status:** ✅ **RECOMMENDED** for production environments
**Security Level:** 🔐 High (automatic token rotation, no stored credentials)
**Setup Complexity:** Medium
**Maintenance:** Low (automatic)

#### Configuration

**Environment Variables (`.env`):**
```bash
AZURE_SUBSCRIPTION_ID=your-subscription-id
AZURE_TENANT_ID=your-tenant-id
AZURE_SQL_SERVER=your-server.database.windows.net
AZURE_SQL_DATABASE=YourDatabaseName
```

**Application Settings (`appsettings.json`):**
```json
{
  "Database": {
    "Provider": "SqlServer"
  },
  "Azure": {
    "SubscriptionId": "${AZURE_SUBSCRIPTION_ID}",
    "TenantId": "${AZURE_TENANT_ID}",
    "SqlServer": "${AZURE_SQL_SERVER}",
    "Database": "${AZURE_SQL_DATABASE}"
  }
}
```

**Connection String (Auto-generated):**
```
Server=tcp:{server}.database.windows.net,1433;Database={database};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

#### Setup Steps

1. **Enable Managed Identity on Azure Resource:**
   ```bash
   # For Azure VM
   az vm identity assign --resource-group YourRG --name YourVM

   # For Azure App Service
   az webapp identity assign --resource-group YourRG --name YourApp
   ```

2. **Grant Database Access:**
   ```bash
   # Create Azure AD admin for SQL Server
   az sql server ad-admin create \
     --resource-group YourRG \
     --server-name your-sql-server \
     --display-name "ManagedIdentityAdmin" \
     --object-id $(az webapp identity show --resource-group YourRG --name YourApp --query principalId -o tsv)
   ```

3. **Assign Database Roles:**
   ```sql
   -- Execute in SQL Server Management Studio or Azure Portal Query Editor
   CREATE USER [your-app-name] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_datareader ADD MEMBER [your-app-name];
   ALTER ROLE db_datawriter ADD MEMBER [your-app-name];
   ```

#### Benefits

- ✅ **Zero Credential Management:** No passwords to rotate or secure
- ✅ **Enhanced Security:** Azure AD integration with MFA support
- ✅ **Compliance Ready:** Meets enterprise security requirements
- ✅ **Automatic Token Rotation:** No manual intervention required
- ✅ **Audit Trail:** All access logged through Azure AD

#### Limitations

- ❌ Requires Azure AD configuration
- ❌ Only works with Azure SQL Database
- ❌ Requires Azure resource with Managed Identity

---

### 2. Azure SQL Authentication (Development/Transition)

**Status:** ⚠️ **DEPRECATED** - Migrate to Managed Identity
**Security Level:** 🔒 Medium (credentials in environment variables)
**Setup Complexity:** Low
**Maintenance:** High (manual password rotation)

#### Configuration

**Environment Variables (`.env`):**
```bash
AZURE_SQL_SERVER=your-server.database.windows.net
AZURE_SQL_DATABASE=YourDatabaseName
AZURE_SQL_USER=your-username
AZURE_SQL_PASSWORD=your-secure-password
```

**Application Settings (`appsettings.json`):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:${AZURE_SQL_SERVER},1433;Initial Catalog=${AZURE_SQL_DATABASE};Persist Security Info=False;User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "Database": {
    "Provider": "SqlServer"
  }
}
```

#### Migration Path

**Immediate Actions:**
1. Plan migration to Azure Managed Identity
2. Set timeline for credential rotation
3. Update security policies

**Migration Steps:**
1. Enable Managed Identity on Azure resource
2. Grant database permissions to Managed Identity
3. Update application configuration
4. Test with Managed Identity
5. Remove password-based credentials
6. Update documentation

---

### 3. SQL Server LocalDB (Development Only)

**Status:** ⚠️ **DEPRECATED** - Use SQLite for local development
**Security Level:** 🔒 Medium (Windows authentication)
**Setup Complexity:** Medium (requires LocalDB installation)
**Maintenance:** Medium

#### Configuration

**Application Settings (`appsettings.json`):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WileyWidgetDb;Trusted_Connection=True;"
  },
  "Database": {
    "Provider": "LocalDB"
  }
}
```

#### Setup Requirements

1. **Install SQL Server LocalDB:**
   ```bash
   # Download and install SqlLocalDB.msi from Microsoft
   # Or via Chocolatey: choco install sqllocaldb
   ```

2. **Verify Installation:**
   ```bash
   sqllocaldb info
   sqllocaldb start
   ```

#### Migration Path

**Recommended:** Migrate to SQLite for local development
- ✅ No installation required
- ✅ Cross-platform compatibility
- ✅ File-based (easy backup/restore)
- ✅ No server management

---

### 4. SQLite (Default - Development)

**Status:** ✅ **RECOMMENDED** for local development
**Security Level:** 🔒 High (file-based, local access only)
**Setup Complexity:** Low (no installation required)
**Maintenance:** Low

#### Configuration

**Application Settings (`appsettings.json`):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=WileyWidget.db"
  },
  "Database": {
    "Provider": "SQLite"
  }
}
```

#### Features

- ✅ **Zero Configuration:** Works out of the box
- ✅ **Cross-Platform:** Same file format on Windows, macOS, Linux
- ✅ **File-Based:** Easy backup, restore, and sharing
- ✅ **No Server Required:** No additional services to manage
- ✅ **ACID Compliance:** Full transactional support

---

## 🔄 Migration Guides

### Password Authentication → Azure Managed Identity

#### Phase 1: Preparation (1-2 days)
```bash
# 1. Enable Managed Identity
az webapp identity assign --resource-group YourRG --name YourApp

# 2. Get Managed Identity Object ID
MI_OBJECT_ID=$(az webapp identity show --resource-group YourRG --name YourApp --query principalId -o tsv)

# 3. Create Azure AD admin for SQL Server
az sql server ad-admin create \
  --resource-group YourRG \
  --server-name your-sql-server \
  --display-name "ManagedIdentityAdmin" \
  --object-id $MI_OBJECT_ID
```

#### Phase 2: Database Permissions (1 day)
```sql
-- Execute in Azure Portal Query Editor
CREATE USER [your-app-name] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [your-app-name];
ALTER ROLE db_datawriter ADD MEMBER [your-app-name];
GRANT ALTER, EXECUTE, VIEW DEFINITION TO [your-app-name];
```

#### Phase 3: Application Configuration (2-4 hours)
1. **Update `appsettings.json`:**
   ```json
   {
     "Azure": {
       "SubscriptionId": "your-subscription-id",
       "TenantId": "your-tenant-id",
       "SqlServer": "your-server.database.windows.net",
       "Database": "YourDatabaseName"
     }
   }
   ```

2. **Update `.env` file:**
   ```bash
   # Add Azure configuration
   AZURE_SUBSCRIPTION_ID=your-subscription-id
   AZURE_TENANT_ID=your-tenant-id
   AZURE_SQL_SERVER=your-server.database.windows.net
   AZURE_SQL_DATABASE=YourDatabaseName

   # Remove old credentials (after testing)
   # AZURE_SQL_USER=old-username
   # AZURE_SQL_PASSWORD=old-password
   ```

#### Phase 4: Testing & Validation (4-8 hours)
1. **Deploy to staging environment**
2. **Monitor application logs for authentication success**
3. **Run database operations test suite**
4. **Verify audit logs in Azure SQL**

#### Phase 5: Production Deployment (2-4 hours)
1. **Deploy to production**
2. **Monitor for 24-48 hours**
3. **Remove deprecated credentials**
4. **Update security documentation**

### LocalDB → SQLite (Development)

#### Migration Steps
1. **Update `appsettings.json`:**
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Data Source=WileyWidget.db"
     },
     "Database": {
       "Provider": "SQLite"
     }
   }
   ```

2. **Remove LocalDB dependency**
3. **Test application functionality**
4. **Update development documentation**

---

## 🔍 Troubleshooting

### Common Issues

#### "Authentication=Active Directory Managed Identity" not supported
**Cause:** Azure SQL Database not configured for Azure AD authentication
**Solution:**
```bash
# Enable Azure AD authentication
az sql server update \
  --resource-group YourRG \
  --name your-sql-server \
  --enable-ad-only-auth
```

#### Managed Identity not found
**Cause:** Managed Identity not enabled or incorrect resource
**Solution:**
```bash
# Check if Managed Identity is enabled
az webapp identity show --resource-group YourRG --name YourApp

# Enable if missing
az webapp identity assign --resource-group YourRG --name YourApp
```

#### Permission denied for database
**Cause:** Managed Identity not granted database roles
**Solution:**
```sql
-- Grant necessary permissions
ALTER ROLE db_datareader ADD MEMBER [your-app-name];
ALTER ROLE db_datawriter ADD MEMBER [your-app-name];
```

### Monitoring & Logging

#### Application Logs
- Look for: "🔐 Using Azure Managed Identity for database authentication"
- Warning: "⚠️ Azure Managed Identity not configured"
- Error: "Authentication failed" or "Login failed for user"

#### Azure SQL Audit Logs
```sql
-- Query audit logs
SELECT * FROM sys.fn_get_audit_file('path-to-audit-files*.sqlaudit', DEFAULT, DEFAULT)
WHERE database_name = 'YourDatabaseName'
ORDER BY event_time DESC;
```

---

## 📋 Security Checklist

### Azure Managed Identity Setup
- [ ] Managed Identity enabled on Azure resource
- [ ] Azure AD admin configured for SQL Server
- [ ] Database roles assigned to Managed Identity
- [ ] Network security configured (VNet, Firewall)
- [ ] Least privilege principle applied

### Credential Management
- [ ] No hardcoded credentials in source code
- [ ] Environment variables used for configuration
- [ ] Secure credential storage (Azure Key Vault recommended)
- [ ] Regular credential rotation policy
- [ ] Access monitoring and alerting configured

### Compliance
- [ ] Azure AD integration for audit trails
- [ ] Encryption in transit (TLS 1.2+)
- [ ] Encryption at rest enabled
- [ ] Regular security assessments
- [ ] Incident response plan documented

---

## 📞 Support

For issues with database configuration:
1. Check application logs for authentication errors
2. Verify Azure resource configuration
3. Review Azure SQL Database audit logs
4. Test connection with Azure CLI tools
5. Consult Azure documentation for specific error codes

**Recommended Resources:**
- [Azure SQL Database Managed Identity](https://docs.microsoft.com/en-us/azure/sql-database/sql-database-aad-authentication)
- [Azure App Service Managed Identity](https://docs.microsoft.com/en-us/azure/app-service/overview-managed-identity)
- [Azure SQL Security Best Practices](https://docs.microsoft.com/en-us/azure/sql-database/sql-database-security-best-practice)</content>
<parameter name="filePath">c:\Users\biges\Desktop\Wiley_Widget\docs\database-connection-methods-reference.md
