# | Document | Purpose | Audience |

|----------|---------|----------|
| [README.md](../README.md) | Project overview, quick start, features | All users |
| [Project Plan](../.vscode/project-plan.md) | True North vision and phased roadmap | All stakeholders |
| [Copilot Instructions](../.vscode/copilot-instructions.md) | AI assistant guidelines and standards | Developers |
| [Development Guide](development-guide.md) | Comprehensive technical standards | Developers |
| [Azure SQL Integration](azure-sql-integration.md) | Database setup and EF Core implementation | Developers |
| [Connection Methods Reference](connection-methods-reference.md) | Complete connection configuration guide | Developers |
| [Contributing Guide](../CONTRIBUTING.md) | Development workflow and guidelines | Contributors |🚀 Getting Started

| Document                                          | Purpose                                   | Audience         |
| ------------------------------------------------- | ----------------------------------------- | ---------------- | ------------------- |
| [README.md](../README.md)                         | Project overview, quick start, features   | All users        |
| [Project Plan](../.vscode/project-plan.md)        | True North vision and phased roadmap      | All stakeholders |
| [Development Guide](development-guide.md)         | Comprehensive technical standards         | Developers       |
| [Azure SQL Integration](azure-sql-integration.md) | Database setup and EF Core implementation | Developers       |
| [Contributing Guide](../CONTRIBUTING.md)          | Development workflow and guidelines       | Contributors     | Documentation Index |

## 📚 Documentation Overview

This documentation provides comprehensive guidance for developing and maintaining the WileyWidget application.

## 🚀 Getting Started

| Document                                  | Purpose                                 | Audience     |
| ----------------------------------------- | --------------------------------------- | ------------ |
| [README.md](../README.md)                 | Project overview, quick start, features | All users    |
| [Development Guide](development-guide.md) | Comprehensive development standards     | Developers   |
| [Contributing Guide](../CONTRIBUTING.md)  | Development workflow and guidelines     | Contributors |

## 📋 Development Standards

### Architecture & Design

- **MVVM Pattern**: Strict View-ViewModel-Model separation
- **EF Core**: Azure SQL integration with Entity Framework
- **Syncfusion WPF**: UI components and theming
- **CommunityToolkit.Mvvm**: Reactive MVVM framework

### Code Quality

- **Testing**: NUnit with 70%+ coverage requirement
- **Logging**: Serilog structured logging
- **Settings**: JSON-based configuration persistence
- **PowerShell**: Build automation and scripting

### Security & Integration

- **Azure SQL**: Cloud database with managed identity
- **OAuth**: QuickBooks Online secure integration
- **Token Management**: Encrypted credential storage

## 🛠️ Development Workflow

### Daily Development

1. **Setup**: Clone repository and configure environment
2. **Development**: Create feature branches for changes
3. **Testing**: Write and run unit tests (70% coverage minimum)
4. **Build**: Use PowerShell scripts for consistent builds
5. **Review**: Self-review code quality and standards compliance
6. **Merge**: Pull request workflow for main branch integration

### Build Commands

````pwsh
# Full build with tests
pwsh ./scripts/build.ps1

# Include UI smoke tests
$env:RUN_UI_TESTS=1; pwsh ./scripts/build.ps1

# Run specific test project
```powershell
dotnet test WileyWidget.Tests/WileyWidget.Tests.csproj
````

```

## 📁 Project Structure

```

WileyWidget/
├── docs/ # Documentation
│ ├── development-guide.md # Comprehensive standards
│ └── README.md # This index
├── scripts/ # Build automation
├── WileyWidget/ # Main application
│ ├── Models/ # Data structures
│ ├── ViewModels/ # MVVM ViewModels
│ ├── Services/ # Business logic
│ └── Views/ # XAML UI files
├── WileyWidget.Tests/ # Unit tests
└── WileyWidget.UiTests/ # UI automation tests

````

## 🔗 Connection Methods & Configuration

### Database Connection Options

WileyWidget supports multiple database connection methods for different environments:

#### 🏠 **Local Development (LocalDB)**

**Default for development and testing**

```json
// appsettings.json - LocalDB Configuration
{
    "ConnectionStrings": {
        "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WileyWidgetDb;Trusted_Connection=True;MultipleActiveResultSets=true"
    }
}
````

**Setup Commands:**

```powershell
# Install LocalDB (if not already installed)
choco install sql-server-localdb -y

# Verify LocalDB installation
sqllocaldb info

# Start LocalDB instance
sqllocaldb start MSSQLLocalDB

# Check database files location
sqllocaldb info MSSQLLocalDB
```

#### ☁️ **Azure SQL Database (Production)**

**Cloud-hosted database with enterprise features**

```json
// appsettings.json - Azure SQL Configuration
{
  "ConnectionStrings": {
    "AzureConnection": "Server=tcp:${AZURE_SQL_SERVER},1433;Initial Catalog=${AZURE_SQL_DATABASE};Persist Security Info=False;User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

**Environment Variables (.env file):**

```env
# Azure SQL Database Configuration
AZURE_SQL_SERVER=your-server.database.windows.net
AZURE_SQL_DATABASE=WileyWidgetDb
AZURE_SQL_USER=your-admin-user
AZURE_SQL_PASSWORD=your-secure-password
AZURE_SQL_RETRY_ATTEMPTS=3

# Azure Subscription Information
AZURE_SUBSCRIPTION_ID=your-subscription-id
AZURE_TENANT_ID=your-tenant-id
```

#### 🔐 **Azure Managed Identity (Recommended for Production)**

**Passwordless authentication using Azure AD**

```json
// appsettings.json - Managed Identity Configuration
{
  "ConnectionStrings": {
    "AzureConnection": "Server=${AZURE_SQL_SERVER};Database=${AZURE_SQL_DATABASE};Authentication=Active Directory Managed Identity;"
  }
}
```

### Connection Method Selection

The application automatically selects the appropriate connection method:

1. **Development**: Uses LocalDB when `ASPNETCORE_ENVIRONMENT=Development`
2. **Production**: Uses Azure SQL when environment variables are configured
3. **Fallback**: Falls back to LocalDB if Azure connection fails

**Configuration Priority:**

1. Environment variables (highest priority)
2. `appsettings.Production.json`
3. `appsettings.json`
4. LocalDB fallback (lowest priority)

### Testing Database Connections

#### Test LocalDB Connection

```powershell
# Navigate to scripts directory
cd scripts

# Test LocalDB connectivity
.\test-database-connection.ps1 -UseLocalDB
```

#### Test Azure SQL Connection

```powershell
# Test Azure SQL with environment variables
.\test-database-connection.ps1

# Test with specific connection string
.\test-database-connection.ps1 -ConnectionString "Server=tcp:your-server.database.windows.net,1433;..."
```

#### Create Test Data

```powershell
# Create test database schema and sample data
.\test-database-connection.ps1 -CreateTestData
```

### Azure Resource Setup

#### Automated Setup (Recommended)

```powershell
# Complete Azure setup with SQL database
.\setup-azure.ps1 -AzureSubscriptionId "your-subscription-id"

# Setup with custom resource group
.\setup-azure.ps1 -AzureResourceGroup "wileywidget-prod-rg" -AzureLocation "East US"
```

#### Manual Azure CLI Setup

```powershell
# Login to Azure
az login

# Set subscription
az account set --subscription "your-subscription-id"

# Create resource group
az group create --name "wileywidget-rg" --location "East US"

# Create SQL Server
az sql server create --name "wileywidget-sql" --resource-group "wileywidget-rg" --admin-user "adminuser" --admin-password "SecurePass123!"

# Create database
az sql db create --name "WileyWidgetDb" --resource-group "wileywidget-rg" --server "wileywidget-sql" --service-objective "Basic"

# Configure firewall
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "AllowAzureServices" --start-ip-address "0.0.0.0" --end-ip-address "0.0.0.0"
```

### Connection Troubleshooting

#### Common Issues & Solutions

**1. LocalDB Connection Failed**

```powershell
# Check LocalDB status
sqllocaldb info

# Start LocalDB if stopped
sqllocaldb start MSSQLLocalDB

# Recreate LocalDB instance (last resort)
sqllocaldb delete MSSQLLocalDB
sqllocaldb create MSSQLLocalDB
```

**2. Azure SQL Authentication Failed**

```powershell
# Verify Azure CLI login
az account show

# Test Azure SQL connection
az sql db show-connection-string --server "your-server" --name "WileyWidgetDb" --client "ado.net"
```

**3. Firewall Connection Blocked**

```powershell
# Add current IP to firewall
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "MyIP" --start-ip-address "your-ip" --end-ip-address "your-ip"
```

**4. Timeout Issues**

```powershell
# Increase timeout in connection string
Connection Timeout=60;

# Or set in environment
$env:WILEY_WIDGET_DB_TIMEOUT=60
```

### Performance Optimization

#### Connection Pooling

```json
// appsettings.json - Performance Settings
{
  "Database": {
    "MaxPoolSize": 100,
    "MinPoolSize": 5,
    "ConnectionTimeout": 30,
    "CommandTimeout": 30
  }
}
```

#### Retry Logic Configuration

```json
// appsettings.json - Retry Settings
{
  "Database": {
    "MaxRetryCount": 3,
    "MaxRetryDelay": "00:00:30",
    "EnableRetryOnFailure": true
  }
}
```

### Security Best Practices

#### 🔒 **Credential Management**

- Store passwords in environment variables (never in code)
- Use Azure Key Vault for production secrets
- Rotate passwords regularly
- Use managed identities when possible

#### 🛡️ **Network Security**

- Enable SSL/TLS encryption (`Encrypt=True`)
- Use specific IP ranges in firewall rules
- Enable Azure Defender for SQL
- Regular security audits

#### 📊 **Access Control**

- Principle of least privilege
- Regular permission reviews
- Audit logging enabled
- Multi-factor authentication

### Monitoring & Diagnostics

#### Connection Health Checks

```powershell
# Monitor connection pool
# (Available in application logs)

# Check database performance
az sql db show --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "WileyWidgetDb"
```

#### Logging Configuration

```json
// appsettings.json - Logging
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore": "Warning",
      "WileyWidget": "Information"
    }
  }
}
```

---

## 🎯 Quality Gates

### Automated Checks

- **Build**: Must compile successfully
- **Tests**: 70%+ code coverage required
- **Linting**: Trunk checks for code quality
- **Licensing**: Syncfusion license validation

### Manual Reviews

- **Architecture**: MVVM pattern compliance
- **Security**: OAuth and data protection
- **Performance**: UI responsiveness and memory usage
- **Documentation**: Code comments and XML docs

## 📈 Roadmap & Planning

### Current Priorities

- [ ] UI automation testing (FlaUI)
- [ ] Live theme switching improvements
- [ ] Code signing and packaging
- [ ] Advanced Syncfusion features

### Future Considerations

- [ ] Azure deployment automation
- [ ] Performance monitoring
- [ ] User feedback integration
- [ ] Advanced data visualization

## 🤝 Contributing

See [CONTRIBUTING.md](../CONTRIBUTING.md) for detailed contribution guidelines and development workflow.

---

_Documentation maintained in `docs/` folder. Last updated: August 28, 2025_
