# 🛠️ Technology Stack

WileyWidget is built on a modern, enterprise-grade technology stack designed for performance, maintainability, and scalability.

## Core Frameworks

| Technology | Version | Purpose |
|------------|---------|---------|
| **.NET** | 9.0 (UI), 8.0 (Libraries) | Application framework |
| **WinUI 3** | via Windows App SDK 1.6 | Modern Windows UI framework |
| **Windows App SDK** | 1.6.241114003 | Windows platform APIs |
| **C#** | 12.0 | Primary language |

## UI & Presentation

| Technology | Version | Purpose |
|------------|---------|---------|
| **Syncfusion WinUI** | 31.2.5 | Premium UI controls (DataGrid, Charts, etc.) |
| **Prism Framework** | 9.0.537 | MVVM architecture & modularity |
| **Prism.DryIoc** | 9.0.107 | Dependency injection container |
| **CommunityToolkit.Mvvm** | 8.4.0 | MVVM source generators |

## Data & Persistence

| Technology | Version | Purpose |
|------------|---------|---------|
| **Entity Framework Core** | 9.0.10 | ORM & database access |
| **SQL Server Express** | LocalDB | Local database engine |
| **Microsoft.Data.SqlClient** | 6.1.2 | SQL Server connectivity |

## Business Logic & Services

| Technology | Version | Purpose |
|------------|---------|---------|
| **Microsoft.Extensions.Hosting** | 9.0.10 | Generic host & DI |
| **Microsoft.Extensions.DependencyInjection** | 9.0.11 | Dependency injection |
| **FluentValidation** | 12.1.0 | Business rule validation |
| **Polly** | 8.6.4 | Resilience & transient fault handling |

## External Integrations

| Technology | Version | Purpose |
|------------|---------|---------|
| **QuickBooks SDK** | 14.7.0.2 | QuickBooks Online integration |
| **IppOAuth2PlatformSdk** | 14.0.0 | QuickBooks OAuth 2.0 |
| **Microsoft.Extensions.Http** | 9.0.10 | HTTP client factory |

## Logging & Observability

| Technology | Version | Purpose |
|------------|---------|---------|
| **Serilog** | 4.3.0 | Structured logging |
| **Serilog.Sinks.File** | 7.0.0 | File logging sink |
| **Serilog.Sinks.Console** | 6.0.0 | Console logging sink |
| **Serilog.Enrichers.Environment** | 3.0.1 | Log enrichment |
| **OpenTelemetry** | 1.13.1 | Distributed tracing |

## Testing & Quality

| Technology | Version | Purpose |
|------------|---------|---------|
| **xUnit** | 2.9.3 | Unit & integration testing |
| **Moq** | 4.20.72 | Mocking framework |
| **FluentAssertions** | 6.8.0 | Fluent test assertions |
| **coverlet.collector** | 6.0.4 | Code coverage collection |
| **Microsoft.NET.Test.Sdk** | 17.14.1 | Test SDK |

## Security & Configuration

| Technology | Version | Purpose |
|------------|---------|---------|
| **DPAPI** | Built-in | Windows Data Protection API for secrets |
| **Microsoft.Extensions.Configuration** | 9.0.10 | Configuration management |
| **Microsoft.Extensions.Options** | 9.0.10 | Options pattern |
| **DotNetEnv** | 3.1.1 | Environment variable management |

## Package Management

| Feature | Implementation |
|---------|----------------|
| **Central Package Management (CPM)** | Directory.Packages.props |
| **Package Source** | NuGet.org |
| **Restore Strategy** | NuGet PackageReference |

## Development Tools

| Tool | Purpose |
|------|---------|
| **Visual Studio 2022** | Primary IDE |
| **.NET CLI** | Build & test automation |
| **PowerShell 7+** | Scripting & automation |
| **Git** | Version control |
| **GitHub Actions** | CI/CD pipeline |

## Architecture Patterns

- **N-Tier Layered Architecture**: Separation of concerns across Presentation, Business, Data, and Domain layers
- **MVVM (Model-View-ViewModel)**: Clean separation of UI and business logic
- **Repository Pattern**: Abstraction of data access logic
- **Dependency Injection**: Loose coupling and testability
- **Event Aggregator**: Decoupled communication (Prism)
- **Module Pattern**: Feature-based code organization (Prism)

## Build & Deployment

| Technology | Purpose |
|------------|---------|
| **MSBuild** | Build system |
| **NuGet** | Package management |
| **Windows Installer** | Deployment packaging (future) |
| **ClickOnce** | Alternative deployment (future) |

## Target Platform

- **Operating System**: Windows 10 (1809+) / Windows 11
- **Architecture**: x64
- **Runtime**: .NET 9.0 Desktop Runtime
- **Minimum RAM**: 128 MB (recommended: 512 MB+)
- **Disk Space**: 100 MB (application + data)

## Package Version Management

All package versions are centrally managed in `Directory.Packages.props`, ensuring:
- ✅ Consistent versions across all projects
- ✅ Easy dependency updates
- ✅ Prevention of version conflicts
- ✅ Single source of truth for all packages

---

For detailed architecture information, see [README.md](README.md).
