# Wiley Widget Project Structure

## Overview
This document defines the official file structure for the Wiley Widget project. This structure must be maintained to ensure consistency, proper organization, and adherence to .NET best practices.

## Root Directory Structure

### Essential Project Files
- `WileyWidget.csproj` - Main C# project file
- `WileyWidget.sln` - Visual Studio solution file
- `WileyWidget.code-workspace` - VS Code workspace configuration
- `README.md` - Project documentation
- `global.json` - .NET SDK version specification

### Configuration Files
- `.editorconfig` - Code style and formatting rules
- `.gitignore` - Git ignore patterns
- `Directory.Build.props` - Shared MSBuild properties
- `Directory.Build.targets` - Shared MSBuild targets
- `Directory.Packages.props` - Centralized NuGet package management
- `appsettings.json` - Application configuration (base)
- `appsettings.Debug.json` - Debug environment configuration
- `appsettings.Production.json` - Production environment configuration
- `azure.yaml` - Azure Pipelines CI/CD configuration

### Environment Files
- `.env` - Local environment variables
- `.env.example` - Environment variables template
- `.env.busbuddy-template` - BusBuddy environment template
- `.env.wiley-widget` - Wiley Widget environment variables

### Build and Output Directories
- `bin/` - Build output binaries
- `obj/` - Intermediate build files
- `publish/` - Published application files
- `coverage-report/` - Test coverage reports
- `logs/` - Application and build logs
- `build/` - Build artifacts and scripts

### Development Tools
- `.vscode/` - VS Code settings and extensions
- `.trunk/` - Trunk CI configuration
- `scripts/` - Build and utility scripts
- `signing/` - Code signing certificates and keys

### Dependencies
- `node_modules/` - Node.js dependencies
- `package-lock.json` - NPM dependency lock file

### Version Control and CI/CD
- `.git/` - Git repository
- `.github/` - GitHub workflows and templates
- `infra/` - Infrastructure as Code (Bicep, Terraform)
- `docs/` - Documentation files

## Wiley Widget Source Directory

### Core Application Structure
```
Wiley Widget/
├── Configuration/          # App configuration classes
├── Converters/            # WPF value converters
├── Data/                  # Data access layer
│   ├── AppDbContext.cs
│   ├── DatabaseSeeder.cs
│   └── Repositories/
├── Migrations/            # EF Core migrations
├── Models/                # Data models and DTOs
├── Resources/             # Application resources
│   ├── license.key
│   ├── LicenseKey.Private.cs
│   └── signing/
├── Services/              # Business logic services
├── Themes/                # WPF themes and styles
├── ViewModels/            # MVVM view models
├── Views/                 # WPF views and windows
└── Properties/            # Assembly properties
```

## Test Projects Structure

### Unit Tests
```
WileyWidget.Tests/
├── Unit test files (*.cs)
├── Test data and fixtures
├── TestResults/           # Test output
├── bin/                   # Test binaries
├── obj/                   # Test intermediates
└── WileyWidget.TestSettings.runsettings
```

### UI Tests
```
WileyWidget.UiTests/
├── UI test files (*.cs)
├── Test automation scripts
├── TestResults/           # Test output
├── bin/                   # Test binaries
├── obj/                   # Test intermediates
├── theme-debug-results.json
└── ui-control-scan-results.json
```

## Maintenance Instructions

### File Placement Rules

1. **Source Code**: All C#, XAML, and related source files must go in `Wiley Widget/` subdirectories
2. **Test Code**: Unit tests in `WileyWidget.Tests/`, UI tests in `WileyWidget.UiTests/`
3. **Configuration**: App settings and config files in root or `Wiley Widget/Configuration/`
4. **Resources**: Static resources in `Wiley Widget/Resources/`
5. **Documentation**: All docs in `docs/` directory
6. **Scripts**: Utility and build scripts in `scripts/`
7. **Build Artifacts**: Never commit `bin/`, `obj/`, `TestResults/`, `*.log`

### Directory Creation Guidelines

- Create subdirectories in `Wiley Widget/` for new feature areas
- Use consistent naming: PascalCase for directories
- Group related files together
- Maintain flat structure within feature directories

### File Organization Best Practices

1. **One Responsibility**: Each file should have a single, clear purpose
2. **Consistent Naming**: Use PascalCase for classes, camelCase for methods
3. **Namespace Structure**: Match directory structure
4. **File Size**: Keep files under 1000 lines when possible
5. **Documentation**: Add XML comments to public APIs

### Prohibited Actions

- ❌ Placing source files directly in root
- ❌ Mixing test types in same project
- ❌ Committing build artifacts
- ❌ Hardcoding paths - use relative paths
- ❌ Creating deep directory hierarchies

### Enforcement

- All new files must follow this structure
- Existing files should be migrated to correct locations
- Pull requests must maintain this organization
- Automated checks should validate structure compliance

## Contact

For questions about file organization or structure changes, refer to the project maintainers or development team.
