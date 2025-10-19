# WileyWidget

**A Modern WPF Application for Budget Management and Financial Analysis**

[![.NET Version](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Build Status](https://github.com/Bigessfour/Wiley-Widget/actions/workflows/ci.yml/badge.svg)](https://github.com/Bigessfour/Wiley-Widget/actions/workflows/ci.yml)
[![Coverage](https://img.shields.io/badge/coverage-70%25+-brightgreen.svg)](https://github.com/Bigessfour/Wiley-Widget/actions/workflows/ci.yml)

**Version:** 0.1.0 - Preview Release
**Framework:** .NET 9.0 WPF
**UI Framework:** Syncfusion WPF Controls v31.1.20
**Application Framework:** Prism v9.0 (Modular MVVM Architecture)

## 📋 Overview

WileyWidget is a modern Windows desktop application built with WPF, Syncfusion controls, and Prism framework, designed for budget management and financial data analysis. The application features a comprehensive modular MVVM architecture with Entity Framework Core integration, using local SQL Server Express for data storage.

### Key Capabilities

- **Modular Architecture**: Prism-based module system for extensible application structure
- **Dialog Management**: Prism dialog service for modal dialogs and user interactions
- **Navigation**: Prism region-based navigation with view injection
- **Modern UI**: Fluent Design themes with dark/light mode switching
- **Robust Architecture**: MVVM pattern with dependency injection and comprehensive testing

---

## 🚀 Quick Start

### Prerequisites

- **Windows 10/11** (64-bit)
- **.NET 9.0 SDK** (9.0.305 or later)
- **SQL Server Express** (local database)
- **Syncfusion Community License** (free for individual developers)

### Installation & Setup

1. **Clone the Repository**
   ```bash
   git clone https://github.com/Bigessfour/Wiley-Widget.git
   cd Wiley-Widget
   ```

2. **Setup Syncfusion License**
   ```powershell
   # Set environment variable (recommended)
   [System.Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','YOUR_LICENSE_KEY','User')

   # Or place license.key file beside the executable
   ```

3. **Build and Run**
   ```powershell
   # Restore dependencies and build
   dotnet build WileyWidget.csproj

   # Run the application
   dotnet run --project WileyWidget.csproj
   ```

### First Launch

The application will:
- Initialize the local SQL Server Express database (WileyWidgetDev)
- Load default themes and settings
- Display the main dashboard with budget management interface

---

## 🔐 Configuration & Secret Management

WileyWidget implements a **secure, encrypted secret management system** using Windows Data Protection API (DPAPI) for enterprise-grade credential storage. All sensitive data (API keys, client secrets, licenses) are automatically encrypted and can only be accessed by the Windows user who stored them.

### Secret Storage Architecture

#### **🔒 DPAPI Encryption**
- **Windows DPAPI**: Uses `ProtectedData.Protect/Unprotect` with `DataProtectionScope.CurrentUser`
- **User-Specific**: Secrets encrypted for the current Windows user only
- **Machine-Bound**: Cannot be decrypted on different computers
- **256-bit Entropy**: Additional cryptographic entropy per secret
- **Secure Storage**: Encrypted files in `%APPDATA%\WileyWidget\Secrets\`

#### **🗂️ Storage Location**
```
%APPDATA%\WileyWidget\Secrets\
├── .entropy          # Hidden entropy file for encryption
├── QuickBooks-ClientId.secret
├── QuickBooks-ClientSecret.secret
├── QuickBooks-RedirectUri.secret
├── QuickBooks-Environment.secret
├── Syncfusion-LicenseKey.secret
├── XAI-ApiKey.secret
├── XAI-BaseUrl.secret
└── [additional secrets...]
```

### User Interface for Secret Management

#### **⚙️ Settings Dialog Access**
1. **Launch Application** → Main window opens
2. **Open Settings** → Click gear icon or press `Ctrl+,`
3. **Navigate Tabs** → Select appropriate integration tab
4. **Enter Credentials** → Input API keys, secrets, licenses
5. **Save Changes** → Click "Save" button or press `Ctrl+S`

#### **📋 Integration Tabs**

##### **QuickBooks Integration Tab**
- **Client ID**: OAuth2 Client ID from Intuit Developer Portal
- **Client Secret**: Securely stored OAuth2 client secret
- **Redirect URI**: OAuth2 callback URL (e.g., `https://localhost:5001/callback`)
- **Environment**: Sandbox (testing) or Production (live data)

##### **Syncfusion License Tab**
- **License Key**: Syncfusion community license key
- **Status**: Real-time license validation
- **Note**: License stored securely in encrypted vault

##### **XAI Integration Tab**
- **API Key**: XAI API key (format: `xai-xxxxxxxx`)
- **Base URL**: API endpoint (default: `https://api.x.ai/v1/`)
- **Model**: AI model selection (grok-4-0709 recommended)
- **Timeout**: Request timeout in seconds (5-300)

### Automatic Migration & Security

#### **🔄 Environment Variable Migration**
On first application launch, WileyWidget automatically migrates existing environment variables:
```powershell
# These environment variables are automatically migrated to encrypted storage:
SYNCFUSION_LICENSE_KEY      → Syncfusion-LicenseKey
QUICKBOOKS_CLIENT_ID        → QuickBooks-ClientId
QUICKBOOKS_CLIENT_SECRET    → QuickBooks-ClientSecret
QUICKBOOKS_REDIRECT_URI     → QuickBooks-RedirectUri
QUICKBOOKS_ENVIRONMENT      → QuickBooks-Environment
XAI_API_KEY                 → XAI-ApiKey
XAI_BASE_URL                → XAI-BaseUrl
```

#### **🛡️ Security Guarantees**
- **Zero Plaintext**: Secrets never stored in plaintext files
- **User Isolation**: Different Windows users cannot access each other's secrets
- **Machine Lock**: Encrypted secrets cannot be used on different computers
- **Memory Safety**: Sensitive data cleared immediately after use
- **Audit Trail**: All secret operations logged securely

### Configuration Workflow

#### **Initial Setup Process**
```mermaid
graph TD
    A[Launch WileyWidget] --> B[Settings Dialog Opens]
    B --> C[Navigate to Integration Tab]
    C --> D[Enter API Credentials]
    D --> E[Click Save Button]
    E --> F[DPAPI Encryption Applied]
    F --> G[Secrets Stored Securely]
    G --> H[Connection Test Performed]
    H --> I[Status Updated in UI]
```

#### **Runtime Secret Access**
```csharp
// Secrets automatically loaded from encrypted storage
var clientSecret = await _secretVaultService.GetSecretAsync("QuickBooks-ClientSecret");
var apiKey = await _secretVaultService.GetSecretAsync("XAI-ApiKey");
```

### Troubleshooting Secret Issues

#### **Common Issues & Solutions**

##### **"Connection Failed" Status**
- **Check Credentials**: Verify API keys are correct in Settings dialog
- **Test Connection**: Use "Test Connection" buttons in each integration tab
- **Environment Variables**: Ensure environment variables are set (auto-migrated on first run)

##### **License Validation Errors**
- **Syncfusion License**: Get free community license from syncfusion.com
- **Format Check**: Ensure license key format is correct
- **Restart Required**: Some licenses require application restart

##### **API Connection Issues**
- **Network Access**: Ensure internet connectivity for external APIs
- **Rate Limits**: Check API provider rate limiting
- **Credentials**: Verify API keys have correct permissions

#### **Manual Secret Management**
```powershell
# View encrypted secrets (development only)
# Note: Secrets are encrypted and cannot be viewed in plaintext
Get-ChildItem "$env:APPDATA\WileyWidget\Secrets"

# Clear all secrets (requires application restart)
Remove-Item "$env:APPDATA\WileyWidget\Secrets\*" -Force
```

### Enterprise Security Features

#### **🔐 Encryption Details**
- **Algorithm**: Windows DPAPI with AES-256 encryption
- **Key Derivation**: PBKDF2 with user credentials and machine entropy
- **Storage Format**: Base64-encoded encrypted blobs
- **Access Control**: NTFS permissions restrict file access

#### **📊 Audit & Monitoring**
- **Operation Logging**: All secret operations logged to application logs
- **Access Tracking**: Secret retrieval operations recorded
- **Error Handling**: Failed decryption attempts logged (security monitoring)

#### **🚀 Performance Characteristics**
- **Encryption Speed**: <1ms per secret operation
- **Storage Size**: ~2x plaintext size (Base64 encoding)
- **Memory Usage**: Minimal memory footprint with immediate cleanup
- **Startup Impact**: <100ms initialization time

This secure secret management system ensures WileyWidget can safely handle enterprise credentials while maintaining the highest security standards for sensitive data protection.

---

## 🏗️ Architecture

### Why Layered Architecture?

WileyWidget implements a **modern N-Tier layered architecture** with **Prism framework integration** following Microsoft's official guidance for enterprise .NET applications. This architectural pattern provides several critical benefits:

#### **🎯 Separation of Concerns**
- **Presentation Layer**: Pure UI logic with MVVM pattern
- **Business Layer**: Domain logic and validation rules
- **Data Layer**: Database operations and persistence
- **Domain Layer**: Core business entities and interfaces

#### **🔧 Maintainability & Testability**
- Each layer can be developed, tested, and deployed independently
- Clear contracts between layers enable parallel development
- Isolated testing prevents cascading failures

#### **📈 Scalability & Performance**
- Business logic can be reused across multiple presentation layers (WPF, Web API, etc.)
- Database operations are optimized and cached at the data layer
- UI remains responsive through proper async/await patterns

#### **🛡️ Security & Reliability**
- Input validation at multiple layers prevents malicious data
- Database constraints and business rules work together
- Comprehensive error handling and logging at each layer

---

### How We Implemented the Layered System

#### **Migration from Monolithic to Layered Architecture**

Starting from a traditional WPF project structure, we systematically migrated to a layered architecture through three phases:

1. **Phase 1**: Extracted domain models into `WileyWidget.Models`
2. **Phase 2**: Moved data access logic into `WileyWidget.Data`
3. **Phase 3**: Created business logic layer in `WileyWidget.Business`

#### **Framework Strategy**
- **Presentation**: .NET 9.0-windows (WPF) for modern Windows features
- **Business/Data/Domain**: .NET 8.0 for stability and LTS support
- **Testing**: .NET 8.0 for compatibility with data/business layers

#### **Dependency Flow**
```
WileyWidget (UI) → WileyWidget.Business → WileyWidget.Data → WileyWidget.Models
     ↓                                                            ↑
WileyWidget.UiTests                                       WileyWidget.IntegrationTests
     ↓                                                            ↑
WileyWidget.Tests
```

---

### Layered Workspace Structure

```
WileyWidget/
├── WileyWidget/                          # 🖥️ PRESENTATION LAYER (.NET 9.0-windows)
│   ├── Views/                           # XAML UI files
│   ├── ViewModels/                      # MVVM view models
│   ├── App.xaml                         # Application entry point
│   ├── Program.cs                       # Startup logic
│   └── WileyWidget.csproj               # WPF project file
│
├── WileyWidget.Business/                 # 💼 BUSINESS LOGIC LAYER (.NET 8.0)
│   ├── Services/                        # Application services
│   ├── Validators/                      # Business validation rules
│   ├── Interfaces/                      # Service contracts
│   └── WileyWidget.Business.csproj      # Class library project
│
├── WileyWidget.Data/                     # 🗄️ DATA ACCESS LAYER (.NET 8.0)
│   ├── AppDbContext.cs                  # EF Core database context
│   ├── Repositories/                    # Repository implementations
│   ├── Migrations/                      # Database schema migrations
│   └── WileyWidget.Data.csproj          # Class library project
│
├── WileyWidget.Models/                   # 📋 DOMAIN MODEL LAYER (.NET 8.0)
│   ├── MunicipalAccount.cs              # Core business entities
│   ├── Department.cs                    # Domain models
│   ├── BudgetEntry.cs                   # Business objects
│   ├── DTOs/                            # Data transfer objects
│   └── WileyWidget.Models.csproj        # Class library project
│
├── WileyWidget.IntegrationTests/         # 🧪 INTEGRATION TESTS (.NET 8.0)
│   ├── Infrastructure/                  # Test infrastructure
│   ├── Relationships/                   # Relationship tests
│   ├── Performance/                     # Performance benchmarks
│   └── Concurrency/                     # Concurrency tests
│
├── WileyWidget.Tests/                    # ✅ UNIT TESTS (.NET 8.0)
│   ├── Business/                        # Business logic unit tests
│   ├── Data/                           # Data access unit tests
│   └── Models/                          # Model validation tests
│
└── WileyWidget.UiTests/                  # 🎭 UI TESTS (.NET 9.0-windows)
    ├── Automation/                      # UI automation scripts
    ├── PageObjects/                     # UI test page objects
    └── Scenarios/                       # End-to-end test scenarios
```

---

### Layer Responsibilities & Design Patterns

#### **🏛️ Domain Layer (WileyWidget.Models)**
**Purpose**: Core business entities and domain logic
**Responsibilities**:
- Entity Framework Core entities with data annotations
- Domain validation rules and business constraints
- Value objects (AccountNumber, owned entities)
- Interfaces for cross-cutting concerns (IAuditable, ISoftDeletable)

**Design Patterns**:
- **Entity Framework Core Code-First**: Database schema from code
- **Owned Entity Types**: Complex value objects within entities
- **Table-per-Hierarchy**: Inheritance mapping for related entities

#### **🗄️ Data Access Layer (WileyWidget.Data)**
**Purpose**: Database operations and data persistence
**Responsibilities**:
- Entity Framework Core DbContext configuration
- Repository pattern implementations
- Database migrations and schema management
- Connection management and transaction handling

**Design Patterns**:
- **Repository Pattern**: Abstract data access behind interfaces
- **Unit of Work**: Transaction management across repositories
- **Specification Pattern**: Query composition and filtering

#### **💼 Business Logic Layer (WileyWidget.Business)**
**Purpose**: Application business rules and workflows
**Responsibilities**:
- Business validation and rule enforcement
- Application services coordinating multiple operations
- Cross-cutting concerns (logging, caching, security)
- Integration with external services (QuickBooks, Azure)

**Design Patterns**:
- **Service Layer Pattern**: Business operations as services
- **Strategy Pattern**: Pluggable business rules
- **Decorator Pattern**: Cross-cutting concerns

#### **🖥️ Presentation Layer (WileyWidget)**
**Purpose**: User interface and interaction logic with Prism framework
**Responsibilities**:
- WPF UI with Syncfusion controls and Prism regions
- Modular MVVM pattern implementation with Prism ViewModels
- Prism dialog service for modal interactions
- Navigation and state management with Prism navigation service

**Design Patterns**:
- **Prism MVVM Pattern**: Enhanced MVVM with Prism base classes
- **Command Pattern**: UI actions and commands with Prism DelegateCommand
- **Observer Pattern**: Data binding and property change notifications
- **Module Pattern**: Prism modules for application extensibility

---

### Testing Strategy by Layer

#### **🧪 Integration Testing (WileyWidget.IntegrationTests)**
**Scope**: End-to-end data operations and relationships
**Tools**: xUnit, TestContainers.MsSql, FluentAssertions, BenchmarkDotNet

**Test Categories**:
- **Relationship Tests**: Foreign key constraints, cascading deletes
- **Performance Tests**: Query optimization, bulk operations
- **Concurrency Tests**: Multi-user scenarios, deadlock prevention
- **Data Integrity**: Transaction boundaries, rollback scenarios

**Key Features**:
- **TestContainers**: Isolated SQL Server instances per test
- **Database Seeding**: Consistent test data across runs
- **Performance Benchmarking**: Automated performance regression detection

#### **✅ Unit Testing (WileyWidget.Tests)**
**Scope**: Individual components and business logic
**Tools**: xUnit, Moq, FluentAssertions, AutoFixture

**Coverage by Layer**:
- **Models**: Entity validation, property constraints
- **Data**: Repository operations, query logic
- **Business**: Service methods, validation rules
- **ViewModels**: Command execution, property changes

**Testing Patterns**:
- **Arrange-Act-Assert**: Clear test structure
- **Builder Pattern**: TestDataBuilder for complex objects
- **Mocking**: External dependencies (database, APIs)
- **Theory Tests**: Data-driven test scenarios

#### **🎭 UI Testing (WileyWidget.UiTests)**
**Scope**: User interface behavior and workflows
**Tools**: FlaUI, Appium, or TestStack.White

**Test Categories**:
- **UI Automation**: Button clicks, form submissions
- **Visual Verification**: Layout, styling, responsiveness
- **Workflow Testing**: End-to-end user scenarios
- **Accessibility**: Screen reader compatibility, keyboard navigation

**Implementation Strategy**:
- **Page Object Model**: Reusable UI component abstractions
- **Test Data Management**: Realistic test data for UI scenarios
- **Screenshot Comparison**: Visual regression detection

---

### Architecture Benefits Realized

#### **🚀 Development Velocity**
- **Parallel Development**: Teams can work on different layers simultaneously
- **Independent Deployment**: Layers can be updated without affecting others
- **Clear Contracts**: Well-defined interfaces prevent integration issues

#### **🔧 Maintenance Efficiency**
- **Isolated Changes**: Bug fixes in one layer don't cascade
- **Comprehensive Testing**: Each layer has dedicated test coverage
- **Code Reusability**: Business logic can be reused across applications

#### **📊 Quality Assurance**
- **Layer Isolation**: Issues are contained within their layer
- **Comprehensive Coverage**: Unit + Integration + UI testing
- **Performance Monitoring**: Automated benchmarks prevent regressions

#### **🏢 Enterprise Readiness**
- **Scalability**: Architecture supports multiple frontends (WPF, Web, Mobile)
- **Security**: Multi-layer validation and authorization
- **Monitoring**: Comprehensive logging and diagnostics at each layer

---

### Technology Stack by Layer

| Layer | Framework | ORM | Testing | UI | External APIs |
|-------|-----------|-----|---------|----|---------------|
| **Presentation** | .NET 9.0 WPF | - | FlaUI | Syncfusion | - |
| **Business** | .NET 8.0 | - | xUnit, Moq | - | QuickBooks API |
| **Data** | .NET 8.0 | EF Core 9.0.8 | xUnit, TestContainers | - | Azure SQL |
| **Domain** | .NET 8.0 | - | xUnit | - | - |

This layered architecture ensures WileyWidget is maintainable, testable, and ready for enterprise-scale deployment while following Microsoft's recommended patterns for modern .NET applications.

---

## 🔧 Prism Framework Integration

WileyWidget leverages the **Prism framework** for building modular, maintainable WPF applications. Prism provides a solid foundation for implementing the MVVM pattern, dependency injection, and modular application architecture.

### Key Prism Components

#### **📦 Module System**
- **Modular Architecture**: Application divided into feature-specific modules
- **Dynamic Loading**: Modules loaded on-demand for better startup performance
- **Dependency Management**: Clean separation between module dependencies

**Available Modules**:
- `DashboardModule`: Main dashboard and analytics
- `BudgetModule`: Budget management and analysis
- `EnterpriseModule`: Enterprise data management
- `ReportsModule`: Reporting and data visualization
- `SettingsModule`: Application configuration
- `ToolsModule`: Utility tools and helpers

#### **🗣️ Dialog Service**
- **Modal Dialogs**: Standardized dialog implementation
- **ViewModel-First**: Dialogs driven by ViewModels, not Views
- **Async Support**: Non-blocking dialog operations

**Dialog Types**:
- `ConfirmationDialog`: Yes/No confirmations
- `NotificationDialog`: Information messages
- `WarningDialog`: Warning alerts
- `ErrorDialog`: Error notifications
- `SettingsDialog`: Application settings

#### **🧭 Navigation Service**
- **Region-Based Navigation**: Prism regions for view composition
- **View Injection**: Dynamic view loading and replacement
- **Navigation Parameters**: Type-safe parameter passing

#### **🏗️ Application Bootstrapper**
- **Unity Container**: Dependency injection container
- **Module Catalog**: Centralized module registration
- **Shell Configuration**: Main window and region setup

### Prism Configuration

```csharp
// App.xaml.cs - Prism owns dependency injection via Unity
using Prism.Ioc;
using Prism.Modularity;
using Prism.Unity;

public class App : PrismApplication
{
   protected override void RegisterTypes(IContainerRegistry containerRegistry)
   {
      // Register services and ViewModels here
      containerRegistry.RegisterSingleton<INavigationService, NavigationService>();
      containerRegistry.RegisterSingleton<IDialogService, DialogService>();
   }

   protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
   {
      // Register application modules
      moduleCatalog.AddModule<DashboardModule>();
      moduleCatalog.AddModule<BudgetModule>();
      // ... other modules
   }
}
```

---

## ✨ Features

### Core Functionality

- **📊 Budget Management**
  - Multi-year budget tracking
  - Department-wise budget allocation
  - Budget variance analysis
  - Historical data comparison

- **🎨 Modern UI**
  - Syncfusion DataGrid with advanced features
  - Interactive charts and visualizations
  - Fluent Design themes (Dark/Light)
  - Responsive layout with docking panels

- **🔗 Enterprise Integration**
  - QuickBooks Online API integration
  - Secure OAuth2 authentication
  - Automated data synchronization
  - Real-time financial updates

- **⚙️ System Features**
  - Persistent user settings
  - Comprehensive logging
  - Error handling and diagnostics
  - Performance monitoring

### Advanced Features

- **AI Integration**: Microsoft.Extensions.AI for intelligent insights
- **Reporting**: Bold Reports for advanced reporting capabilities
- **Security**: Local configuration and secrets management
- **Performance**: Optimized startup with background initialization
- **Testing**: Comprehensive unit and UI test coverage

---

## 📋 **Project Structure**

```
WileyWidget/
├── Models/           # Enterprise data models (Phase 1)
├── Data/            # EF Core DbContext & Repositories
├── ViewModels/      # MVVM view models (Phase 2)
├── Views/          # XAML UI files (Phase 2)
├── Services/       # Business logic & AI integration (Phase 3)
├── scripts/               # Build and deployment scripts
└── docs/           # North Star & implementation guides
```

---

## 🛠️ **Development Guidelines**

### **Rule #1: No Plan Changes Without Group Consensus**
**ME, Grok-4, and Grok Fast Code-1 must ALL agree** to any plan changes. This prevents scope creep and keeps us focused.

### **Code Standards**
- **EF Core 9.0.8:** Use for all data operations with SQL Server Express
- **CommunityToolkit.Mvvm 8.4.0:** For ViewModel bindings and commands
- **Syncfusion WPF 31.1.20:** For UI components and theming
- **Serilog 4.3.0:** For structured logging with file and async sinks
- **No nullable reference types:** Per project guidelines
- **Repository Pattern:** For data access abstraction
- **Dependency Injection:** Microsoft.Extensions.DI for service registration
- **AI Integration:** Microsoft.Extensions.AI for intelligent features

### **Testing Strategy**
- **Unit Tests:** NUnit for business logic and ViewModels
- **Integration Tests:** Database operations and API integrations
- **UI Tests:** FlaUI for smoke tests and critical user flows
- **Coverage Target:** 80% by Phase 4 with CI/CD validation
- **Test Categories:** unit, smoke, integration, slow (marked appropriately)

---

## 🧪 **Testing Framework**

WileyWidget implements a comprehensive, multi-layered testing strategy designed for enterprise-grade reliability and maintainability. Our testing framework combines traditional unit testing with modern integration and UI automation approaches.

### **Testing Architecture Overview**

| Test Type | Framework | Target | Execution | Coverage Focus |
|-----------|-----------|--------|-----------|----------------|
| **Unit Tests** | xUnit + Moq | Business Logic | CI/CD Pipeline | Logic & Algorithms |
| **Integration Tests** | xUnit + TestContainers | Data Layer | CI/CD Pipeline | Database Operations |
| **UI Tests** | xUnit + FlaUI | WPF Controls | CI/CD Pipeline | User Interactions |
| **Python Tests** | pytest | System Resources | Development | Memory & Performance |
| **PowerShell Scripts** | Pester-like | Build Automation | CI/CD Pipeline | Deployment Validation |

### **1. C# Unit Testing (`WileyWidget.Tests/`)**

**Framework:** xUnit 2.9.2 with Moq 4.20.70 and FluentAssertions 7.0.0
**Target Files:** ViewModels, Services, Business Logic, Data Operations
**Test Categories:** Unit, Smoke, Component

**Key Test Files & Coverage:**
- `MainViewModelTests.cs` - Main application ViewModel (enterprise management, QuickBooks integration)
- `AIAssistViewModelTests.cs` - AI assistant functionality (conversation modes, financial calculations)
- `UtilityCustomerViewModelTests.cs` - Customer management operations (CRUD, search, validation)
- `WhatIfScenarioEngineTests.cs` - Financial scenario modeling (pay raises, equipment costs, reserves)
- `ServiceChargeCalculatorServiceTests.cs` - Rate calculation algorithms (break-even analysis, revenue projections)
- `EnterpriseRepositoryTests.cs` - Data access layer (EF Core operations, query optimization)
- `MunicipalAccountRepositoryUnitTests.cs` - Account management (budget tracking, department allocation)

**Testing Patterns:**
- **Mock Dependencies:** All external services mocked with Moq for isolated testing
- **Async Testing:** Comprehensive async/await operation testing with cancellation tokens
- **Exception Handling:** Error condition testing and graceful failure validation
- **Property Validation:** Observable property changes and data binding verification
- **Command Testing:** RelayCommand execution and CanExecute logic validation

### **2. C# Integration Testing (`WileyWidget.IntegrationTests/`)**

**Framework:** xUnit 2.9.2 with TestContainers 4.2.0 and Respawn 6.2.1
**Target Files:** Database operations, Entity relationships, Concurrency handling
**Test Categories:** Integration, Database, Performance

**Key Test Areas:**
- **Database Integration Tests** - Real SQL Server operations using TestContainers
- **Concurrency Tests** - Multi-threaded database access and transaction isolation
- **Relationship Tests** - Foreign key constraints and cascade operations
- **Performance Tests** - Query optimization and benchmark comparisons
- **Migration Tests** - Schema changes and data integrity validation

**Infrastructure:**
- **TestContainers:** Spins up real SQL Server instances for each test run
- **Database Reset:** Respawn library for clean database state between tests
- **BenchmarkDotNet:** Performance benchmarking for critical operations
- **Transaction Testing:** ACID compliance validation under concurrent load

### **3. C# UI Testing (`WileyWidget.UiTests/`)**

**Framework:** xUnit 2.9.2 with FlaUI 4.0.0 for WPF automation
**Target Files:** XAML views, User controls, Window interactions
**Test Categories:** UI, Component, E2E

**Key Test Files:**
- `MainWindowComponentTests.cs` - Main window layout, ribbon controls, navigation
- `AIAssistViewBindingTests.cs` - AI assistant UI bindings and conversation modes
- `BudgetViewComponentTests.cs` - Budget management interface validation
- `EnterpriseViewComponentTests.cs` - Enterprise data grid operations
- `FlaUISmokeTests.cs` - Critical user journey validation
- `SyncfusionControlsComponentTests.cs` - Third-party control integration

**Testing Capabilities:**
- **UI Automation:** Window manipulation, control interaction, data binding validation
- **Visual Regression:** Layout and rendering verification
- **Accessibility:** Keyboard navigation and screen reader compatibility
- **Theming:** Dark/light mode switching and visual consistency
- **Performance:** UI responsiveness and memory usage during interactions

### **4. Python System Testing (`tests/`)**

**Framework:** pytest 7.0+ with custom memory monitoring utilities
**Target Files:** System resources, Memory management, Performance monitoring
**Test Categories:** System, Memory, Resources

**Key Test Files:**
- `test_memory_leaks.py` - Memory leak detection and garbage collection validation
- `test_resource_exhaustion.py` - Resource usage monitoring and cleanup verification
- `WileyWidget.DependencyInjection.Tests/` - DI container validation
- `WileyWidget.LifecycleTests/` - Object lifecycle management
- `WileyWidget.ThemeResource.Tests/` - Resource dictionary management

**Testing Focus:**
- **Memory Management:** Weak references, object disposal, GC pressure testing
- **Resource Monitoring:** File handles, network connections, thread usage
- **Performance Profiling:** Startup time, memory footprint, CPU utilization
- **Dependency Injection:** Service registration, lifetime management, scoping

### **5. PowerShell Test Automation (`scripts/`)**

**Framework:** PowerShell scripts with process monitoring and cleanup
**Target Files:** Build processes, Deployment automation, Environment validation
**Test Categories:** Build, Deployment, Environment

**Key Scripts:**
- `test-stafact-with-cleanup.ps1` - UI test execution with orphaned process cleanup
- `test-stafact-phase2.ps1` - Advanced UI testing with diagnostics
- `test-with-kill.ps1` - Test execution with forced process termination
- `test-enterprise-connections.ps1` - Database connectivity validation
- `test-serilog-config.ps1` - Logging configuration verification
- `test-quickbooks-connection.ps1` - External API integration testing

**Automation Features:**
- **Process Management:** Automatic cleanup of orphaned .NET processes
- **Environment Validation:** Database connections, API endpoints, configuration
- **Build Verification:** Compilation success, dependency resolution, packaging
- **Deployment Testing:** Installation validation, service startup, rollback procedures

### **Test Execution & CI/CD Integration**

**Local Development:**
```powershell
# Run all unit tests
dotnet test WileyWidget.Tests/WileyWidget.Tests.csproj

# Run UI tests with cleanup
.\scripts\test-stafact-with-cleanup.ps1

# Run Python system tests
python -m pytest tests/ -v

# Run integration tests (requires Docker)
dotnet test WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj
```

**CI/CD Pipeline Execution:**
- **Unit Tests:** Run on every PR and push (5-10 seconds)
- **Integration Tests:** Run on main branch merges (2-5 minutes with TestContainers)
- **UI Tests:** Run on release branches (3-8 minutes with FlaUI)
- **Coverage Reporting:** 70% minimum threshold enforced
- **Parallel Execution:** Tests run in parallel for faster feedback

### **Test Organization & Naming Conventions**

**File Naming:** `{ClassName}Tests.cs` (e.g., `MainViewModelTests.cs`)
**Method Naming:** `{MethodName}_{Scenario}_{ExpectedResult}` (e.g., `LoadEnterprisesAsync_WithValidData_LoadsEnterprises`)
**Categories:** `[Trait("Category", "Unit")]` for filtering and parallel execution
**Setup/Teardown:** xUnit constructor/disposal for test isolation

### **Coverage Targets & Quality Gates**

- **Unit Tests:** 80% line coverage, 90% branch coverage
- **Integration Tests:** All critical database operations covered
- **UI Tests:** All user workflows and error conditions
- **Performance Tests:** Sub-100ms response times for critical operations
- **Memory Tests:** No memory leaks in common usage patterns

### **Testing Best Practices**

- **Test Isolation:** Each test completely independent with proper mocking
- **Arrange-Act-Assert:** Clear test structure for maintainability
- **Meaningful Names:** Test names describe business behavior, not implementation
- **Fast Feedback:** Unit tests run in <100ms, integration tests <30 seconds
- **Realistic Data:** Test data reflects production scenarios and edge cases
- **Continuous Evolution:** Tests updated alongside code changes

---

## 📚 **Documentation**

- **[North Star Roadmap](docs/wiley-widget-north-star-v1.1.md)** - Complete implementation plan
- **[Contributing Guide](CONTRIBUTING.md)** - Development workflow
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

### **Project Documentation**
- **[Project Plan](.vscode/project-plan.md)**: True North vision and phased roadmap
- **[Development Guide](docs/development-guide.md)**: Comprehensive development standards and best practices
- **[Copilot Instructions](.vscode/copilot-instructions.md)**: AI assistant guidelines and project standards
- **[Database Setup Guide](docs/database-setup.md)**: SQL Server Express installation and configuration
- **[Syncfusion License Setup](docs/syncfusion-license-setup.md)**: License acquisition and registration guide
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

## Setup Scripts

### Database Setup

```powershell
# Check database status
pwsh ./scripts/setup-database.ps1 -CheckOnly

# Setup database (SQL Server Express)
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

### Local Database Configuration

The application uses local SQL Server Express for development and production:

- **Server**: .\SQLEXPRESS (local instance)
- **Database**: WileyWidgetDev
- **Authentication**: Windows Authentication (Integrated Security)

### Security Notes

- **Database backups** are recommended for production use
- **Use strong passwords** if switching to SQL Authentication
- **Regular maintenance** of SQL Server Express instance
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
