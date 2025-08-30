# WileyWidget Development Guide

## 📋 **Related Documentation**

### **MANDATORY SAFETY PROCEDURES**
- **[Standard Operating Procedures](sop-azure-operations.md)**: **REQUIRED** - Azure safety protocols
- **[Azure Safety Guide](azure-novice-guide.md)**: Safe Azure operations for all developers
- **[Copilot Azure Integration](copilot-azure-examples.md)**: AI-assisted safe development

### **Project Documentation**
- **[Copilot Instructions](../.vscode/copilot-instructions.md)**: AI assistant guidelines and project standards
- **[Project Plan](../.vscode/project-plan.md)**: True North vision and phased roadmap
- **[GrokSupercomputer Documentation](GrokSupercomputer-Documentation.md)**: AI-powered budget analytics engine guide
- **[Database Setup Guide](database-setup.md)**: SQL Server LocalDB installation and configuration
- **[Syncfusion License Setup](syncfusion-license-setup.md)**: License acquisition and registration guide

## 🚨 **AZURE OPERATIONS - STANDARD DEVELOPMENT WORKFLOW**

### **MANDATORY: Azure Safety Protocol**
**ALL Azure operations must follow the Standard Operating Procedures. Direct Azure CLI commands are FORBIDDEN.**

#### **Daily Azure Workflow (MANDATORY)**
```powershell
# 1. Start of day - Check Azure status
.\scripts\azure-safe-operations.ps1 -Operation status

# 2. Before development work - Test connection
.\scripts\azure-safe-operations.ps1 -Operation connect

# 3. Before any changes - Create backup
.\scripts\azure-safe-operations.ps1 -Operation backup

# 4. Test any operation - ALWAYS use dry-run first
.\scripts\azure-safe-operations.ps1 -Operation [operation] -DryRun

# 5. Execute operation - Only after dry-run testing
.\scripts\azure-safe-operations.ps1 -Operation [operation]

# 6. End of day - Verify system status
.\scripts\azure-safe-operations.ps1 -Operation status
```

#### **Approved Azure Operations**
```powershell
# ✅ SAFE OPERATIONS (use these only)
.\scripts\azure-safe-operations.ps1 -Operation status    # Check system status
.\scripts\azure-safe-operations.ps1 -Operation connect   # Test database connection
.\scripts\azure-safe-operations.ps1 -Operation backup    # Create safe backup
.\scripts\azure-safe-operations.ps1 -Operation list      # List resources
```

#### **FORBIDDEN Direct Commands**
```bash
# ❌ NEVER USE THESE - Use safe scripts instead
az sql db delete
az group delete
az resource delete
az sql db update
```

### **GitHub Copilot Chat Azure Integration**
**MANDATORY: Use Copilot for Azure assistance following safety protocols**

#### **Safe Copilot Questions**
```
✅ "How do I safely check my Azure database connection?"
✅ "Show me how to create a backup using the safe script"
✅ "Explain Azure Resource Groups in simple terms"
✅ "What would happen if I run this command? Explain first"
✅ "I'm new to Azure - help me understand [concept]"
```

#### **Copilot Azure Workflow**
1. **Ask for safe alternatives** to direct commands
2. **Request explanations** before running operations
3. **Use dry-run examples** for testing
4. **Follow safety protocols** in all responses
5. **Provide learning context** for all concepts

## Project Overview

This is a solo-dev WPF desktop app (.NET 9) using **Syncfusion WPF 30.2.7** for UI (Ribbon, DataGrid, Fluent themes). **ENTERPRISE ENHANCEMENT:** Now features enterprise-grade architecture with Microsoft.Extensions.DependencyInjection, health monitoring, service validation, WPF middleware, and comprehensive AI database integration.

## 🔧 **Technology Stack & Standards**

### **Core Architecture (Enterprise Grade)**

**Dependency Injection:**
- **Microsoft.Extensions.DependencyInjection:** Enterprise-grade DI container
- **Service Locator Pattern:** Global service access for WPF compatibility
- **Constructor Injection:** Proper dependency management throughout application
- **Service Registration:** Centralized configuration in `DatabaseConfiguration.cs`

**Health Monitoring & Diagnostics:**
- **Microsoft.Extensions.Diagnostics.HealthChecks:** System health monitoring
- **External API Monitoring:** xAI API availability and performance tracking
- **Health Check Services:** Built-in diagnostics and status reporting
- **Performance Metrics:** Real-time monitoring and alerting

**Service Validation & Configuration:**
- **Microsoft.Extensions.Options:** Strongly-typed configuration with validation
- **Configuration Validation:** Runtime validation of service configurations
- **Multiple Sources:** appsettings.json, environment variables, Azure Key Vault
- **Validation Feedback:** Clear error messages and configuration guidance

**WPF Middleware Pipeline:**
- **Cross-Cutting Concerns:** Logging, error handling, performance monitoring
- **Middleware Pattern:** Extensible pipeline for WPF applications
- **Aspect-Oriented Programming:** Clean separation of concerns
- **Error Recovery:** Automatic retry and fallback mechanisms

**Advanced Database Integration:**
- **Entity Framework Core 8.x:** ORM for all data operations
- **Dedicated Service Layer:** `GrokDatabaseService` for AI operations
- **Audit Trails:** Complete logging of all database interactions
- **Performance Optimization:** Query optimization and connection pooling

**Comprehensive Logging:**
- **Serilog:** Structured logging throughout application
- **Audit Logging:** Complete trail of AI interactions and decisions
- **Performance Monitoring:** Detailed metrics and error tracking
- **Multiple Sinks:** Console, file, and database logging

### **Syncfusion WPF - REQUIRED VERSION: 30.2.7**

**CRITICAL REQUIREMENT**: All Syncfusion controls, resources, and components MUST use version 30.2.7 exclusively.

**Enforced Standards:**

- **Version Lock**: Use ONLY Syncfusion WPF 30.2.7 packages
- **Documentation Source**: Reference ONLY official Syncfusion 30.2.7 documentation
- **No Custom Controls**: All UI components must come from Syncfusion 30.2.7
- **Resource Management**: Use Syncfusion.SfSkinManager.WPF 30.2.7 for theming
- **Control Library**: Use Syncfusion.Tools.WPF 30.2.7 for advanced controls
- **Data Visualization**: Use Syncfusion.SfGrid.WPF 30.2.7 for data grids

**Package References (REQUIRED):**

```xml
<PackageReference Include="Syncfusion.Licensing" Version="30.2.7" />
<PackageReference Include="Syncfusion.SfGrid.WPF" Version="30.2.7" />
<PackageReference Include="Syncfusion.SfSkinManager.WPF" Version="30.2.7" />
<PackageReference Include="Syncfusion.Tools.WPF" Version="30.2.7" />
```

### **Theme System - REQUIRED: FluentDark & FluentLight**

**MANDATORY THEME IMPLEMENTATION:**

**Standard Themes:**

- **FluentDark**: Primary dark theme for professional appearance
- **FluentLight**: Alternative light theme for user preference

**Theme Switching Requirements:**

- Implement live theme switching capability
- Persist user theme selection in settings
- Apply themes using Syncfusion.SfSkinManager.WPF 30.2.7
- Ensure all controls support both themes properly
- Provide theme toggle in application settings/ribbon

**Theme Implementation:**

```csharp
// Required using Syncfusion.SfSkinManager.WPF 30.2.7
using Syncfusion.SfSkinManager;

// Apply FluentDark theme
SfSkinManager.SetTheme(this, new FluentDarkTheme());

// Apply FluentLight theme
SfSkinManager.SetTheme(this, new FluentLightTheme());
```

### **UI Component Standards**

**Ribbon Control:**

- Use Syncfusion.Tools.WPF 30.2.7 RibbonControlAdv
- Implement tabbed navigation structure
- Follow official 30.2.7 documentation patterns

**DataGrid:**

- Use Syncfusion.SfGrid.WPF 30.2.7 SfDataGrid
- Implement sorting, filtering, and CRUD operations
- Follow 30.2.7 binding and configuration patterns

**Dynamic Resources:**

- All resources must come from Syncfusion 30.2.7
- Use theme-aware resource dictionaries
- Ensure proper resource loading and management

## Project Vision & Roadmap

**Reference**: See `docs/wiley-widget-north-star-v1.1.md` for the complete True North vision, detailed roadmap phases, and success criteria.

### Current Development Status

**Phase 1: Foundation & Data Backbone** ✅ _Status: COMPLETED_
**Phase 2: UI Dashboards & Basic Analytics** ✅ _Status: COMPLETED_
**Phase 3: What If Tools & AI Magic** ✅ _Status: COMPLETED_
**Enterprise Architecture Enhancements** ✅ _Status: COMPLETED_
**Phase 4: Polish, Test, & Deploy** 🔄 _Status: IN PROGRESS_

### Implementation Summary

**✅ COMPLETED PHASES:**

**Phase 1: Foundation & Data Backbone (100% Complete)**
- Enterprise, BudgetInteraction, OverallBudget data models
- EF Core setup with Azure SQL configuration
- Database seeding and migration system
- Repository pattern with IEnterpriseRepository
- Comprehensive error handling and logging

**Phase 2: UI Dashboards & Basic Analytics (100% Complete)**
- Syncfusion WPF 30.2.7 full integration
- MainWindow with SfDataGrid, SfChart, budget summary
- Real-time calculations with BudgetMetrics and BudgetInsights
- Theme persistence with SettingsService
- SfDiagram for budget interactions visualization
- Comprehensive testing with NUnit and FlaUI

**Phase 3: What If Tools & AI Magic (100% Complete)**
- GrokSupercomputer with 8+ advanced AI methods
- xAI API integration with secure key management
- Intelligent caching (15-30 minutes) with database persistence
- Fallback calculations when AI offline
- Cost optimization (~$0.01 per analysis)
- Real-time AI updates in UI

**Enterprise Architecture Enhancements (100% Complete)**
- Microsoft.Extensions.DependencyInjection container
- Service locator pattern for WPF compatibility
- Health monitoring with external API checks
- Service validation with options pattern
- WPF middleware pipeline for cross-cutting concerns
- Dedicated GrokDatabaseService for AI operations
- Comprehensive audit logging with Serilog
- Performance monitoring and optimization

**🔄 CURRENT PHASE:**

**Phase 4: Polish, Test, & Deploy (20% In Progress)**
- Testing overhaul (80%+ coverage target)
- Feature polish and user experience improvements
- Security audit and documentation updates
- CI/CD workflow enhancements
- Beta testing and user feedback integration

### Phase 3: Enhanced UI Features 📋 (Planned)

**Goal**: Rich user interface development

- Ribbon tabbed navigation
- Advanced DataGrid features
- Live theme switching (Fluent Dark/Light)
- Responsive design and accessibility
- Professional UX patterns

### Phase 4: QuickBooks Online Integration 🔗 (Future)

**Goal**: Secure external API integration

- OAuth 2.0 authentication flow
- Secure token management
- Bidirectional data synchronization
- Customer/Invoice/Item data exchange
- Conflict resolution strategies

### Phase 5: Testing & Quality Assurance 🧪 (Ongoing)

**Goal**: Comprehensive quality assurance

- UI automation with FlaUI
- Integration testing suite
- Performance testing and optimization
- Security testing and vulnerability assessment

### Phase 6: Production Polish ✨ (Final)

**Goal**: Production-ready application

- Code signing and security
- Auto-updater mechanism
- Self-contained packaging
- Professional documentation
- Application monitoring

## Architecture Principles

### MVVM Pattern

- **Strict separation**: No code-behind logic in XAML files
- **CommunityToolkit.Mvvm**: Use `[ObservableObject]`, `[RelayCommand]`, and `[ObservableProperty]`
- **ViewModels in ViewModels folder**: Keep all business logic here
- **Models for data structures**: Simple POCOs with validation

### Data Layer

- **EF Core**: Microsoft.EntityFrameworkCore.SqlServer for Azure SQL
- **No migrations yet**: Keep schema simple and clean
- **Repository Pattern**: Clean separation between data access and business logic
- **Connection Resilience**: Automatic retry logic for transient failures

## 🔗 Database Connection Methods & Configuration

### Supported Connection Methods

WileyWidget supports multiple database connection methods to accommodate different development and production scenarios:

#### 1. **LocalDB (Development Default)**

**Best for**: Local development, unit testing, offline work

**Configuration**:

```json
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WileyWidgetDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

**Setup Commands**:

```powershell
# Install LocalDB
# cspell:disable-next-line
choco install sql-server-localdb -y

# Verify installation
sqllocaldb info

# Start LocalDB
sqllocaldb start MSSQLLocalDB
```

#### 2. **Azure SQL Database (Production)**

**Best for**: Cloud deployments, enterprise applications, multi-user scenarios

**Environment Configuration**:

```env
# .env file
AZURE_SQL_SERVER=your-server.database.windows.net
AZURE_SQL_DATABASE=WileyWidgetDb
AZURE_SQL_USER=your-admin-user
AZURE_SQL_PASSWORD=your-secure-password
AZURE_SQL_RETRY_ATTEMPTS=3
```

**App Settings Configuration**:

```json
// appsettings.json
{
  "ConnectionStrings": {
    "AzureConnection": "Server=tcp:${AZURE_SQL_SERVER},1433;Initial Catalog=${AZURE_SQL_DATABASE};Persist Security Info=False;User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

**Azure CLI Setup**:

```powershell
# Complete setup
.\scripts\setup-azure.ps1 -AzureSubscriptionId "your-subscription-id"

# Manual setup
az login
az group create --name "wileywidget-rg" --location "East US"
az sql server create --name "wileywidget-sql" --resource-group "wileywidget-rg" --admin-user "adminuser" --admin-password "SecurePass123!"
az sql db create --name "WileyWidgetDb" --resource-group "wileywidget-rg" --server "wileywidget-sql" --service-objective "Basic"
```

#### 3. **Azure Managed Identity (Secure Production)**

**Best for**: Secure cloud deployments, passwordless authentication

**Configuration**:

```json
// appsettings.json
{
  "ConnectionStrings": {
    "AzureConnection": "Server=${AZURE_SQL_SERVER};Database=${AZURE_SQL_DATABASE};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;"
  }
}
```

**Setup**:

```powershell
# Enable managed identity
az webapp identity assign --name "wileywidget-app" --resource-group "wileywidget-rg"

# Grant SQL access
az sql server ad-admin create --server "wileywidget-sql" --resource-group "wileywidget-rg" --display-name "WileyWidget App" --object-id "<managed-identity-object-id>"
```

#### 4. **Service Principal (CI/CD)**

**Best for**: Automated deployments, CI/CD pipelines

**Configuration**:

```env
# .env file
AZURE_CLIENT_ID=your-service-principal-id
AZURE_CLIENT_SECRET=your-service-principal-secret
AZURE_TENANT_ID=your-tenant-id
```

**Setup**:

```powershell
# Create service principal
az ad sp create-for-rbac --name "wileywidget-sp" --role "Contributor" --scopes "/subscriptions/your-subscription-id"
```

### Connection Method Selection Logic

The application automatically selects the appropriate connection method:

1. **Environment Variables** (Highest Priority)
   - Used when `AZURE_SQL_*` variables are present
   - Supports all Azure connection methods

2. **Configuration Files** (Medium Priority)
   - `appsettings.Production.json` for production
   - `appsettings.json` for development

3. **LocalDB Fallback** (Lowest Priority)
   - Used when no Azure configuration is found
   - Ensures application works offline

### Testing Database Connections

#### Automated Testing

```powershell
# Test LocalDB
.\scripts\test-database-connection.ps1 -UseLocalDB

# Test Azure SQL
.\scripts\test-database-connection.ps1

# Test with specific connection string
.\scripts\test-database-connection.ps1 -ConnectionString "Server=tcp:your-server.database.windows.net,1433;..."

# Create test data
.\scripts\test-database-connection.ps1 -CreateTestData
```

#### Manual Connection Testing

```csharp
// Programmatic connection test
using (var connection = new SqlConnection(connectionString))
{
    try
    {
        connection.Open();
        Console.WriteLine("✅ Connection successful");

        var command = new SqlCommand("SELECT @@VERSION", connection);
        var version = command.ExecuteScalar();
        Console.WriteLine($"SQL Server Version: {version}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Connection failed: {ex.Message}");
    }
}
```

### Performance Optimization

#### Connection Pooling

```json
// appsettings.json
{
  "Database": {
    "MaxPoolSize": 100,
    "MinPoolSize": 5,
    "ConnectionTimeout": 30,
    "Pooling": true
  }
}
```

#### Retry Logic

```json
// appsettings.json
{
  "Database": {
    "MaxRetryCount": 3,
    "MaxRetryDelay": "00:00:30",
    "EnableRetryOnFailure": true
  }
}
```

### Security Best Practices

#### Credential Management

- **Development**: Use `.env` files (never commit to source control)
- **Production**: Use Azure Key Vault or managed identities
- **Passwords**: Rotate every 90 days, use strong passwords
- **Access**: Principle of least privilege

#### Network Security

- **SSL/TLS**: Always use `Encrypt=True`
- **Firewall**: Restrict to specific IP ranges
- **Private Endpoints**: Use for enhanced security
- **Azure Defender**: Enable for SQL Server protection

### Troubleshooting Common Issues

#### LocalDB Issues

```powershell
# Check LocalDB status
sqllocaldb info

# Start LocalDB
sqllocaldb start MSSQLLocalDB

# Recreate instance
sqllocaldb delete MSSQLLocalDB
sqllocaldb create MSSQLLocalDB
```

#### Azure SQL Issues

```powershell
# Test Azure CLI login
az account show

# Check firewall rules
az sql server firewall-rule list --resource-group "wileywidget-rg" --server "wileywidget-sql"

# Add current IP
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "MyIP" --start-ip-address "your-ip" --end-ip-address "your-ip"
```

#### Connection Timeout

```powershell
# Increase timeout
Connection Timeout=60;

# Check network connectivity
Test-NetConnection -ComputerName "your-server.database.windows.net" -Port 1433
```

### Monitoring & Diagnostics

#### Health Checks

```csharp
// Add health checks
services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "Database");
```

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

---

## Project Structure

- **Repository pattern**: Abstract data access for testability
- **Connection strings**: Use Azure.Identity for managed identity in production

## Development Standards

### Languages & Tools

- **C#**: Primary language for business logic and ViewModels
- **XAML**: UI markup only - no code-behind
- **PowerShell**: Build scripts and automation (use proper modules, avoid Write-Host except for traces)

### Code Quality

- **Nullable references**: Disabled for now (enable later via .editorconfig)
- **Testing**: NUnit with 70%+ coverage requirement
- **Logging**: Serilog for structured logging
- **Settings**: JSON persistence via SettingsService

### UI/UX Guidelines

- **Syncfusion only**: No custom controls - use official Syncfusion WPF controls
- **Themes**: Fluent Dark/Light with live switching capability
- **Responsive design**: Handle DPI scaling properly
- **Accessibility**: Follow WPF accessibility guidelines

## Azure Integration

### Database

- **Azure SQL**: Primary data store
- **EF Core**: ORM with Microsoft.EntityFrameworkCore.SqlServer
- **Authentication**: Azure.Identity (managed identity for production, connection string for dev)
- **Connection resilience**: Implement retry logic for transient failures

### Security

- **OAuth**: QuickBooks Online integration
- **Token encryption**: Secure storage of OAuth tokens
- **Managed identity**: Preferred for Azure deployments
- **Connection strings**: Environment-based configuration

## Testing Strategy

### Unit Tests

- **NUnit**: Primary testing framework
- **Coverage**: Minimum 70% line coverage (CI enforced)
- **Location**: WileyWidget.Tests project
- **Mocking**: Use Moq for dependencies

### UI Tests

- **Location**: WileyWidget.UiTests project
- **Framework**: Consider FlaUI for automation
- **Coverage**: Smoke tests for critical UI paths

## Build & Deployment

### CI/CD

- **GitHub Actions**: Automated builds and releases
- **Trunk checks**: Code quality and linting
- **Artifact generation**: Include build diagnostics and test results

### Packaging

- **Self-contained**: Single executable deployment
- **License handling**: External license.key file support
- **Versioning**: Centralized in Directory.Build.targets

## File Organization

### Project Structure

```
WileyWidget/
├── Models/          # Data structures and POCOs
├── ViewModels/      # MVVM ViewModels
├── Services/        # Business logic and external integrations
├── Views/           # XAML UI files
└── Resources/       # Styles, templates, and assets
```

### Configuration Files

- **.editorconfig**: Code style and formatting rules
- **Directory.Build.props**: Common build properties
- **Directory.Build.targets**: Build customization
- **.vscode/settings.json**: VS Code workspace settings

## Development Workflow

### Daily Development

1. **Pull latest**: `git pull origin main`
2. **Create feature branch**: `git checkout -b feature/your-feature`
3. **Write tests first**: TDD approach when possible
4. **Implement feature**: Follow MVVM and coding standards
5. **Run tests**: `pwsh ./scripts/build.ps1`
6. **Commit**: Small, focused commits with conventional prefixes

### Code Review (Self-Review)

- **Readability**: Code should be self-documenting
- **Performance**: Consider UI responsiveness
- **Maintainability**: Follow established patterns
- **Testing**: Adequate test coverage for new features

## Best Practices

### Performance

- **UI Thread**: Keep heavy operations off UI thread
- **Virtualization**: Use for large data sets in DataGrid
- **Lazy loading**: Implement for large datasets
- **Memory management**: Dispose of resources properly

### Error Handling

- **Global exception handling**: App.xaml.cs level
- **User-friendly messages**: Meaningful error dialogs
- **Logging**: Comprehensive error logging with context
- **Graceful degradation**: Handle service failures gracefully

### Security

- **Input validation**: Validate all user inputs
- **Secure storage**: Encrypt sensitive data
- **OAuth flows**: Secure token handling
- **Connection security**: Use encrypted connections

## Tooling & Extensions

### Recommended VS Code Extensions

- **C# Dev Kit**: Core C# development
- **PowerShell Tools**: PowerShell script development
- **Trunk**: Code quality and linting
- **XAML Tools**: XAML editing support

### Build Tools

- **PowerShell 7+**: All automation scripts
- **.NET 9 SDK**: Development and building
- **NuGet**: Package management
- **Git**: Version control

## References

### Documentation

- **README.md**: Quick start and feature overview
- **Copilot Instructions**: AI assistant guidelines and standards
- **CONTRIBUTING.md**: Development workflow
- **RELEASE_NOTES.md**: Release history
- **CHANGELOG.md**: Technical change history

### External Resources

- **Microsoft Docs**: <https://learn.microsoft.com/en-us/dotnet/>
- **EF Core**: <https://learn.microsoft.com/en-us/ef/core/>
- **Syncfusion**: <https://help.syncfusion.com/wpf>
- **Azure SQL**: Azure documentation

## Roadmap Priorities

### Near Term

- **UI Automation**: Implement comprehensive UI tests
- **Theme Switching**: Live theme switching via SfSkinManager
- **Packaging**: Self-contained executable with installer
- **Code Signing**: Secure code signing for releases

### Future Considerations

- **Azure Deployment**: Cloud-native deployment options
- **Advanced Features**: Additional Syncfusion controls
- **Performance Monitoring**: Application performance tracking
- **User Feedback**: In-app feedback mechanisms

---

_This guide should be updated as the project evolves. Last updated: August 28, 2025_
