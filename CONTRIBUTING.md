# Contributing (Solo-Friendly)

Pragmatic notes for future-you (or a curious passerby). Git aliases moved here from README to declutter.

## 📋 **Essential Reading**

### **MANDATORY SAFETY PROCEDURES**
- **[Standard Operating Procedures](docs/sop-azure-operations.md)**: **REQUIRED** - Azure safety protocols
- **[Azure Safety Guide](docs/azure-novice-guide.md)**: Safe Azure operations for all contributors
- **[Azure Quick Reference](docs/azure-quick-reference.md)**: Daily operations checklist

### **Project Documentation**
- **[Copilot Instructions](.vscode/copilot-instructions.md)**: AI assistant guidelines and project standards
- **[Project Plan](.vscode/project-plan.md)**: Complete True North vision and roadmap
- **[Development Guide](docs/development-guide.md)**: Comprehensive technical standards

## 🚨 **CONTRIBUTOR SAFETY REQUIREMENTS**

### **MANDATORY: Azure Safety Certification**
**ALL contributors must complete Azure safety training before making Azure-related changes.**

**Required Reading:**
- [ ] Standard Operating Procedures (docs/sop-azure-operations.md)
- [ ] Azure Safety Guide (docs/azure-novice-guide.md)
- [ ] Copilot Azure Examples (docs/copilot-azure-examples.md)

**Required Training:**
- [ ] Safe script operations
- [ ] Dry-run procedures
- [ ] Emergency protocols
- [ ] Backup procedures

### **Azure Contribution Workflow**
```powershell
# 1. Check Azure status before contributing
.\scripts\azure-safe-operations.ps1 -Operation status

# 2. Create backup before any changes
.\scripts\azure-safe-operations.ps1 -Operation backup

# 3. Test all operations with dry-run
.\scripts\azure-safe-operations.ps1 -Operation [operation] -DryRun

# 4. Execute only after testing
.\scripts\azure-safe-operations.ps1 -Operation [operation]

# 5. Verify after changes
.\scripts\azure-safe-operations.ps1 -Operation status
```

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

#### **MANDATORY SAFETY PROTOCOLS**
- **Safe Scripts Only**: All Azure operations must use approved safe scripts
- **No Direct CLI**: Direct Azure CLI commands are FORBIDDEN for all contributors
- **Dry Run Required**: Every operation must be tested before execution
- **Backup Mandatory**: Automatic backups required before destructive operations

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

#### **GitHub Copilot Azure Integration**
**MANDATORY: Use Copilot following safety protocols**

**Safe Copilot Questions:**
```
✅ "How do I safely check my Azure database connection?"
✅ "Show me how to create a backup using the safe script"
✅ "Explain Azure Resource Groups in simple terms"
✅ "What would happen if I run this command? Explain first"
```

**Prohibited Questions:**
```
❌ "Delete my Azure database"
❌ "Run this az sql db delete command"
❌ "Execute this Azure CLI command for me"
```
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
