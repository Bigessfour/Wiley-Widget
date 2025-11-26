# Wiley Widget - Development Scope Summary

**Last Updated**: November 26, 2025  
**Branch**: `upgrade-to-NET10`  
**Status**: 🚧 Dashboard E2E Development

---

## 🎯 CURRENT FOCUS: DASHBOARD FEATURE ONLY

### Development Philosophy

**"We ship features, not frameworks."**

WileyWidget development follows a **single-feature-at-a-time, end-to-end** approach:

- ✅ Complete ONE feature fully (UI → ViewModel → Service → Data → Tests)
- ✅ Validate with unit tests, integration tests, and manual testing
- ✅ Document thoroughly before moving to next feature
- ❌ NO simultaneous development of multiple features
- ❌ NO "framework first, features later" approach

---

## 📋 Current Sprint: Dashboard E2E

### What We're Building

A **municipal budget dashboard** displaying 5 key metrics:

1. Total Revenues ($2,450,000)
2. Total Expenditures ($2,200,000)
3. Budget Balance ($250,000)
4. Active Accounts (127)
5. Budget Utilization (89.8%)

**Features**:

- ✅ Metric cards in 2-column grid layout
- ✅ Refresh button for data reload
- ✅ Loading indicators during data fetch
- ✅ Error handling with user feedback
- ✅ Trend indicators (up/down/stable) with color coding

### Architecture Stack

| Layer         | Technology                              | Status        |
| ------------- | --------------------------------------- | ------------- |
| **UI**        | WinForms (Panel + metric cards)         | 🚧 To build   |
| **ViewModel** | CommunityToolkit.Mvvm + commands        | 🚧 To enhance |
| **Service**   | IDashboardService + mock implementation | 🚧 To create  |
| **Models**    | DashboardMetric + DashboardSummary      | 🚧 To extract |
| **Data**      | Mock data (Phase 1) → SQLite (Phase 2+) | ⏳ Future     |
| **Tests**     | xUnit + Moq + FluentAssertions          | 🚧 To write   |

---

## ❌ EXPLICITLY OUT OF SCOPE

The following features are **BLOCKED** until Dashboard is complete:

### ❌ Chart Visualizations

- No LiveCharts integration
- No interactive charts
- No data visualization beyond metrics

### ❌ Budget Management

- No CRUD operations for budgets
- No budget entry forms
- No hierarchical budget views

### ❌ External Integrations

- No QuickBooks sync
- No xAI/Grok API calls
- No Azure services
- No telemetry (SigNoz)

### ❌ Advanced Features

- No authentication/authorization
- No multi-tenant support
- No advanced reporting
- No data import/export

### ❌ Legacy Code Migration

- No WinUI code porting
- No Syncfusion WinUI evaluation
- Focus on WinForms only

---

## 📚 Documentation Structure

All Dashboard development documentation is centralized:

### Primary Documents

1. **[DASHBOARD-E2E-DEVELOPMENT-PLAN.md](DASHBOARD-E2E-DEVELOPMENT-PLAN.md)**
   - Complete 5-phase implementation plan
   - Detailed code examples
   - Architecture decisions
   - Success criteria

2. **[DASHBOARD-TASK-CHECKLIST.md](DASHBOARD-TASK-CHECKLIST.md)**
   - Phase-by-phase task breakdown
   - Checkboxes for progress tracking
   - Validation commands
   - Troubleshooting guides

3. **[DASHBOARD-QUICK-START.md](DASHBOARD-QUICK-START.md)**
   - 30-minute quick start guide
   - Copy-paste code snippets
   - Minimal setup steps

### Supporting Documents

- **[approved-workflow.md](../.vscode/approved-workflow.md)** - Hard rules and tool enforcement
- **[copilot-instructions.md](../.vscode/copilot-instructions.md)** - MCP guidelines and CI/CD
- **[copilot-mcp-rules.md](../.vscode/copilot-mcp-rules.md)** - Filesystem MCP enforcement

---

## 🚀 Getting Started

### Option 1: Quick Start (30 minutes)

Follow **[DASHBOARD-QUICK-START.md](DASHBOARD-QUICK-START.md)** to:

1. Set up test infrastructure
2. Create models and services
3. Build enhanced ViewModel
4. Get a working foundation

### Option 2: Full Implementation (2-3 days)

Follow **[DASHBOARD-E2E-DEVELOPMENT-PLAN.md](DASHBOARD-E2E-DEVELOPMENT-PLAN.md)** to:

1. **Phase 0**: Setup & Validation (1-2 hours)
2. **Phase 1**: Model & Service Layer (2-4 hours)
3. **Phase 2**: Enhanced ViewModel (2-3 hours)
4. **Phase 3**: WinForms UI (3-4 hours)
5. **Phase 4**: Unit Testing (2-3 hours)
6. **Phase 5**: Integration & Validation (1-2 hours)

### Option 3: Task-by-Task Execution

Use **[DASHBOARD-TASK-CHECKLIST.md](DASHBOARD-TASK-CHECKLIST.md)** for:

- Checkbox-driven development
- Phase-by-phase progress tracking
- Built-in validation commands
- Troubleshooting steps

---

## 📊 Definition of Done (Dashboard)

The Dashboard feature is **COMPLETE** when ALL these criteria are met:

### Code Quality ✅

- [ ] Zero compiler errors
- [ ] Zero compiler warnings
- [ ] Zero analyzer warnings
- [ ] `trunk check --ci` passes
- [ ] All code follows MVVM patterns

### Testing ✅

- [ ] 8+ unit tests pass (service + ViewModel)
- [ ] Code coverage > 80% for Dashboard code
- [ ] Manual test checklist 100% complete
- [ ] No flaky tests

### Functionality ✅

- [ ] Dashboard displays 5 metrics correctly
- [ ] Refresh button works without errors
- [ ] Loading indicator shows during data fetch
- [ ] Error messages display for failures
- [ ] Trend indicators show correct colors

### Documentation ✅

- [ ] README.md updated with Dashboard section
- [ ] Inline XML comments for public APIs
- [ ] Architecture documented in plan
- [ ] Commit messages follow conventions

### Performance ✅

- [ ] Dashboard loads in < 1 second
- [ ] Refresh completes in < 1 second
- [ ] Memory usage < 100 MB after 5 refreshes
- [ ] No UI freezing

### Integration ✅

- [ ] DI configured correctly in Program.cs
- [ ] All dependencies resolve
- [ ] CI/CD pipeline runs Dashboard tests
- [ ] Build artifacts are clean

---

## 🔄 After Dashboard: Next Features

**Only after Dashboard DoD is met**, consider these next slices (in order):

1. **Chart View** (Estimated: 2-3 days)
   - Integrate LiveCharts for visual metrics
   - Bar charts for budget categories
   - Line charts for trends

2. **Budget List View** (Estimated: 3-4 days)
   - Display budget entries in data grid
   - Search and filter functionality
   - Basic CRUD operations

3. **Database Integration** (Estimated: 2-3 days)
   - Replace mock data with EF Core
   - SQLite local database
   - Repository pattern implementation

4. **Settings Panel** (Estimated: 1-2 days)
   - Configuration UI
   - Theme switching (if applicable)
   - User preferences

5. **QuickBooks Sync** (Estimated: 4-5 days)
   - OAuth authentication
   - Data synchronization
   - Error handling and retry logic

**But NOT until Dashboard is ✅ DONE!**

---

## 🛠️ Development Commands

### Daily Workflow

```powershell
# Morning startup
trunk check --monitor
python scripts/dev-start.py

# Development cycle (repeat)
trunk fmt --all
trunk check --fix
trunk check --ci
git add .
git commit -m "feat(dashboard): description"
git push

# Testing
dotnet test --filter FullyQualifiedName~Dashboard

# Build validation
dotnet build WileyWidget.sln --configuration Release
```

### MCP Enforcement

**CRITICAL**: All file operations MUST use MCP filesystem tools:

```javascript
// Activate filesystem tools
activate_file_reading_tools()
activate_directory_and_file_creation_tools()

// Use MCP functions
mcp_filesystem_read_text_file({ path: "..." })
mcp_filesystem_edit_file({ path: "...", edits: [...] })
mcp_filesystem_write_file({ path: "...", content: "..." })
```

### CI/CD Feedback Loop

```powershell
# Complete loop
trunk check --ci --upload
git push
Start-Sleep 60
gh run watch $(gh run list --limit=1 --json=databaseId --jq='.[0].databaseId')

# If CI fails, auto-fix
trunk check --fix --filter=security,quality
git add .
git commit -m "fix: trunk auto-fixes"
git push
```

---

## 📝 Commit Message Convention

Use conventional commits for all Dashboard work:

```
feat(dashboard): create DashboardMetric model
feat(dashboard): implement IDashboardService interface
feat(dashboard): add async load command to ViewModel
feat(dashboard): create WinForms metric card UI
test(dashboard): add DashboardService unit tests
docs(dashboard): update README with Dashboard feature
ci(dashboard): add Dashboard tests to pipeline
fix(dashboard): handle null metrics in UI rendering
```

---

## 🤝 Team Communication

### Scope Changes

**NO scope changes without consensus:**

- User must approve
- GitHub Copilot must validate technical feasibility
- All existing documentation must be updated

### Progress Updates

Daily standup format:

1. **Yesterday**: Tasks completed with checkboxes
2. **Today**: Current phase and specific tasks
3. **Blockers**: Issues preventing progress

### Questions & Clarifications

Use GitHub Issues with labels:

- `dashboard` - Dashboard-specific questions
- `scope` - Scope clarification needed
- `blocked` - Development blocker

---

## 🎯 Success Metrics

### Target Metrics (Dashboard Phase)

| Metric               | Target        | Measurement           |
| -------------------- | ------------- | --------------------- |
| **Development Time** | 2-3 days      | Actual hours logged   |
| **Test Coverage**    | > 80%         | CodeCov report        |
| **Performance**      | < 1s load     | Stopwatch timing      |
| **Quality**          | 0 warnings    | Trunk/compiler output |
| **Documentation**    | 100% complete | Checklist validation  |

### Quality Gates

```powershell
# Pre-commit validation (MANDATORY)
trunk fmt --all
trunk check --fix
trunk check --ci

# Test validation
dotnet test --filter FullyQualifiedName~Dashboard

# Build validation
dotnet build --no-restore --configuration Release
```

---

## 📞 Support & Resources

### Documentation

- Dashboard implementation plan: `docs/DASHBOARD-E2E-DEVELOPMENT-PLAN.md`
- Task checklist: `docs/DASHBOARD-TASK-CHECKLIST.md`
- Quick start: `docs/DASHBOARD-QUICK-START.md`

### Tooling

- Trunk CLI: https://trunk.io/
- GitHub CLI: https://cli.github.com/
- .NET 9.0: https://dotnet.microsoft.com/

### References

- WinForms: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/
- CommunityToolkit.Mvvm: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/
- xUnit: https://xunit.net/
- Moq: https://github.com/moq/moq4

---

**Remember: Dashboard first. Everything else waits.**

**Scope Discipline = Shipping Software**
