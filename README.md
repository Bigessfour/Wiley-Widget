# Wiley Widget: The Ultimate Small-Town Rate Revolution

**Version:** 1.0.1 - Syncfusion WPF 30.2.7 Update (2025-09-03)
**Status:** Production Ready - All Phases Complete
**Timeline:** MVP Achieved - Ready for Beta Testing
**AI Features:** GrokSupercomputer Integration with Enterprise Database Backend
**UI Framework:** Syncfusion WPF 30.2.7
**.NET Target:** .NET 9.0
**Database:** SQLite (configurable to Azure SQL/SQL Server)

## 🎯 **Our True North Star Vision**

We're building a sleek, AI-powered tool for small-town mayors to transform municipal enterprises (Water, Sewer, Trash, Apartments) into self-sustaining businesses. No more Stone Age rates—get real-time dashboards, "What If" scenario planning powered by xAI's Grok, and AI insights that even your Clerk will love.

**Key AI Capabilities:**
- 🤖 **GrokSupercomputer**: AI-powered budget analytics and optimization
- 📊 **Intelligent Insights**: Automated recommendations and risk assessment
- 🎯 **What If Scenarios**: ML-enhanced scenario modeling and forecasting
- 💰 **Cost Optimization**: Smart caching and usage monitoring (~$0.01 per analysis)

**Current Phase:** All Phases Complete - Enterprise Architecture Implemented
- ✅ **Phase 1:** Foundation & Data Backbone (100% Complete)
- ✅ **Phase 2:** UI Dashboards & Basic Analytics (100% Complete)
- ✅ **Phase 3:** What If Tools & AI Magic (100% Complete)
- ✅ **Enterprise Enhancements:** Advanced Architecture (100% Complete)
- 🔄 **Phase 4:** Polish, Test, & Deploy (20% In Progress)

---

## 🤖 **AI-Powered Features: GrokSupercomputer Integration**

Wiley Widget leverages xAI's Grok API through our custom GrokSupercomputer service to deliver enterprise-grade budget analytics with minimal local complexity.

### **Core AI Capabilities**

#### **GrokSupercomputer Service** (`Services/GrokSupercomputer.cs`)
- **Intelligent Budget Analysis**: AI-powered deficit calculations and rate optimization
- **Automated Recommendations**: ML-driven suggestions for budget optimization
- **Scenario Modeling**: "What If" analysis with predictive insights
- **Cost Optimization**: Smart caching and batch processing (~$0.01 per analysis)

#### **Key Methods**
- `ComputeEnterprisesAsync()` - Basic enterprise analysis with AI insights
- `ComputeBudgetAnalyticsAsync()` - Comprehensive budget metrics calculation
- `GenerateBudgetInsightsAsync()` - AI-powered recommendations and risk assessment

#### **Integration Points**
- **MainViewModel**: `AnalyzeBudgetWithGrokAsync()` for comprehensive analysis
- **Caching Layer**: 15-30 minute result caching for performance
- **Fallback System**: Graceful degradation to local calculations when AI unavailable

### **AI Configuration**

#### **Required Setup**
```json
{
  "xAI": {
    "ApiKey": "your-xai-api-key-here",
    "BaseUrl": "https://api.x.ai/v1/",
    "Model": "grok-4-0709"
  }
}
```

#### **Environment Variables**
```bash
# AI Configuration
XAI_API_KEY=your-xai-api-key
XAI_MODEL=grok-4-0709
XAI_CACHE_TTL_MINUTES=30

# Syncfusion Licensing
SYNCFUSION_LICENSE_KEY=your-syncfusion-license-key
SYNCFUSION_LICENSE_KEY_EMBEDDED=embedded-license-key
SYNCFUSION_EMBEDDED_LICENSE_KEY=alternative-embedded-key

# Application Features
WILEYWIDGET_AUTOCLOSE_LICENSE=false
WILEYWIDGET_ENABLE_DEBUG_LOGGING=true
WILEYWIDGET_DISABLE_ANALYTICS=false
WILEYWIDGET_THEME=FluentDark
```

### **Enterprise Architecture Features**

#### **Dependency Injection & Services**
- **Microsoft.Extensions.DependencyInjection:** Enterprise-grade DI container
- **Service Locator Pattern:** Global service access for WPF compatibility
- **Health Monitoring:** External API health checks and system diagnostics
- **Service Validation:** Options pattern with comprehensive configuration validation

#### **Advanced Database Integration**
- **GrokDatabaseService:** Dedicated service for AI database operations
- **AI Entity Models:** AiAnalysisResult, AiRecommendation, AiAnalysisAudit, AiResponseCache
- **Audit Trails:** Complete logging of all AI interactions
- **Intelligent Caching:** Database-persistent caching with performance optimization

#### **WPF Middleware & Cross-Cutting Concerns**
- **Middleware Pipeline:** Extensible pipeline for WPF applications
- **Comprehensive Logging:** Serilog integration with structured logging
- **Error Handling:** Graceful degradation and fallback mechanisms
- **Performance Monitoring:** Real-time metrics and diagnostics

### **AI Development Best Practices**

#### **When to Use AI**
- ✅ Complex multi-variable calculations
- ✅ Predictive modeling and forecasting
- ✅ Intelligent insights and recommendations
- ✅ Risk assessment and scenario analysis

#### **When to Use Local Calculations**
- ❌ Simple arithmetic operations
- ❌ Real-time UI updates requiring instant feedback
- ❌ Offline functionality requirements
- ❌ Processing sensitive or confidential data

#### **Error Handling Pattern**
```csharp
try
{
    return await _grokSupercomputer.ComputeEnterprisesAsync(enterprises);
}
catch (Exception ex)
{
    _logger.Warning(ex, "AI service unavailable, using local calculations");
    return ComputeLocally(enterprises); // Fallback to local calculation
}
```

### **Cost Management**
- **Per Analysis**: ~$0.01 for complex budget calculations
- **Caching**: Reduces API calls by 70% through intelligent result storage
- **Batch Processing**: Combine multiple calculations in single requests
- **Usage Monitoring**: Real-time cost tracking with budget alerts

### **Security & Privacy**
- **Data Anonymization**: Sensitive identifiers removed from AI prompts
- **Secure Storage**: API keys stored in Azure Key Vault
- **Minimal Data**: Only necessary fields sent for calculations
- **Audit Trails**: Complete logging of all AI interactions for compliance
- **Audit Trail**: Complete logging of all AI interactions

### **Performance Characteristics**
- **API Response**: < 5 seconds for complex analyses
- **Cached Results**: < 1 second retrieval
- **Memory Usage**: < 150MB with AI features enabled
- **Offline Mode**: Full functionality without internet connectivity

### **ReadyToRun (R2R) Compilation**
Wiley Widget uses ReadyToRun compilation for optimized startup performance:

- **Configuration**: `PublishReadyToRun=true` in project file
- **Benefits**: Faster application startup, reduced JIT compilation overhead
- **Compatibility**: Works with .NET 6+ and AOT-compatible scenarios
- **Deployment**: Automatically applied during publish operations
- **Fallback**: Graceful degradation if R2R compilation fails

**To disable R2R for debugging:**
```xml
<PropertyGroup>
  <PublishReadyToRun>false</PublishReadyToRun>
</PropertyGroup>
```

### **Syncfusion WPF 30.2.7 Integration**
Wiley Widget uses Syncfusion Essential Studio for WPF to deliver enterprise-grade UI components:

#### **Core Components Used**
- **SfDataGrid**: Advanced data grid with sorting, filtering, and grouping
- **SfChart**: Interactive charts for budget analytics visualization
- **SfScheduler**: Timeline and calendar controls for planning
- **SfDiagram**: Flowchart and diagram creation capabilities
- **SfSkinManager**: Professional theming with FluentDark/FluentLight support
- **RibbonControlAdv**: Modern ribbon interface for main application
- **DockingManager**: Advanced window docking and layout management

#### **Version 30.2.7 Features**
- **Bug Fixes**: GridControl infinity display, PDF processing, SfDataGrid scrolling
- **Performance**: Enhanced rendering and memory management
- **Themes**: Updated FluentDark and FluentLight theme support
- **Compatibility**: Full .NET 9.0 support with latest WPF features

#### **Licensing**
- **Automatic**: License key loaded from environment variables
- **Validation**: Runtime license validation with graceful degradation
- **Configuration**: `SYNCFUSION_LICENSE_KEY` environment variable

### **Future AI Enhancements (Phase 3B)**
- **Predictive Analytics**: ML-based revenue forecasting with seasonal patterns
- **Risk Assessment**: Monte Carlo simulations for budget scenarios
- **Optimization Algorithms**: Advanced rate adjustment recommendations
- **Real-time Analysis**: Streaming data processing capabilities

---

## ⚠️ **STANDARD OPERATING PROCEDURES - READ FIRST**

### **Azure Safety Protocol (MANDATORY)**
**ALL Azure operations must use the safe scripts. Direct Azure CLI commands are FORBIDDEN.**

**🚨 CRITICAL SAFETY RULES:**
- **ALWAYS use safe scripts** for Azure operations
- **NEVER run direct Azure CLI commands** without safe script alternatives
- **ALWAYS test with `-DryRun`** before executing operations
- **ALWAYS create backups** before making changes
- **ALWAYS check status** before and after operations

**✅ APPROVED Azure Operations:**
```powershell
# Check Azure status (safe, read-only)
.\scripts\azure-safe-operations.ps1 -Operation status

# Test database connection (safe, read-only)
.\scripts\azure-safe-operations.ps1 -Operation connect

# Create backup (safe, creates copy)
.\scripts\azure-safe-operations.ps1 -Operation backup

# List resources (safe, read-only)
.\scripts\azure-safe-operations.ps1 -Operation list
```

### **Current Azure Configuration Status**
**✅ Azure Resources Verified and Operational:**
- **Resource Group:** `WileyWidget-RG` (East US)
- **SQL Server:** `wileywidget-sql` (East US 2 - deployed to bypass East US restrictions)
- **Database:** `WileyWidgetDB` (Basic tier, 2GB)
- **Firewall Rules:** `AllowAllAzureServices` + `AllowMyIP` (216.147.125.99)
- **Managed Identity:** Configured for secure authentication
- **MCP Server:** Azure MCP server configured and functional
- **Subscription:** `89c2076a-8c6f-41fe-b03c-850d46a57abf` (Azure subscription 1)

**🔧 MCP (Model Context Protocol) Servers:**
- **GitHub MCP:** Configured for repository management
- **Azure MCP:** Configured for cloud resource operations
- **Microsoft Docs MCP:** Configured for documentation reference

---

## �️ **Azure Database Setup & Configuration**

### **Database Architecture Overview**
Wiley Widget supports both local development (SQLite) and cloud production (Azure SQL Database) environments. The application automatically switches between providers based on configuration.

### **Current Azure Database Configuration**
**✅ Azure SQL Database Successfully Deployed:**
- **Resource Group:** `WileyWidget-RG` (East US)
- **SQL Server:** `wileywidget-sql` (East US 2 - deployed here to avoid East US provisioning restrictions)
- **Database:** `WileyWidgetDB` (Basic tier, 2GB max size)
- **Location:** East US 2 (selected to bypass temporary East US restrictions)
- **Admin User:** `wileyadmin`
- **Connection:** `wileywidget-sql.database.windows.net`

### **Firewall Configuration**
**Dynamic IP Access Enabled:**
- **AllowAllAzureServices:** Permits all Azure services to connect (0.0.0.0 - 0.0.0.0)
- **AllowMyIP:** Permits current public IP `216.147.125.99` for direct connections
- **Note:** IP address may change; update firewall rules if connection fails

### **Connection Configuration**
**Environment Variables (`.env` file):**
```bash
# Azure SQL Database Configuration
AZURE_SQL_SERVER=wileywidget-sql.database.windows.net
AZURE_SQL_DATABASE=WileyWidgetDB
AZURE_SQL_USER=wileyadmin
AZURE_SQL_PASSWORD=P@ssw0rd123!
AZURE_SQL_RETRY_ATTEMPTS=3
```

**Application Settings (`appsettings.json`):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:${AZURE_SQL_SERVER},1433;Initial Catalog=${AZURE_SQL_DATABASE};Persist Security Info=False;User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "Database": {
    "Provider": "SqlServer",
    "EnableSensitiveDataLogging": false,
    "EnableDetailedErrors": false,
    "CommandTimeout": 30,
    "MaxRetryCount": "${AZURE_SQL_RETRY_ATTEMPTS}",
    "MaxRetryDelay": "00:00:30"
  }
}
```

### **Database Deployment Commands**
**Executed Commands (for reference):**
```bash
# 1. Create SQL Server
az sql server create \
  --name wileywidget-sql \
  --resource-group WileyWidget-RG \
  --location "East US 2" \
  --admin-user wileyadmin \
  --admin-password "P@ssw0rd123!"

# 2. Create Database
az sql db create \
  --resource-group WileyWidget-RG \
  --server wileywidget-sql \
  --name WileyWidgetDB \
  --service-objective Basic

# 3. Configure Firewall (Azure Services)
az sql server firewall-rule create \
  --resource-group WileyWidget-RG \
  --server wileywidget-sql \
  --name AllowAllAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# 4. Configure Firewall (Current IP)
az sql server firewall-rule create \
  --resource-group WileyWidget-RG \
  --server wileywidget-sql \
  --name AllowMyIP \
  --start-ip-address 216.147.125.99 \
  --end-ip-address 216.147.125.99
```

### **Security Considerations**
- **⚠️ CHANGE DEFAULT PASSWORD:** The current password `P@ssw0rd123!` should be changed immediately for production use
- **IP Whitelisting:** The `AllowMyIP` rule uses your current public IP. If your IP changes, update the firewall rule
- **Azure Services Access:** The `AllowAllAzureServices` rule allows any Azure service to connect - consider restricting for production

### **Troubleshooting Database Connections**
1. **Connection Timeout:** Verify firewall rules include your current IP
2. **Authentication Failed:** Check username/password in `.env` file
3. **Server Not Found:** Ensure `AZURE_SQL_SERVER` is correct in `.env`
4. **IP Changed:** Update `AllowMyIP` firewall rule with new public IP

**Update Firewall for New IP:**
```bash
# Get current IP
curl -s https://api.ipify.org

# Update firewall rule (replace IP_ADDRESS with actual IP)
az sql server firewall-rule create \
  --resource-group WileyWidget-RG \
  --server wileywidget-sql \
  --name AllowMyIP \
  --start-ip-address IP_ADDRESS \
  --end-ip-address IP_ADDRESS \
  --yes  # Overwrite existing rule
```

### **Database Migration & Schema**
- **Entity Framework:** Configured for Azure SQL Server with automatic migrations
- **Migration Status:** ✅ All migrations applied successfully (2025-09-01)
- **Tables Created:** 8 tables (Enterprises, BudgetInteractions, OverallBudgets, AI entities)
- **Data Types:** SQL Server compatible (datetime2, nvarchar, decimal, etc.)
- **Connection:** Fully tested and operational

---

## �🚀 **Phase 1 Implementation Quick Start**

### **Prerequisites**
1. **Setup Database**: ✅ Azure SQL Database configured and migrated
2. **Setup Environment**: `pwsh ./scripts/load-env.ps1 -Load` (loads secure environment variables)
3. **Setup Syncfusion License**: `pwsh ./scripts/setup-license.ps1` (obtain and configure license)
4. **Setup Azure Environment**: ✅ Azure resources deployed and configured

### **Phase 1 Development Workflow**
```powershell
# 1. Clone and setup
git clone https://github.com/Bigessfour/Wiley-Widget.git
cd Wiley-Widget

# 2. Build the project
dotnet build WileyWidget.csproj

# 3. Run EF migrations (Phase 1)
dotnet ef migrations add InitialCreate
dotnet ef database update

# 4. Test data connection
dotnet run --project WileyWidget.csproj
```

### **Phase 1 Success Benchmarks**
- ✅ **Database Connection:** Azure DB or LocalDB connects without errors
- ✅ **Data Models:** Enterprise, BudgetInteraction, OverallBudget classes created
- ✅ **CRUD Operations:** Can add/edit/delete enterprise data
- ✅ **Performance:** Load time <2s for 100 records, memory <50MB

---

## 📋 **Project Structure**

```
WileyWidget/
├── Configuration/           # Enterprise DI & service configuration
│   ├── DatabaseConfiguration.cs    # Service registration & DI setup
│   ├── ServiceLocator.cs          # Global service access for WPF
│   ├── ExternalApiHealthCheck.cs  # Health monitoring
│   ├── ServiceValidation.cs       # Configuration validation
│   └── WpfMiddleware.cs           # Cross-cutting concerns pipeline
├── Models/                 # Data models & entities
│   ├── Enterprise.cs              # Core enterprise model
│   ├── BudgetInteraction.cs       # Budget relationship model
│   ├── OverallBudget.cs          # Budget summary model
│   └── AiModels.cs               # AI-specific database entities
├── Data/                   # EF Core database context & repositories
│   ├── AppDbContext.cs           # Main database context
│   ├── IEnterpriseRepository.cs  # Repository interface
│   └── EnterpriseRepository.cs   # Repository implementation
├── Services/               # Business logic & AI integration
│   ├── IGrokSupercomputer.cs     # AI service interface
│   ├── GrokSupercomputer.cs      # AI service implementation
│   └── GrokDatabaseService.cs    # AI database operations
├── ViewModels/             # MVVM view models
│   ├── MainViewModel.cs          # Main application view model
│   └── EnterpriseViewModel.cs    # Enterprise-specific view model
├── Views/                  # XAML UI files
│   ├── MainWindow.xaml           # Main application window
│   └── EnterpriseView.xaml       # Enterprise detail view
├── Converters/             # Value converters
│   └── DashboardConverters.cs    # UI data conversion
├── scripts/                # Safe Azure operations & build scripts
├── docs/                   # North Star & implementation guides
└── Tests/                  # Unit & integration tests
```

---

## 🛠️ **Development Guidelines**

### **Rule #1: No Plan Changes Without Group Consensus**
**ME, Grok-4, and Grok Fast Code-1 must ALL agree** to any plan changes. This prevents scope creep and keeps us focused.

### **Code Standards**
- **EF Core 8.x:** Use for all data operations
- **CommunityToolkit.Mvvm:** For ViewModel bindings
- **Syncfusion WPF 30.2.4:** For UI components
- **No nullable reference types:** Per project guidelines
- **Repository Pattern:** For data access abstraction

### **Testing Strategy**
- **Unit Tests:** NUnit for business logic
- **Integration Tests:** Database operations
- **UI Tests:** FlaUI for smoke tests
- **Coverage Target:** 80% by Phase 4

---

## 📚 **Documentation**

- **[Application Separation Guide](APPLICATION_SEPARATION_GUIDE.md)** - Clear separation between BusBuddy and Wiley Widget
- **[North Star Roadmap](docs/wiley-widget-north-star-v1.1.md)** - Complete implementation plan
- **[Contributing Guide](CONTRIBUTING.md)** - Development workflow
- **[Azure Setup](docs/azure-setup.md)** - Safe Azure operations
- **[MCP Configuration Guide](docs/mcp-configuration-guide.md)** - Model Context Protocol setup
- **[Testing Guide](docs/TESTING.md)** - Testing standards

---

## 🎯 **Success Metrics**

By Phase 4 completion:
- ✅ Realistic rates covering operations + employees + quality services
- ✅ "Aha!" moments from dashboards for city leaders
- ✅ AI responses feel like helpful neighbors, not robots
- ✅ Clerk says "This isn't total BS"
- 🎯 **Bonus:** Version 1.0 released on GitHub

---

## 🤝 **Contributing**

See [CONTRIBUTING.md](CONTRIBUTING.md) for development workflow and standards.

**Remember:** This is a hobby-paced project (8-12 weeks to MVP). Small wins, benchmarks, and no pressure—just building something that actually helps your town!

## Documentation

### **Core Operating Procedures (MANDATORY READING)**
- **[Azure Safety Protocol](docs/azure-novice-guide.md)**: **REQUIRED** - Safe Azure operations for all users
- **[Standard Operating Procedures](docs/sop-azure-operations.md)**: Complete operational procedures
- **[Copilot Azure Integration](docs/copilot-azure-examples.md)**: Safe AI-assisted Azure development

### **Project Documentation**
- **[Application Separation Guide](APPLICATION_SEPARATION_GUIDE.md)**: **REQUIRED** - Clear separation between BusBuddy and Wiley Widget applications
- **[Project Plan](.vscode/project-plan.md)**: True North vision and phased roadmap
- **[Development Guide](docs/development-guide.md)**: Comprehensive development standards and best practices
- **[MCP Configuration Guide](docs/mcp-configuration-guide.md)**: Model Context Protocol servers and setup
- **[Copilot Instructions](.vscode/copilot-instructions.md)**: AI assistant guidelines and project standards
- **[Database Setup Guide](docs/database-setup.md)**: SQL Server LocalDB installation and configuration
- **[Syncfusion License Setup](docs/syncfusion-license-setup.md)**: License acquisition and registration guide
- **[QuickBooks Integration](QuickBooks%20Integration.md)**: Complete QBO API reference and implementation guide
- **[Contributing Guide](CONTRIBUTING.md)**: Development workflow and contribution guidelines
- **[Release Notes](RELEASE_NOTES.md)**: Version history and upcoming features
- **[Changelog](CHANGELOG.md)**: Technical change history

## Featuress://github.com/Bigessfour/Wiley-Widget/actions/workflows/ci.yml/badge.svg)

Single-user WPF application scaffold (NET 9) using Syncfusion WPF controls (pinned v30.2.7) with pragmatic tooling.

## Current Status (v0.1.0)

- Core app scaffold stable (build + unit tests green on CI)
- Binary MSBuild logging added (`/bl:msbuild.binlog`) – artifact: `build-diagnostics` (includes `TestResults/msbuild.binlog` & `MSBuildDebug.zip`)
- Coverage threshold: 70% (enforced in CI)
- UI smoke test harness present (optional; not yet comprehensive)
- Syncfusion license loading supports env var, file, or inline (sample left commented)
- Logging: Serilog rolling files + basic enrichers; no structured sink beyond file yet
- Nullable refs intentionally disabled for early simplicity
- Next likely enhancements: richer UI automation, live theme switching via `SfSkinManager`, packaging/signing

## Azure Setup

### Prerequisites
- Azure CLI installed (`winget install Microsoft.AzureCLI`)
- Azure subscription with appropriate permissions
- .NET 8.0 SDK

### Quick Azure Setup

1. **Configure Environment**:
   ```powershell
   # Copy and configure environment file
   Copy-Item .env.example .env
   # Edit .env with your Azure values
   ```

2. **Run Azure Setup Script**:
   ```powershell
   # Test Azure connection
   .\scripts\azure-setup.ps1 -TestConnection

   # Create Azure resources (optional)
   .\scripts\azure-setup.ps1 -CreateResources

   # Deploy database schema
   .\scripts\azure-setup.ps1 -DeployDatabase
   ```

3. **Launch with Azure Configuration**:
   ```powershell
   # In VS Code, use "Launch WileyWidget (Azure)" debug configuration
   # Or run with Azure environment
   $env:ASPNETCORE_ENVIRONMENT = "Production"
   dotnet run --project WileyWidget.csproj
   ```

### Azure Resources Created
- **Resource Group**: `WileyWidget-RG`
- **SQL Server**: Azure SQL Database server
- **SQL Database**: `WileyWidgetDb` (S0 tier)
- **Firewall Rules**: Configured for Azure services

### Azure Development Tools
- **VS Code Extensions**: Azure Account, Functions, Storage, App Service, Cosmos DB
- **Debug Configurations**: Local and Azure-specific launch profiles
- **Database Management**: SQL Server extension for Azure SQL
- **Build Tasks**: Azure setup, connection testing, and deployment tasks

### Environment Variables
```env
# Azure Configuration
AZURE_SUBSCRIPTION_ID=your-subscription-id
AZURE_TENANT_ID=your-tenant-id
AZURE_SQL_SERVER=your-server.database.windows.net
AZURE_SQL_DATABASE=WileyWidgetDb
AZURE_SQL_USER=your-admin-user
AZURE_SQL_PASSWORD=your-secure-password
```

### Troubleshooting
- **Connection Issues**: Run `.\scripts\azure-setup.ps1 -TestConnection`
- **Authentication**: Run `az login` to refresh Azure credentials
- **Firewall**: Ensure your IP is allowed in Azure SQL firewall rules
- **Extensions**: Restart VS Code after installing Azure extensions

## Setup Scripts

### Database Setup

```powershell
# Check database status
pwsh ./scripts/setup-database.ps1 -CheckOnly

# Setup database (install LocalDB if needed)
pwsh ./scripts/setup-database.ps1
```

### Syncfusion License Setup

```powershell
# Check current license status
pwsh ./scripts/setup-license.ps1 -CheckOnly

# Interactive license setup
pwsh ./scripts/setup-license.ps1

# Setup with specific license key
pwsh ./scripts/setup-license.ps1 -LicenseKey "YOUR_LICENSE_KEY"

# Watch license registration (for debugging)
pwsh ./scripts/setup-license.ps1 -Watch

# Remove license setup
pwsh ./scripts/setup-license.ps1 -Remove
```

Minimal enough that future-you won’t hate past-you.

## Features

- Syncfusion DataGrid + Ribbon (add your license key)
- MVVM (CommunityToolkit.Mvvm)
- NUnit tests + coverage
- CI & Release GitHub workflows
- Central versioning (`Directory.Build.targets`)
- Global exception logging to `%AppData%/WileyWidget/logs`
- Theme persistence (FluentDark / FluentLight)
- User settings stored in `%AppData%/WileyWidget/settings.json`
- About dialog with version info
- Window size/position + state persistence
- External license key loader (license.key beside exe)
- Status bar: total item count, selected widget name & price preview
- Theme change logging (recorded via Serilog)

## Environment Configuration

WileyWidget uses secure environment variable management for sensitive configuration:

### Environment Variables

```powershell
# Load environment variables from .env file
.\scripts\load-env.ps1 -Load

# Check current status
.\scripts\load-env.ps1 -Status

# Test connections
.\scripts\load-env.ps1 -TestConnections

# Unload environment variables
.\scripts\load-env.ps1 -Unload
```

### Configuration Hierarchy

1. **Configuration System** (appsettings.json + environment variables)
2. **Environment Variables** (loaded from .env file)
3. **User Secrets** (for development secrets)
4. **Machine Environment Variables** (fallback)

### Azure Configuration

The application is configured to work with Azure SQL Database:

- **Server**: wileywidget-server.database.windows.net
- **Database**: WileyWidgetDB
- **Authentication**: Azure AD Authentication (with managed identity support)

### Security Notes

- **Never commit** `.env` files to version control
- **Use strong passwords** for production databases
- **Rotate credentials** regularly
- **Use managed identities** for Azure-to-Azure authentication
- **Store production secrets** in Azure Key Vault
  Pinned packages (NuGet):

```pwsh
dotnet add WileyWidget/WileyWidget.csproj package Syncfusion.Licensing --version 30.2.7
dotnet add WileyWidget/WileyWidget.csproj package Syncfusion.SfGrid.WPF --version 30.2.7
dotnet add WileyWidget/WileyWidget.csproj package Syncfusion.SfSkinManager.WPF --version 30.2.7
dotnet add WileyWidget/WileyWidget.csproj package Syncfusion.Tools.WPF --version 30.2.7
```

License placement (choose one):

1. Environment variable (recommended): set `SYNCFUSION_LICENSE_KEY` (User scope) then restart shell/app
   ```pwsh
   [System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','<your-key>','User')
   ```
2. Provide a `license.key` file beside the executable (auto‑loaded)
3. Hard‑code in `App.xaml.cs` register call (NOT recommended for OSS / commits)

**WARNING:** Never commit `license.key` or a hard‑coded key – both are ignored/avoidance reinforced via `.gitignore`.

## License Verification

Quick ways to confirm your Syncfusion license is actually registering:

1. Environment variable present:
   ```pwsh
   [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','User')
   ```
   Should output a 90+ char key (don’t echo in screen recordings).
2. Script watch (streams detection + registration path):
   ```pwsh
   pwsh ./scripts/show-syncfusion-license.ps1 -Watch
   ```
   Look for: `Syncfusion license registered from environment variable.`
3. Log inspection:
   ```pwsh
   explorer %AppData%/WileyWidget/logs
   ```
   Open today’s `app-*.log` and verify registration line.
4. File fallback: drop a `license.key` beside the built `WileyWidget.exe` (use `license.sample.key` as format reference).

If none of the above register, ensure the key hasn’t expired and you’re on a supported version (v30.2.4 here).

## Raw File References (machine-consumable)

| Purpose             | Raw URL (replace OWNER/REPO if forked)                                                                 |
| ------------------- | ------------------------------------------------------------------------------------------------------ |
| Settings Service    | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/Services/SettingsService.cs |
| Main Window         | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/MainWindow.xaml             |
| Build Script        | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/scripts/build.ps1                       |
| App Entry           | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/App.xaml.cs                 |
| About Dialog        | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/AboutWindow.xaml            |
| License Loader Note | https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/WileyWidget/App.xaml.cs                 |

## Raw URLs (Machine Readability)

Direct raw links to key project artifacts for automation / ingestion:

- Project file: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/WileyWidget/WileyWidget.csproj
- Solution file: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/WileyWidget.sln
- CI workflow: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/.github/workflows/ci.yml
- Release workflow: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/.github/workflows/release.yml
- Settings service: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/WileyWidget/Services/SettingsService.cs
- License sample: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/WileyWidget/LicenseKey.Private.sample.cs
- Build script: https://raw.githubusercontent.com/REPO_OWNER/REPO_NAME/main/scripts/build.ps1

Replace REPO_OWNER/REPO_NAME with the actual GitHub org/repo when published.

## License Key (Inline Option)

If you prefer inline (e.g., private fork) uncomment and set in `App.xaml.cs`:

```csharp
SyncfusionLicenseProvider.RegisterLicense("YOUR_KEY");
```

Official docs: https://help.syncfusion.com/common/essential-studio/licensing/how-to-register-in-an-application

## Build & Run (Direct)

```pwsh
dotnet build WileyWidget.sln
dotnet run --project WileyWidget/WileyWidget.csproj
```

## Preferred One-Step Build Script

```pwsh
pwsh ./scripts/build.ps1                               # restore + build + unit tests + coverage
RUN_UI_TESTS=1 pwsh ./scripts/build.ps1                # include UI smoke tests
TEST_FILTER='Category=UiSmokeTests' pwsh ./scripts/build.ps1 -Config Debug  # ad-hoc filtered run
pwsh ./scripts/build.ps1 -Publish                      # publish single-file (framework-dependent)
pwsh ./scripts/build.ps1 -Publish -SelfContained -Runtime win-x64  # self-contained executable
```

Tip: For always self-contained releases use: `pwsh ./scripts/build.ps1 -Publish -SelfContained -Runtime win-x64`.

## Versioning

Edit `Directory.Build.targets` (Version / FileVersion) or use release workflow (updates automatically).

## Logging

Structured logging via Serilog writes rolling daily files at:
`%AppData%/WileyWidget/logs/app-YYYYMMDD.log`

Included enrichers: ProcessId, ThreadId, MachineName.

Quick access: `explorer %AppData%\WileyWidget\logs`

- File Header (optional for tiny POCOs) kept minimal – class XML summary suffices.
- Public classes, methods, and properties: XML doc comments (///) summarizing intent.
- Private helpers: brief inline // comment only when intent isn't obvious from name.
- Regions avoided; prefer small, cohesive methods.
- No redundant comments (e.g., // sets X) – focus on rationale, edge cases, side-effects.
- When behavior might surprise (fallbacks, error swallowing), call it out explicitly.

Example pattern:

```csharp
/// <summary>Loads persisted user settings or creates defaults on first run.</summary>
public void Load()
{
	// Corruption handling: rename bad file and recreate defaults.
}
```

## Settings & Theme Persistence

User settings JSON auto-created at `%AppData%/WileyWidget/settings.json`.
Theme buttons update the stored theme immediately; applied on next launch (applied via planned `SfSkinManager` integration).

Environment override (tests / portable mode):

```pwsh
$env:WILEYWIDGET_SETTINGS_DIR = "$PWD/.wiley_settings"
```

If set, the service writes `settings.json` under that directory instead of AppData.

Active theme button is visually indicated (✔) and disabled to prevent redundant clicks.

Example `settings.json`:

```json
{
  "Theme": "FluentDark",
  "Window": { "Width": 1280, "Height": 720, "X": 100, "Y": 100, "State": "Normal" },
  "LastWidgetSelected": "WidgetX"
}
```

## About Dialog

Ribbon: Home > Help > About shows version (AssemblyInformationalVersion).

## Release Flow

1. Decide new version (e.g. 0.1.1)
2. Run GitHub Action: Release (provide version)
3. Download artifact from GitHub Releases page
4. Distribute (self-contained zip includes dependencies)

Artifacts follow naming pattern: `WileyWidget-vX.Y.Z-win-x64.zip`

Releases: https://github.com/Bigessfour/Wiley-Widget/releases

## Project Structure

```
WileyWidget/            # App
WileyWidget.Tests/      # Unit tests
WileyWidget.UiTests/    # Placeholder UI harness
scripts/                # build.ps1
.github/workflows/      # ci.yml, release.yml
CHANGELOG.md / RELEASE_NOTES.md
```

## Tests

```pwsh
dotnet test WileyWidget.sln --collect:"XPlat Code Coverage"
```

Coverage report HTML produced in CI (artifact). Locally you can install ReportGenerator:

```pwsh
dotnet tool update --global dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:Html
```

One-liner (local quick view):

```pwsh
dotnet test --collect:"XPlat Code Coverage" ; reportgenerator -reports:**/coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:Html ; start CoverageReport/index.html
```

### Coverage Threshold (CI)

CI enforces a minimum line coverage (default 70%). Adjust `COVERAGE_MIN` env var in `.github/workflows/ci.yml` as the test suite grows.

## Next (Optional)

- Integrate `SfSkinManager` for live theme switch (doc-backed pattern)
- UI automation (FlaUI) for DataGrid + Ribbon smoke
  - Basic smoke test already included: launches app, asserts main window title & UI children
- Dynamic DataGrid column generation snippet (future):
  ```csharp
  dataGrid.Columns.Clear();
  foreach (var prop in source.First().GetType().GetProperties())
  		dataGrid.Columns.Add(new GridTextColumn { MappingName = prop.Name });
  ```
- Signing + updater
- Pre-push hook (see scripts/pre-push) to gate pushes

Nullable reference types disabled intentionally for simpler interop & to reduce annotation noise at this early stage (may revisit later).

## UI Tests

Basic FlaUI-based smoke test ensures the WPF app launches, main window title contains "Wiley" and a DataGrid control is present. Category: `UiSmokeTests`.

Run only smoke UI tests:

```pwsh
dotnet test WileyWidget.sln --filter Category=UiSmokeTests
```

Or via build script (set optional filter):

```pwsh
$env:TEST_FILTER='Category=UiSmokeTests'
pwsh ./scripts/build.ps1 -Config Debug
```

Enable unit + UI phases (separate runs) without manual filter:

```pwsh
RUN_UI_TESTS=1 pwsh ./scripts/build.ps1
```

Notes:

- UI smoke tests optional; set RUN_UI_TESTS to include them, or filter manually.
- Script performs pre-test process cleanup (kills lingering WileyWidget/testhost) and retries up to 3 times.
- Automation sets `WILEYWIDGET_AUTOCLOSE_LICENSE=1` to auto-dismiss Syncfusion trial dialog when present.

## Troubleshooting

Diagnostics & common gotchas:

- File locking (MSB3026/MSB3027/MSB3021): build script auto-cleans (kills WileyWidget/testhost/vstest.console); verify no stray processes.
- MSB4166 (child node exited): binary log captured at `TestResults/msbuild.binlog`. Open with MSBuild Structured Log Viewer. Raw debug files (if any) archived under `TestResults/MSBuildDebug` + zip. A marker file keeps the folder non-empty for CI retention.
- Capture fresh logs manually:
  ```pwsh
  $env:MSBUILDDEBUGPATH = "$env:TEMP/MSBuildDebug"
  pwsh ./scripts/build.ps1
  # Inspect TestResults/msbuild.binlog or $env:TEMP/MSBuildDebug/ for MSBuild_*.failure.txt
  ```
- No UI tests discovered: use `--filter Category=UiSmokeTests` or set `RUN_UI_TESTS=1`.
- Syncfusion license not detected: run `pwsh ./scripts/show-syncfusion-license.ps1 -Watch`; ensure env var or `license.key` file present.
- Syncfusion trial dialog blocks exit: set `WILEYWIDGET_AUTOCLOSE_LICENSE=1` during automation.
- Coverage report missing: confirm `coverage.cobertura.xml` under `TestResults/`; install ReportGenerator for HTML view.
- Coverage threshold fail: adjust `COVERAGE_MIN` env var or pass `-SkipCoverageCheck` for exploratory build.

## Contributing & Workflow (Single-Dev Friendly)

Even as a solo developer, a light process keeps history clean and releases reproducible.

Branching (Simple)

- main: always buildable; reflects latest completed work.
- feature/short-description: optional for riskier changes; squash merge or fast-forward.

Commit Messages

- Imperative present tense: Add window state persistence
- Group logically (avoid giant mixed commits). Small cohesive commits aid bisecting.

Release Tags

1. Run tests locally
2. Update version via Release workflow (or adjust `Directory.Build.targets` manually for pre-release experiments)
3. Verify artifact zip on the GitHub Release
4. Tag follows semantic versioning (e.g., v0.1.1)

Hotfix Flow

1. branch: hotfix/issue
2. fix + test
3. bump patch version via Release workflow
4. merge/tag

Code Style & Comments

- Enforced informally via `.editorconfig` (spaces, 4 indent, trim trailing whitespace)
- XML docs for public surface, rationale comments for non-obvious private logic
- No redundant narrations (avoid // increment i)

Checklist Before Push

- Build: success
- Tests: all green
- README: updated if feature/user-facing change
- No secrets (ensure `license.key` not committed)
- Logs, publish artifacts, coverage directories excluded

Future (Optional Enhancements)

- Add pre-push git hook to run build+tests
- Add code coverage threshold gate in CI
- Introduce analyzer set (.editorconfig rules) when complexity grows

## QuickBooks Online (Experimental Integration)

> 📖 **Complete API Reference**: See [QuickBooks Integration.md](QuickBooks%20Integration.md) for comprehensive API documentation, endpoints, and implementation details.

### 1. Environment Variables (set once, User scope)

```pwsh
[Environment]::SetEnvironmentVariable('QBO_CLIENT_ID','<client-id>','User')
[Environment]::SetEnvironmentVariable('QBO_CLIENT_SECRET','<client-secret>','User')   # optional for public / PKCE-only flow
[Environment]::SetEnvironmentVariable('QBO_REDIRECT_URI','http://localhost:8080/callback/','User')
[Environment]::SetEnvironmentVariable('QBO_REALM_ID','<realm-id>','User')             # company (realm) id
```

Close and reopen your shell (process must inherit the variables). Redirect URI must EXACTLY match what is configured in the Intuit developer portal.

### 2. Manual Test Flow

1. `dotnet run --project WileyWidget/WileyWidget.csproj`
2. Open the "QuickBooks" tab.
3. Click "Load Customers" (or "Load Invoices").
4. If no tokens stored, the interactive OAuth flow (external browser) should occur; complete consent.
5. After redirect completes and tokens are stored, click the buttons again – data should populate without another consent.

Expected Columns:

- Customers: Name, Email, Phone (auto-generated columns from Intuit `Customer` model)
- Invoices: Number (DocNumber), Total (TotalAmt), Customer (CustomerRef.name), Due Date (DueDate)

### 3. Token Persistence

Tokens are saved in `%AppData%/WileyWidget/settings.json` under:

```jsonc
// excerpt
{
  "QboAccessToken": "...",
  "QboRefreshToken": "...",
  "QboTokenExpiry": "2025-08-12T12:34:56.789Z",
}
```

Delete this file to force a fresh OAuth flow:

```pwsh
Remove-Item "$env:AppData\WileyWidget\settings.json" -ErrorAction SilentlyContinue
```

Token considered valid if:

- `QboAccessToken` not blank AND
- `QboTokenExpiry` > `UtcNow + 60s` (early refresh safety window)

### 4. Troubleshooting

| Symptom                            | Likely Cause                                       | Fix                                                                   |
| ---------------------------------- | -------------------------------------------------- | --------------------------------------------------------------------- |
| "QBO_CLIENT_ID not set" exception  | Env var missing in new shell                       | Re-set variable (User scope) and reopen shell                         |
| No customers/invoices and no error | Empty sandbox company                              | Add sample data in Intuit dashboard                                   |
| Repeated auth prompt               | Tokens not written (settings file locked or crash) | Check logs, ensure `%AppData%/WileyWidget/settings.json` updates      |
| Refresh every click                | `QboTokenExpiry` stayed default                    | Confirm refresh succeeded; inspect log for "QBO token refreshed" line |
| Invoices empty but customers load  | Filter (future) / realm mismatch                   | Verify `QBO_REALM_ID` matches company ID                              |
| Unhandled invalid refresh token    | Token revoked / expired                            | Delete settings file and re-authorize                                 |

### 5. Logs

Open the latest log file to diagnose:

```pwsh
explorer %AppData%\WileyWidget\logs
```

Look for lines:

- `QBO token refreshed (exp ...)`
- `QBO customers fetch failed` / `QBO invoices fetch failed`
- `Syncfusion license registered ...`

### 6. Reset / Clean

```pwsh
taskkill /IM WileyWidget.exe /F 2>$null
taskkill /IM testhost.exe /F 2>$null
dotnet build WileyWidget.sln
```

### 7. Removing Tokens

Just delete the settings file or manually blank the Qbo\* entries; a new auth will occur on next fetch.

> NOTE: Tokens are stored in plain text for early development convenience. Do NOT ship production builds without encrypting or using a secure credential store.

## Version Control Quick Start (Solo Flow)

Daily minimal:

```pwsh
git status
git add .
git commit -m "feat: describe change"
git pull --rebase
git push
```

Feature (risky) change:

```pwsh
git checkout -b feature/thing
# edits
git commit -m "feat: add thing"
git checkout main
git pull --rebase
git merge --ff-only feature/thing
git branch -d feature/thing
git push
```

Undo helpers:

- Discard unstaged file: `git checkout -- path`
- Amend last commit message: `git commit --amend`
- Revert a pushed commit: `git revert <hash>`

Tag release:

```pwsh
git tag -a v0.1.0 -m "v0.1.0"
git push --tags
```

## Extra Git Aliases

Moved to `CONTRIBUTING.md` to keep this lean.

## Syncfusion License Verification

To verify your Syncfusion Community License (v30.2.7) is correctly set up:

1. **Check Environment Variable**:

   ```pwsh
   [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'User')
   ```

   Should return a ~96+ character key (do NOT paste it in issues). If null/empty, it's not registered.

2. **Run Inspection Script (live watch)**:

   ```pwsh
   pwsh ./scripts/show-syncfusion-license.ps1 -Watch
   ```

   Confirms detection source (env var vs file) and registration status in real-time.

3. **Verify Logs** (app has run at least once):

   ```pwsh
   explorer %AppData%\WileyWidget\logs
   ```

   Open today's `app-YYYYMMDD.log` and look for:

   ```
   Syncfusion license registered from environment variable.
   ```

   (or `...from file` if using `license.key`).

4. **Fallback (File Placement)**:
   Place a `license.key` file beside `WileyWidget.exe` (same folder) containing ONLY the key text. See `license.sample.key` for format. Re-run the app or the watch script.

5. **Troubleshooting**:
   - Ensure the env var scope is `User` (not `Process` only) if launching from a new shell.
   - Confirm no hidden BOM / whitespace in `license.key`.
   - Multiple keys: only the first non-empty source is used (env var takes precedence over file).
   - Upgrading Syncfusion: keep version compatibility (here pinned to `30.2.7`).

If issues persist, re-set the env var and restart your terminal:

```pwsh
[System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','<your-key>','User')
```
# Test commit for manifest generation
