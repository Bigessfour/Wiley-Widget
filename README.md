# Wiley Widget — Municipal Finance Desktop

## Current production UI: Windows Forms + .NET 10

[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![WinForms](https://img.shields.io/badge/UI-Windows%20Forms-blue.svg)](https://docs.microsoft.com/dotnet/desktop/winforms/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Build Status](https://github.com/Bigessfour/Wiley-Widget/actions/workflows/build-winforms.yml/badge.svg)](https://github.com/Bigessfour/Wiley-Widget/actions/workflows/build-winforms.yml)

**Version:** 1.1.0-winforms
**Last Updated:** January 9, 2026
**Framework:** .NET 10.0
**UI Framework:** Windows Forms + Syncfusion WinForms Controls v32.1.19
**Architecture:** N-Tier Layered + MVVM

Fast, stable, zero markup compilation issues. Uses Syncfusion WinForms controls for grids, charts, and data management.

### Syncfusion WinForms v32.1.19

- **ChartControl**: Classic WinForms charting via `Syncfusion.Chart.Windows`
- **SfDataGrid**: Advanced data binding and reporting capabilities
- **DockingManager**: Professional docking panel management
- **SfSkinManager**: Single-source theming system (Office2019Colorful default)
- **Ribbon**: Modern toolbar and menu system with full MVVM support

See: [Syncfusion WinForms Documentation](https://help.syncfusion.com/windowsforms/overview) and [v32.1.19 Release Notes](https://www.nuget.org/packages/Syncfusion.Chart.Windows/32.1.19)

---

## 📑 Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Core Features](#core-features)
- [Technology Stack](#technology-stack)
- [Development](#development)
- [Testing](#testing)
- [QuickBooks Integration](#quickbooks-integration)
- [Configuration & Secrets](#configuration--secrets)
- [Documentation](#documentation)
- [Contributing](#contributing)
- [License](#license)

---

## Overview

WileyWidget is a modern Windows desktop application for municipal budget management and financial analysis. Built with **Windows Forms + .NET 10** and **Syncfusion WinForms v32.1.19**, it features a **clean N-tier layered architecture** with pure MVVM separation, Entity Framework Core integration, and local SQL Server Express data storage.

### Key Capabilities

- **Pure Layered Architecture**: Presentation → Business → Data → Domain layers with clear separation of concerns
- **N-Tier Design**: Models, Data, Business, and UI layers for enterprise-grade maintainability
- **Syncfusion Integration**: DockingManager panels, SfDataGrid data binding, SfSkinManager theming
- **MVVM Pattern**: Complete Windows Forms MVVM implementation with command binding and property change notification
- **Entity Framework Core**: Latest EF Core with SQL Server Express for local data storage
- **Dialog Management**: Service-based dialog system for modal interactions
- **Region-Based Navigation**: View composition and dynamic navigation with plugin architecture
- **Secure Secrets**: DPAPI-encrypted credential storage for API keys and licenses
- **AI Integration**: Microsoft.Extensions.AI with optional xAI Grok integration
- **QuickBooks Online**: OAuth2 integration for financial data synchronization
- **Comprehensive Testing**: Unit, integration, and UI tests with >70% coverage
- **CI/CD Pipeline**: Automated build, test, and release workflows with Trunk integration

### Project Status (v1.1.0)

| Aspect            | Status       | Notes                                   |
| ----------------- | ------------ | --------------------------------------- |
| **Architecture**  | ✅ Stable    | N-tier layered with 2 active modules    |
| **Code Quality**  | ✅ Excellent | 88 legacy files removed, clean codebase |
| **Test Coverage** | ✅ >70%      | Unit, integration, and UI tests         |
| **Documentation** | ✅ Complete  | 51 focused technical documents          |
| **Build Status**  | ✅ Green     | CI/CD with 90% success rate             |
| **.NET Version**  | ✅ v10.0     | Latest LTS framework                    |
| **Syncfusion**    | ✅ v32.1.19  | Latest WinForms components              |

---

## 🚀 Quick Start

### Prerequisites

- **Windows 10/11** (64-bit)
- **.NET 10.0 SDK** (10.0.0 or later)
- **SQL Server Express** (local database)
- **Syncfusion Community License** (free for individual developers)

### Installation & Setup

1. **Clone the Repository**

   ```bash
   git clone https://github.com/Bigessfour/Wiley-Widget.git
   cd Wiley-Widget
   ```

2. **Setup Database**

   ```powershell
   # Run database initialization script
   pwsh ./scripts/setup-database.ps1
   ```

3. **Setup Syncfusion License** (recommended)

   ```powershell
   # Set environment variable for Syncfusion license key
   [System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','YOUR_LICENSE_KEY','User')
   # Alternative: Run setup script
   pwsh ./scripts/setup-license.ps1
   ```

4. **Build and Run**

   ```powershell
   # Restore dependencies and build
   dotnet build WileyWidget.sln

   # Run the application
   dotnet run --project src/WileyWidget/WileyWidget.csproj
   ```

### First Launch

The application will:

- Initialize the local SQL Server Express database (WileyWidgetDev)
- Load default Office2019Colorful theme via SfSkinManager
- Display the main dashboard with budget management interface
- Enable QuickBooks integration (after OAuth setup)

---

## 🏗️ Architecture

### N-Tier Layered Design

WileyWidget follows a **modern N-tier architecture** for enterprise-grade maintainability and scalability:

```
┌─────────────────────────────────────────────────────┐
│  PRESENTATION LAYER (.NET 10.0-windows)            │
│  Windows Forms + Syncfusion Controls + MVVM        │
│  ├── Views (XAML-style Forms)                      │
│  ├── ViewModels (INotifyPropertyChanged)           │
│  └── Dialogs (Service-based modal UI)              │
└────────────────┬────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────┐
│  BUSINESS LOGIC LAYER (.NET 8.0)                   │
│  Application Services + Validation Rules           │
│  ├── Services (Business operations)                │
│  ├── Validators (FluentValidation)                 │
│  └── Domain Models                                 │
└────────────────┬────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────┐
│  DATA ACCESS LAYER (.NET 8.0)                      │
│  Entity Framework Core 9.0 + SQL Server            │
│  ├── AppDbContext (DbContext)                      │
│  ├── Repositories (Repository Pattern)             │
│  └── Migrations (Schema Management)                │
└────────────────┬────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────┐
│  DOMAIN MODEL LAYER (.NET 8.0)                     │
│  Core Business Entities + Value Objects            │
│  ├── MunicipalAccount, Budget, Department          │
│  ├── DTOs (Data Transfer Objects)                  │
│  └── Domain Interfaces                             │
└─────────────────────────────────────────────────────┘
```

### Layer Responsibilities

#### **Presentation Layer** (Windows Forms + Syncfusion)

- User interface with Syncfusion WinForms controls
- MVVM pattern with INotifyPropertyChanged
- DockingManager for panel-based layout
- SfDataGrid for data display and editing
- Service-based dialog management
- Region-based navigation

#### **Business Logic Layer** (Application Services)

- Business rule enforcement
- Cross-cutting concerns (logging, caching, security)
- QuickBooks integration orchestration
- Financial calculations and analysis
- Input validation via FluentValidation

#### **Data Access Layer** (Entity Framework Core)

- AppDbContext configuration
- Repository pattern implementation
- Database migrations and schema management
- Query optimization and relationship management
- Transaction handling

#### **Domain Model Layer** (Core Entities)

- MunicipalAccount, Budget, Department entities
- Value objects and domain logic
- Data annotations for validation
- Owned entity types for composition

### Design Patterns Used

| Pattern                  | Where        | Purpose                        |
| ------------------------ | ------------ | ------------------------------ |
| **MVVM**                 | Presentation | Clean separation of UI logic   |
| **Repository**           | Data         | Abstraction of data access     |
| **Dependency Injection** | All layers   | Loose coupling and testability |
| **Service Locator**      | Business     | Centralized service management |
| **Strategy**             | Business     | Pluggable algorithms           |
| **Command**              | UI           | Encapsulated actions           |
| **Observer**             | MVVM         | Property change notification   |

---

## Core Features

### 📊 Budget Management

- Multi-year budget tracking and allocation
- Department-wise budget assignments
- Budget variance analysis and reporting
- Historical data comparison and trends
- What-if scenario modeling

### 🎨 Modern User Interface

- **Syncfusion SfDataGrid**: Advanced data binding, sorting, filtering, grouping
- **Syncfusion DockingManager**: Professional docking panel layout
- **Office2019Colorful Theme**: Modern Fluent Design integration via SfSkinManager
- **SfChart**: Interactive financial visualizations
- **Ribbon Control**: Professional toolbar and menu system
- **Responsive Layout**: Adapts to window resizing

### 🔗 Enterprise Integration

- **QuickBooks Online**: OAuth2 integration with automatic token refresh
- **Financial Data Sync**: Real-time budget and transaction synchronization
- **Secure Authentication**: DPAPI-encrypted credential storage
- **API Resilience**: Polly retry policies with exponential backoff

### ⚙️ System Features

- **Theme Persistence**: SfSkinManager theme selection saved across sessions
- **User Settings**: Encrypted settings in `%APPDATA%\WileyWidget\settings.json`
- **Comprehensive Logging**: Serilog with rolling daily file output
- **Error Handling**: Global exception handling with user-friendly dialogs
- **Performance Monitoring**: Startup diagnostics and performance tracking

### 🔐 Security Features

- **DPAPI Encryption**: Windows Data Protection API for secret storage
- **Machine-Bound Secrets**: Entropy-protected credential vault
- **SHA-256 Filename Hashing**: Obfuscated secret file names
- **Auto-Migration**: Environment variables automatically migrated to encrypted storage
- **Audit Trail**: All secret operations logged securely

---

## 🔌 QuickBooks Integration

WileyWidget integrates with **QuickBooks Online (QBO)** via OAuth2 for secure financial data synchronization with support for both sandbox and production environments.

### Quick Setup

```powershell
# Set OAuth credentials (from Intuit Developer Portal)
$env:QBO_CLIENT_ID = "your-client-id"
$env:QBO_CLIENT_SECRET = "your-client-secret"
$env:QBO_ENVIRONMENT = "sandbox"  # or "production"

# Run the application
dotnet run --project src/WileyWidget/WileyWidget.csproj
```

### Features

- **OAuth2 Flow**: Complete authorization code flow with PKCE support
- **Token Management**: Automatic refresh before expiry (1-hour access tokens)
- **Data Sync**: Customers, invoices, budgets, and chart of accounts
- **Error Handling**: Graceful failure with automatic retry and circuit breaking
- **Webhook Support**: Optional real-time notifications via Cloudflare Tunnel

### Configuration

**Required Environment Variables:**

- `QBO_CLIENT_ID` - OAuth2 client identifier
- `QBO_CLIENT_SECRET` - OAuth2 client secret (optional for PKCE)

**Optional Environment Variables:**

- `QBO_REALM_ID` - QuickBooks company (realm) ID
- `QBO_REDIRECT_URI` - OAuth2 callback URL (auto-configured)
- `QBO_ENVIRONMENT` - "sandbox" or "production" (default: sandbox)

### Testing

```powershell
# Skip interactive OAuth prompts during tests
$env:WW_SKIP_INTERACTIVE = "1"

# Run QuickBooks integration tests
dotnet test --filter "FullyQualifiedName~QuickBooks"
```

See [QuickBooks Documentation](https://developer.intuit.com/app/developer/qbo/docs/get-started) for detailed setup instructions.

---

## 🔐 Configuration & Secrets

WileyWidget implements **DPAPI-encrypted secret management** using Windows Data Protection API for enterprise-grade credential storage. All sensitive data (API keys, licenses, passwords) are automatically encrypted and machine-bound.

### Secret Vault Architecture

**Storage Location:** `%APPDATA%\WileyWidget\Secrets\`

**Encryption:**

- **Algorithm**: Windows DPAPI with AES-256
- **Scope**: Machine-bound (cannot be copied to other computers)
- **Entropy**: 256-bit cryptographic entropy per secret
- **Hashing**: SHA-256 filename obfuscation

**Secrets Managed:**

- QuickBooks OAuth tokens (access, refresh, realm ID)
- Syncfusion license key
- xAI API key and configuration
- Custom API credentials

### User Interface

**Access via Settings Dialog:**

1. Launch WileyWidget
2. Open Settings (Ctrl+, or gear icon)
3. Navigate to Integration tabs:
   - **QuickBooks**: OAuth credentials and environment
   - **Syncfusion**: License key status
   - **AI/xAI**: API key and model selection

### Automatic Migration

On first launch, environment variables are automatically migrated to encrypted storage:

```powershell
# These are auto-migrated from environment to encrypted vault:
SYNCFUSION_LICENSE_KEY → Syncfusion-LicenseKey.secret
QBO_CLIENT_ID → QuickBooks-ClientId.secret
QBO_CLIENT_SECRET → QuickBooks-ClientSecret.secret
XAI_API_KEY → XAI-ApiKey.secret
```

### Security Guarantees

✅ **Zero Plaintext** - Secrets never stored unencrypted
✅ **Machine-Bound** - Secrets tied to specific computer via DPAPI
✅ **Tamper Detection** - Integrity checks prevent secret modification
✅ **Audit Trail** - All operations logged securely
✅ **Memory Safety** - Sensitive data cleared immediately after use

### Development vs Production

**Development:**

```powershell
# Safe to use environment variables during development
$env:SYNCFUSION_LICENSE_KEY = "community-license-key"
$env:QBO_CLIENT_ID = "sandbox-client-id"
```

**Production:**

```powershell
# NEVER use environment variables in production
# Always use Settings dialog to store secrets in encrypted vault
# Backup %APPDATA%\WileyWidget\Secrets\.entropy for disaster recovery
```

See [DPAPI Security Guide](docs/reference/DPAPI_SECURITY_GUIDE.md) for advanced security configuration.

---

## Technology Stack

### Framework & Runtime

| Component         | Version | Purpose                               |
| ----------------- | ------- | ------------------------------------- |
| **.NET**          | 10.0    | Latest framework with modern features |
| **EF Core**       | 9.0.8   | Data access and ORM                   |
| **SQL Server**    | Express | Local database                        |
| **Windows Forms** | .NET 10 | Desktop UI framework                  |

### Syncfusion Components (v32.1.19)

| Component          | Use Case                                 |
| ------------------ | ---------------------------------------- |
| **SfDataGrid**     | Advanced data binding and display        |
| **ChartControl**   | Financial visualizations                 |
| **DockingManager** | Panel-based UI layout                    |
| **SfSkinManager**  | Centralized theming (Office2019Colorful) |
| **Ribbon**         | Modern toolbar and menu                  |
| **AutoComplete**   | Search and lookup                        |

### Infrastructure & Services

| Library                     | Version | Purpose                        |
| --------------------------- | ------- | ------------------------------ |
| **DryIoc**                  | 9.0.537 | Dependency injection container |
| **Polly**                   | 8.6.4   | Resilience and retry policies  |
| **Serilog**                 | 4.3.0   | Structured logging             |
| **FluentValidation**        | 12.1.0  | Business rule validation       |
| **Microsoft.Extensions.AI** | Latest  | AI integration framework       |

### Testing Frameworks

| Framework            | Version | Purpose                 |
| -------------------- | ------- | ----------------------- |
| **xUnit**            | 2.9.2   | Unit testing            |
| **Moq**              | 4.20.70 | Mocking framework       |
| **FluentAssertions** | 7.0.0   | Assertion library       |
| **FlaUI**            | 4.0.0   | UI automation testing   |
| **TestContainers**   | 4.2.0   | Isolated test databases |

---

## Development

### Build & Run

```powershell
# Build entire solution
dotnet build WileyWidget.sln

# Run specific project
dotnet run --project src/WileyWidget/WileyWidget.csproj

# Build with verbose output
dotnet build WileyWidget.sln -v diagnostic

# Release build
dotnet build WileyWidget.sln -c Release
```

### Development Scripts

```powershell
# Comprehensive build + test + coverage
pwsh ./scripts/build.ps1

# Build with UI tests included
RUN_UI_TESTS=1 pwsh ./scripts/build.ps1

# Publish as self-contained executable
pwsh ./scripts/build.ps1 -Publish -SelfContained -Runtime win-x64

# Setup database for local development
pwsh ./scripts/setup-database.ps1

# Setup Syncfusion license
pwsh ./scripts/setup-license.ps1
```

### Code Standards

**Language & Style:**

- **C#**: Modern C# 13 features (net10.0 target)
- **Naming**: PascalCase for public, camelCase for private
- **Comments**: XML docs for public API, rationale comments for complex logic
- **Formatting**: .editorconfig enforced (4 spaces, 120 char line limit)

**Architecture Patterns:**

- **Dependency Injection**: DryIoc for IoC container
- **Repository Pattern**: Abstraction for data access
- **Service Layer**: Business logic encapsulation
- **MVVM**: Clear presentation layer separation

**Security:**

- **No Secrets in Code**: Use environment variables or encrypted vault
- **Input Validation**: Server-side business validation always
- **SQL Injection Prevention**: Use parameterized EF Core queries
- **DPAPI Encryption**: All sensitive data encrypted at rest

---

## 🧪 Testing

### Testing Architecture

WileyWidget implements a **comprehensive multi-layer testing strategy**:

#### **Unit Tests** (`WileyWidget.Tests/`)

- Framework: xUnit 2.9.2 + Moq 4.20.70
- Coverage: Business logic, ViewModels, Services
- Execution: <100ms per test
- Target: 80% line coverage

**Key Test Files:**

- `MainViewModelTests.cs` - Main application logic
- `AIAssistViewModelTests.cs` - AI features
- `EnterpriseRepositoryTests.cs` - Data access

#### **Integration Tests** (`WileyWidget.IntegrationTests/`)

- Framework: xUnit + TestContainers 4.2.0
- Coverage: Database operations, EF Core relationships
- Execution: <30 seconds with isolated SQL Server instances
- Target: All critical database paths

**Test Categories:**

- Relationship tests (foreign keys, cascades)
- Performance tests (query optimization)
- Concurrency tests (multi-user scenarios)
- Migration tests (schema changes)

#### **UI Tests** (`WileyWidget.UiTests/`)

- Framework: xUnit + FlaUI 4.0.0
- Coverage: Form interactions, DataGrid operations, dialogs
- Execution: 3-8 minutes with automated cleanup
- Target: Critical user workflows

**Test Types:**

- Component tests (individual controls)
- Workflow tests (multi-step scenarios)
- Accessibility tests (keyboard navigation)
- Theme tests (UI consistency)

### Running Tests

```powershell
# Run all tests
dotnet test WileyWidget.sln

# Run specific test file
dotnet test WileyWidget.Tests/MainViewModelTests.cs

# Run with coverage
dotnet test WileyWidget.sln --collect:"XPlat Code Coverage"

# Run filtered tests
dotnet test --filter "Category=Unit"

# Run UI tests only
RUN_UI_TESTS=1 dotnet test WileyWidget.UiTests/

# Generate coverage report
dotnet test WileyWidget.sln --collect:"XPlat Code Coverage"
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:Html
```

### Coverage Target

**Minimum Coverage:** 70% (enforced in CI/CD)
**Target Coverage:** 85% for business-critical code
**Measurement:** Code coverage collected via XPlat Code Coverage

### Continuous Integration

Tests run automatically on:

- Every pull request
- Main branch merges
- Release builds
- Manual CI/CD triggering

See `.github/workflows/` for CI/CD pipeline configuration.

---

## Logging & Diagnostics

### Logging System

**Framework:** Serilog 4.3.0 with structured logging

**Output Locations:**

- **Application Logs**: `%APPDATA%\WileyWidget\logs\app-YYYYMMDD.log`
- **Startup Logs**: `logs/startup-YYYYMMDD.txt`
- **Critical Errors**: `logs/critical-startup-failures.log`

**Log Levels:**

- `DEBUG` - Detailed diagnostic information
- `INFO` - Application lifecycle events
- `WARNING` - Potential issues
- `ERROR` - Recoverable errors
- `FATAL` - Unrecoverable errors

**Structured Data:**

```csharp
_logger.LogInformation("QuickBooks sync completed",
    new { SyncedCount = 42, Duration = "5.2s", Status = "Success" });
```

### Diagnostic Tools

```powershell
# View application logs
explorer %AppData%\WileyWidget\logs

# Watch logs in real-time
Get-Content %AppData%\WileyWidget\logs\app-*.log -Wait

# Check Syncfusion license registration
pwsh ./scripts/show-syncfusion-license.ps1 -Watch

# Generate build diagnostics
dotnet build WileyWidget.sln /bl:msbuild.binlog
# Open with MSBuild Structured Log Viewer
```

---

## Project Structure

```text
WileyWidget/                           # Solution root
├── src/
│   ├── WileyWidget/                   # 🖥️ Presentation Layer (.NET 10.0-windows)
│   │   ├── Views/                     # Windows Forms + Syncfusion controls
│   │   ├── ViewModels/                # MVVM ViewModels
│   │   ├── Dialogs/                   # Service-based dialog system
│   │   ├── App.xaml.cs                # Bootstrapper (6 partial classes)
│   │   ├── Program.cs                 # Entry point
│   │   └── WileyWidget.csproj
│   ├── WileyWidget.Business/          # 💼 Business Logic Layer (.NET 8.0)
│   │   ├── Services/                  # Application services
│   │   ├── Validators/                # FluentValidation rules
│   │   ├── Interfaces/                # Service contracts
│   │   └── WileyWidget.Business.csproj
│   ├── WileyWidget.Data/              # 🗄️ Data Access Layer (.NET 8.0)
│   │   ├── AppDbContext.cs            # EF Core DbContext
│   │   ├── Repositories/              # Repository implementations
│   │   ├── Migrations/                # Schema migrations
│   │   └── WileyWidget.Data.csproj
│   └── WileyWidget.Models/            # 📋 Domain Models (.NET 8.0)
│       ├── MunicipalAccount.cs        # Core entities
│       ├── Budget.cs
│       ├── Department.cs
│       ├── DTOs/                      # Data transfer objects
│       └── WileyWidget.Models.csproj
├── tests/
│   ├── WileyWidget.Tests/             # ✅ Unit tests (xUnit)
│   ├── WileyWidget.IntegrationTests/  # 🔗 Integration tests
│   ├── WileyWidget.UiTests/           # 🎭 UI tests (FlaUI)
│   └── WileyWidget.WinForms.Tests/    # 🖼️ WinForms component tests
├── scripts/                            # 24 essential automation scripts
├── docs/                              # 51 technical documentation files
├── .github/workflows/                 # CI/CD pipelines
└── WileyWidget.sln                    # Solution file
```

---

## 📚 Documentation

### Getting Started

- [Quick Start Guide](docs/QUICK_START.md)
- [Installation Guide](docs/INSTALLATION.md)
- [Database Setup](docs/DATABASE_SETUP.md)

### Architecture & Design

- [Architecture Overview](docs/ARCHITECTURE.md)
- [Layered Design Patterns](docs/LAYERED_ARCHITECTURE.md)
- [MVVM Implementation](docs/MVVM_PATTERNS.md)

### Development & Testing

- [Development Guide](docs/DEVELOPMENT_GUIDE.md)
- [Testing Strategy](docs/TESTING.md)
- [Contributing Guidelines](CONTRIBUTING.md)

### Integration & Features

- [QuickBooks Integration](docs/QUICKBOOKS_INTEGRATION.md)
- [Secret Management](docs/SECRET_MANAGEMENT.md)
- [Syncfusion Components](docs/SYNCFUSION_COMPONENTS.md)

### CI/CD & Deployment

- [Build & Release](docs/BUILD_AND_RELEASE.md)
- [CI/CD Pipeline](docs/CICD_PIPELINE.md)
- [Deployment Guide](docs/DEPLOYMENT.md)

### Advanced Topics

- [Performance Optimization](docs/PERFORMANCE_OPTIMIZATION.md)
- [Security Best Practices](docs/SECURITY_BEST_PRACTICES.md)
- [Troubleshooting Guide](docs/TROUBLESHOOTING.md)

---

## 🤝 Contributing

We welcome contributions to WileyWidget! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for:

- Development workflow and branching strategy
- Code style and standards (C#, PowerShell)
- Testing requirements and coverage targets
- Commit message conventions
- Pull request process

**Quick Checklist Before Submitting PR:**

- [ ] Code builds cleanly (`dotnet build WileyWidget.sln`)
- [ ] Unit tests pass (`dotnet test`)
- [ ] Code follows style guidelines
- [ ] No secrets or sensitive data in commits
- [ ] Documentation updated if needed
- [ ] Changelog entry added

---

## 📋 Version History

### v1.1.0 (January 2026)

- ✅ Updated to .NET 10.0
- ✅ Upgraded Syncfusion WinForms to v32.1.19
- ✅ Comprehensive README rewrite
- ✅ Enhanced architecture documentation
- ✅ Improved testing infrastructure

### v1.0.0 (November 2025)

- ✅ N-tier layered architecture stabilized
- ✅ 88 legacy files removed
- ✅ 71% script reduction
- ✅ Pure MVVM implementation
- ✅ Full test coverage (>70%)

### v0.2.0 (October 2025)

- Code cleanup and architecture refactor
- Dead code elimination
- Documentation consolidation

### v0.1.0 (Initial Release)

- Core scaffold with Syncfusion integration
- Basic MVVM implementation
- Unit test framework

---

## 📄 License

This project is licensed under the MIT License - see [LICENSE](LICENSE) file for details.

---

## 🎯 Roadmap & Next Steps

### Planned Enhancements

- [ ] Advanced AI insights via Microsoft.Extensions.AI
- [ ] Real-time webhook notifications from QuickBooks
- [ ] Automated financial forecasting engine
- [ ] Multi-user collaboration features
- [ ] Mobile companion app
- [ ] Cloud deployment support

### Community & Support

- **GitHub Issues**: [Report bugs or request features](https://github.com/Bigessfour/Wiley-Widget/issues)
- **Discussions**: [Ask questions and share ideas](https://github.com/Bigessfour/Wiley-Widget/discussions)
- **Wiki**: [Community documentation](https://github.com/Bigessfour/Wiley-Widget/wiki)

---

## 🙏 Acknowledgments

WileyWidget is built on the shoulders of excellent open-source projects:

- **Windows Forms** - Microsoft's mature desktop framework
- **Syncfusion** - Professional UI components for WinForms
- **Entity Framework Core** - Microsoft's ORM framework
- **Serilog** - Structured logging for .NET
- **Polly** - Resilience and chaos engineering
- **xUnit** - Modern unit testing framework
- **And many more** - See [NuGet dependencies](NuGet.config)

---

**Made with ❤️ for municipal finance teams everywhere**

For questions or feedback, please open an [issue](https://github.com/Bigessfour/Wiley-Widget/issues) or [discussion](https://github.com/Bigessfour/Wiley-Widget/discussions).
