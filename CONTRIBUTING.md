# Contributing (Solo-Friendly)

Pragmatic notes for future-you (or a curious passerby). Git aliases moved here from README to declutter.

## 📋 **Essential Reading**

- **[Copilot Instructions](.vscode/copilot-instructions.md)**: AI assistant guidelines and project standards
- **[Project Plan](.vscode/project-plan.md)**: Complete True North vision and roadmap
- **[Development Guide](docs/development-guide.md)**: Comprehensive technical standards

## Project Vision & Roadmap

### Current Phase: Foundation & Scaffold ✅

**Focus**: Establishing solid development foundation
**Priority**: Syncfusion UI, MVVM architecture, logging, testing infrastructure
**Success Criteria**: Application launches, basic UI functional, 70%+ test coverage

### Next Phase: Data Layer Integration 🔄

**Preview**: Azure SQL with EF Core, repository pattern, connection management
**Preparation**: Review Azure SQL documentation, plan data models

## Development Standards

### Architecture

- **MVVM Pattern**: Strict separation - no code-behind logic in XAML files
- **CommunityToolkit.Mvvm**: Use `[ObservableObject]`, `[RelayCommand]`, `[ObservableProperty]`
- **EF Core**: Microsoft.EntityFrameworkCore.SqlServer for Azure SQL integration
- **Testing**: NUnit with minimum 70% coverage (CI enforced)

### Code Quality

- **Nullable References**: Disabled for now (controlled via .editorconfig)
- **Logging**: Serilog for structured logging to `%AppData%/WileyWidget/logs`
- **Settings**: JSON persistence via SettingsService
- **PowerShell Scripts**: Use proper modules, avoid Write-Host except for traces

### UI Guidelines

- **Syncfusion Only**: No custom controls - use official Syncfusion WPF 30.2.4
- **Themes**: Fluent Dark/Light with persistence
- **Responsive**: Handle DPI scaling properly
- **Accessibility**: Follow WPF accessibility guidelines

### Azure Integration

- **Database**: Azure SQL with EF Core
- **Authentication**: Azure.Identity (managed identity for production)
- **Security**: OAuth for QuickBooks Online, encrypted token storage

## Git Aliases (Optional)

Add to global config:

```pwsh
git config --global alias.st status
git config --global alias.co checkout
git config --global alias.ci commit
git config --global alias.br branch
git config --global alias.lg "log --oneline --decorate --graph --all"
```

Usage:

```pwsh
git st
git lg
```

## Pre-Push Hook (Optional Gate)

Lightweight guard so you don’t push broken builds.

Setup once:

```pwsh
git config core.hooksPath scripts
```

Hook runs build + tests; non-zero exit blocks push.

## Branching

- main: stable, buildable
- feature/\* for risk
- hotfix/\* for urgent patch

## Commit Style

Conventional-ish prefixes optional (feat:, fix:, chore:, test:, docs:). Keep commits small & cohesive.

## Release

Use Release workflow to bump version & produce artifact. Tags: vX.Y.Z

## Coverage

CI enforces 70% min line coverage (adjust via COVERAGE_MIN).

## Build & Test

```pwsh
# Full build with tests
pwsh ./scripts/build.ps1

# Include UI tests
$env:RUN_UI_TESTS=1; pwsh ./scripts/build.ps1

# Run only tests
dotnet test WileyWidget.Tests/WileyWidget.Tests.csproj
```

## TODO Candidates

- Static analyzers (enable when codebase grows)
- UI smoke automation (FlaUI)
- Dynamic DataGrid column support snippet (see README)

Stay ruthless about scope; this is a scaffold, not a framework.
