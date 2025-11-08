# Wiley-Widget Repository Reorganization Plan

**Date:** November 8, 2025  
**Purpose:** Restructure repository following .NET/WPF best practices  
**Status:** Ready for execution

---

## 📋 Executive Summary

This document outlines a comprehensive plan to reorganize the Wiley-Widget repository structure, addressing:

- User-specific files committed to git
- Inconsistent directory structure
- Root directory clutter
- Non-standard project organization

**Expected Outcome:** Clean, maintainable structure following .NET enterprise patterns.

---

## 🔍 Current Issues Identified

### 1. User-Specific Files in Repository

- ❌ `%APPDATA%/npm/` - Windows environment variable directory with npm binaries
- ❌ `.continue/` - Continue.dev IDE configuration (user-specific)
- ❌ `.mcp/` - MCP server configuration (user-specific)
- ❌ `node_modules/` - NPM dependencies (should be regenerated from package.json)
- ❌ Python caches: `.mypy_cache/`, `.pytest_cache/`, `.ruff_cache/`
- ❌ `.tmp.drivedownload/` - Temporary download folder
- ❌ `test.csx` - Trivial test file at root

### 2. Project Organization Issues

- ❌ Library projects scattered at root level
- ❌ Test projects mixed with source projects
- ❌ `Wiley Widget/` folder with space in name (anti-pattern)
- ✅ Main application already in `src/` (good!)

### 3. Configuration File Clutter

- ❌ Multiple config files at root without organization
- ❌ No separation between development and production configs
- ❌ Data files (`budgeted_amounts.txt`, `budget_entries_schema.txt`) at root

### 4. Script Organization

- ❌ Scripts at root level (`run-e2e.ps1`, `verify-license-setup.ps1`)
- ✅ Main scripts directory exists but needs categorization

### 5. Case Sensitivity Inconsistencies

- ❌ `SQL/` directory (uppercase) vs standard lowercase convention

---

## 🎯 Target Structure

```
Wiley_Widget/
├── .github/                    # GitHub-specific (workflows, issue templates)
├── .trunk/                     # Trunk CI/CD configuration (keep)
├── config/                     # Centralized configuration
│   ├── development/
│   │   └── appsettings.json
│   ├── production/
│   │   └── appsettings.Production.json
│   ├── shared/
│   │   └── app.config
│   ├── assistant-preferences.yaml
│   └── event.push.json
├── docs/                       # All documentation
│   ├── examples/               # Example data files
│   │   ├── budgeted_amounts.txt
│   │   └── budget_entries_schema.txt
│   └── *.md                    # Documentation files
├── docker/                     # Docker configurations (keep as-is)
├── licenses/                   # License files (keep as-is)
├── scripts/                    # All scripts, categorized
│   ├── build/
│   ├── deployment/
│   ├── maintenance/
│   │   ├── reorganize-repository.ps1
│   │   └── verify-license-setup.ps1
│   └── testing/
│       └── run-e2e.ps1
├── signing/                    # Code signing (keep as-is)
├── sql/                        # SQL scripts (renamed from SQL/)
├── src/                        # All source code
│   ├── WileyWidget/            # Main WPF application (already here)
│   ├── WileyWidget.Abstractions/
│   ├── WileyWidget.Business/
│   ├── WileyWidget.Data/
│   ├── WileyWidget.Facade/
│   ├── WileyWidget.Models/
│   ├── WileyWidget.Services/
│   ├── WileyWidget.Services.Abstractions/
│   ├── WileyWidget.UI/
│   └── WileyWidget.Webhooks/
├── tests/                      # All test projects
│   └── WileyWidget.Tests/
├── tools/                      # Development tools (keep as-is)
├── wwwroot/                    # Static web assets (keep as-is)
├── .editorconfig               # Editor configuration
├── .gitattributes              # Git attributes
├── .gitignore                  # Enhanced with new patterns
├── .gitleaks.toml              # Gitleaks configuration
├── Directory.Build.props       # MSBuild properties
├── Directory.Build.targets     # MSBuild targets
├── Directory.Packages.props    # Central package management
├── global.json                 # .NET SDK version
├── NuGet.config                # NuGet configuration
├── package.json                # Node.js for MCP/tooling
├── package-lock.json
├── pyproject.toml              # Python tooling
├── pyrightconfig.json          # Python type checking
├── WileyWidget.sln             # Solution file (updated paths)
├── README.md
├── CHANGELOG.md
├── CONTRIBUTING.md
└── SECURITY.md
```

---

## 🔄 Reorganization Phases

### Phase 1: Pre-flight Checks ✅

- Verify git repository status
- Check for uncommitted changes
- Create backup branch: `backup/pre-reorganization-YYYYMMDD-HHMMSS`

### Phase 2: Remove User-Specific Files 🗑️

**Files to remove from git tracking:**

```bash
git rm -rf %APPDATA%
git rm -rf .continue
git rm -rf .mcp
git rm -rf node_modules
git rm -rf .mypy_cache
git rm -rf .pytest_cache
git rm -rf .ruff_cache
git rm -rf .tmp.drivedownload
git rm test.csx
git rm .coverage
```

### Phase 3: Update .gitignore 📝

**Add comprehensive patterns:**

```gitignore
# IDE-specific configurations
.continue/
.mcp/

# VS Code (selective)
.vscode/*
!.vscode/extensions.json
!.vscode/tasks.json
!.vscode/launch.json
!.vscode/*.code-snippets
!.vscode/settings.json

# Python artifacts
.venv/
venv/
__pycache__/

# Node.js artifacts
node_modules/
.npm/

# Windows user paths
%APPDATA%/
%LOCALAPPDATA%/
%TEMP%/

# Temporary folders
.tmp/
.tmp.*/
*.tmp
```

### Phase 4: Move Source Projects 📦

```bash
git mv WileyWidget.Abstractions src/WileyWidget.Abstractions
git mv WileyWidget.Business src/WileyWidget.Business
git mv WileyWidget.Data src/WileyWidget.Data
git mv WileyWidget.Facade src/WileyWidget.Facade
git mv WileyWidget.Models src/WileyWidget.Models
git mv WileyWidget.Services src/WileyWidget.Services
git mv WileyWidget.Services.Abstractions src/WileyWidget.Services.Abstractions
git mv WileyWidget.UI src/WileyWidget.UI
git mv WileyWidget.Webhooks src/WileyWidget.Webhooks

# Handle space in folder name
git mv "Wiley Widget" src/WileyWidget.Legacy
```

### Phase 5: Move Test Projects 🧪

```bash
git mv WileyWidget.Tests tests/WileyWidget.Tests
```

### Phase 6: Organize Scripts 📜

```bash
git mv run-e2e.ps1 scripts/testing/run-e2e.ps1
git mv verify-license-setup.ps1 scripts/maintenance/verify-license-setup.ps1
```

### Phase 7: Centralize Configuration ⚙️

```bash
# Create config subdirectories
mkdir -p config/development config/production config/shared

# Move configuration files
git mv app.config config/shared/app.config
git mv appsettings.json config/development/appsettings.json
git mv appsettings.Production.json config/production/appsettings.Production.json
git mv assistant-preferences.yaml config/assistant-preferences.yaml
git mv event.push.json config/event.push.json

# Move example data
git mv budgeted_amounts.txt docs/examples/budgeted_amounts.txt
git mv budget_entries_schema.txt docs/examples/budget_entries_schema.txt
```

### Phase 8: Rename SQL Directory 📊

```bash
git mv SQL sql
```

### Phase 9: Update Solution File 🔧

**Update project paths in `WileyWidget.sln`:**

```diff
-Project("{...}") = "WileyWidget.Business", "WileyWidget.Business\WileyWidget.Business.csproj"
+Project("{...}") = "WileyWidget.Business", "src\WileyWidget.Business\WileyWidget.Business.csproj"

-Project("{...}") = "WileyWidget.Tests", "WileyWidget.Tests\WileyWidget.Tests.csproj"
+Project("{...}") = "WileyWidget.Tests", "tests\WileyWidget.Tests\WileyWidget.Tests.csproj"
```

### Phase 10: Update Project References 🔗

**Update `ProjectReference` paths in all `.csproj` files:**

- Projects in `src/` reference each other with `..\..\`
- Projects in `tests/` reference `src/` with `..\..\..\src\`

### Phase 11: Update CI/CD and Scripts 🚀

**Files requiring path updates:**

1. `.github/workflows/ci-optimized.yml`
2. `.vscode/tasks.json`
3. Docker volume mounts in `docker-compose*.yml`
4. Scripts in `scripts/` that reference project paths:
   - `fast-build.ps1`
   - `cleanup-dotnet-processes.ps1`
   - `run-tests-verbose.ps1`
   - `trunk-maintenance.ps1`

### Phase 12: Validation ✅

```bash
# Restore NuGet packages
dotnet restore WileyWidget.sln

# Build solution
dotnet build WileyWidget.sln

# Run Trunk checks
trunk check --ci

# Test docker builds
docker build -f docker/Dockerfile.csx-tests .

# Verify VS Code tasks
code .vscode/tasks.json
```

### Phase 13: Commit Strategy 💾

**8 logical commits:**

1. **"chore: remove user-specific and generated files"**
   - Remove %APPDATA%, .continue, .mcp, node_modules, caches, test.csx

2. **"chore: update .gitignore with comprehensive patterns"**
   - Add missing IDE, cache, and temp file patterns

3. **"refactor: reorganize source projects into src/ directory"**
   - Move all library projects to src/

4. **"refactor: reorganize test projects into tests/ directory"**
   - Move test projects to tests/

5. **"refactor: organize scripts into categorized subdirectories"**
   - Move scripts to testing/ and maintenance/

6. **"refactor: centralize configuration files"**
   - Move configs to config/ with environment subdirectories

7. **"chore: rename SQL to sql for consistency"**
   - Lowercase directory name

8. **"fix: update project references after reorganization"**
   - Update .sln, .csproj, scripts, docker configs, documentation

---

## 🛡️ Safety Measures

### Backup Strategy

- **Automatic backup branch** created before any changes
- **Branch name:** `backup/pre-reorganization-YYYYMMDD-HHMMSS`
- **Rollback command:** `git reset --hard <backup-branch>`

### Pre-flight Checks

- ✅ Verify git repository exists
- ✅ Check for uncommitted changes (abort if found, unless `-Force`)
- ✅ Display current branch

### Dry Run Mode

```powershell
.\scripts\maintenance\reorganize-repository.ps1 -DryRun
```

Preview all changes without executing them.

### Validation Checks

- ✅ `dotnet restore` succeeds
- ✅ `trunk check --ci` passes
- ✅ Solution file loads correctly
- ✅ All projects reference correctly

---

## 🚀 Execution Instructions

### Option 1: Automated Script (Recommended)

```powershell
# 1. Preview changes (dry run)
.\scripts\maintenance\reorganize-repository.ps1 -DryRun

# 2. Execute reorganization
.\scripts\maintenance\reorganize-repository.ps1

# 3. Validate changes
dotnet restore
dotnet build
trunk check --ci

# 4. Review and push
git log -5 --oneline
git push origin <current-branch>
```

### Option 2: Manual Step-by-Step

Follow each phase command from the "Reorganization Phases" section above.

### Option 3: Trunk Integration

```powershell
# Execute with Trunk validation
.\scripts\maintenance\reorganize-repository.ps1
trunk check --ci --upload
trunk fmt --all
```

---

## 📊 Impact Analysis

### Files Affected

- **Deleted:** ~10 user-specific files/directories
- **Moved:** ~15 projects + configuration files + scripts
- **Updated:** Solution file, all .csproj files, CI/CD configs, scripts

### Benefits

✅ **Clean repository** - No user-specific or generated files  
✅ **Standard structure** - Follows .NET enterprise conventions  
✅ **Better organization** - Clear separation of source, tests, configs  
✅ **Improved maintainability** - Easier navigation and understanding  
✅ **CI/CD compatibility** - Consistent with Trunk and GitHub Actions  
✅ **Scalability** - Easy to add new projects in organized structure

### Risks

⚠️ **Breaking change** - All paths change (mitigated by comprehensive updates)  
⚠️ **IDE reconfiguration** - Developers may need to reload solution  
⚠️ **CI/CD updates** - Workflows need path corrections (included in plan)

---

## 🔄 Rollback Plan

### If Issues Occur During Execution

```powershell
# Immediate rollback
git reset --hard HEAD

# Restore from backup branch
$backupBranch = git branch --list "backup/pre-reorganization-*" | Select-Object -Last 1
git reset --hard $backupBranch
```

### If Issues Found After Commit

```powershell
# Revert the reorganization commit
git revert HEAD

# Or reset to backup branch
git reset --hard backup/pre-reorganization-<timestamp>
```

---

## 📝 Post-Reorganization Tasks

### 1. Update Team Documentation

- [ ] Notify team of structure changes
- [ ] Update onboarding documentation
- [ ] Update build/deployment documentation

### 2. Update IDE Configurations

- [ ] VS Code workspace settings
- [ ] Visual Studio solution explorer folders
- [ ] Rider project structure

### 3. Update CI/CD Pipelines

- [ ] Verify GitHub Actions workflows
- [ ] Check Trunk CI/CD integration
- [ ] Test Docker builds

### 4. Update External References

- [ ] Update README badges/links
- [ ] Update documentation links
- [ ] Update wiki/external docs

### 5. Developer Actions Required

- [ ] Pull latest changes: `git pull`
- [ ] Regenerate dependencies: `npm install`, `dotnet restore`
- [ ] Reload solution in IDE
- [ ] Clear IDE caches if needed

---

## 📞 Support and Questions

**Script Location:** `scripts/maintenance/reorganize-repository.ps1`  
**Documentation:** This file (`docs/REPOSITORY_REORGANIZATION_PLAN.md`)  
**Backup Branch:** Automatically created with timestamp  
**Rollback:** `git reset --hard backup/pre-reorganization-<timestamp>`

---

## ✅ Checklist

### Before Execution

- [ ] Read this plan completely
- [ ] Backup any uncommitted work
- [ ] Ensure on correct branch
- [ ] Run dry run: `.\scripts\maintenance\reorganize-repository.ps1 -DryRun`

### During Execution

- [ ] Execute script: `.\scripts\maintenance\reorganize-repository.ps1`
- [ ] Monitor output for errors
- [ ] Note backup branch name

### After Execution

- [ ] Verify: `dotnet restore && dotnet build`
- [ ] Verify: `trunk check --ci`
- [ ] Test key functionality
- [ ] Review commits: `git log -5`
- [ ] Push changes: `git push`

---

**End of Reorganization Plan**  
_Generated: November 8, 2025_  
_For: Wiley-Widget .NET WPF Application_
