# Wiley Widget Documentation# | Document | Purpose | Audience |

The documentation is now split into two layers:|----------|---------|----------|

| [README.md](../README.md) | Project overview, quick start, features | All users |

- **docs/core/** – concise guides required for day-to-day development| [Project Plan](../.vscode/project-plan.md) | True North vision and phased roadmap | All stakeholders |

- **docs/reference/** – archived research, historical reports, and integration walkthroughs| [Copilot Instructions](../.vscode/copilot-instructions.md) | AI assistant guidelines and standards | Developers |

| [Development Guide](development-guide.md) | Comprehensive technical standards | Developers |

## Core Guides| [Azure SQL Integration](azure-sql-integration.md) | Database setup and EF Core implementation | Developers |

| [Connection Methods Reference](connection-methods-reference.md) | Complete connection configuration guide | Developers |

| Guide | Summary || [Contributing Guide](../CONTRIBUTING.md) | Development workflow and guidelines | Contributors |🚀 Getting Started

| --- | --- |

| [Overview](core/Overview.md) | Entry point that links to every core document || Document | Purpose | Audience |

| [Architecture](core/Architecture.md) | High-level summary of the Prism + MVVM implementation || ------------------------------------------------- | ----------------------------------------- | ---------------- | ------------------- |

| [Development](core/Development.md) | Environment setup, workflow, and essential commands || [README.md](../README.md) | Project overview, quick start, features | All users |

| [Testing](core/Testing.md) | Testing strategy, coverage expectations, and tooling || [Project Plan](../.vscode/project-plan.md) | True North vision and phased roadmap | All stakeholders |

| [Security](core/Security.md) | Secret management and security guard rails || [Development Guide](development-guide.md) | Comprehensive technical standards | Developers |

| [Azure SQL Integration](azure-sql-integration.md) | Database setup and EF Core implementation | Developers |

## Reference Library| [Contributing Guide](../CONTRIBUTING.md) | Development workflow and guidelines | Contributors | Documentation Index |

All legacy investigations, deep technical notes, and specialized integration docs now live under `docs/reference/`. The former `analysis/`, `guides/`, `reports/`, and similar directories were moved there unchanged. Browse by folder or rely on search when you need background context.## 📚 Documentation Overview

## Related Root DocumentsThis documentation provides comprehensive guidance for developing and maintaining the WileyWidget application.

- [`README.md`](../README.md) – product overview and getting started instructions## 🚀 Getting Started

- [`CONTRIBUTING.md`](../CONTRIBUTING.md) – contribution workflow and etiquette

- [`SECURITY.md`](../SECURITY.md) – full security policy and incident response plan| Document | Purpose | Audience |

| ----------------------------------------- | --------------------------------------- | ------------ |

When adding new long-form documentation:| [README.md](../README.md) | Project overview, quick start, features | All users |

1. Decide whether it belongs in `core/` (essential knowledge) or `reference/` (background material).| [Development Guide](development-guide.md) | Comprehensive development standards | Developers |

2. Cross-link from the appropriate core guide so the curated experience stays current.| [Contributing Guide](../CONTRIBUTING.md) | Development workflow and guidelines | Contributors |

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
