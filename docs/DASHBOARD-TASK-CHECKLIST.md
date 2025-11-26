# Dashboard E2E - Task Checklist

**Project**: Wiley Widget  
**Scope**: Dashboard Feature Only  
**Status**: 🚧 In Progress

---

## Quick Status

- [ ] Phase 0: Setup & Validation
- [ ] Phase 1: Model & Service Layer
- [ ] Phase 2: Enhanced ViewModel
- [ ] Phase 3: WinForms UI
- [ ] Phase 4: Unit Testing
- [ ] Phase 5: Integration & Validation

---

## Phase 0: Setup & Validation ⏱️ 1-2 hours

### Branch & Build

- [ ] Checkout `upgrade-to-NET10` branch
- [ ] Pull latest changes
- [ ] Run `dotnet clean`
- [ ] Run `dotnet build --no-restore` (0 errors, 0 warnings)
- [ ] Run app: `dotnet run --project WileyWidget.WinForms/WileyWidget.WinForms.csproj`
- [ ] Verify simple window opens

### Test Infrastructure

- [ ] Create test project: `dotnet new xunit -n WileyWidget.WinForms.Tests -o tests/WileyWidget.WinForms.Tests`
- [ ] Add to solution: `dotnet sln add tests/WileyWidget.WinForms.Tests/WileyWidget.WinForms.Tests.csproj`
- [ ] Add reference: `dotnet add tests/WileyWidget.WinForms.Tests reference WileyWidget.WinForms/WileyWidget.WinForms.csproj`
- [ ] Add Moq: `dotnet add tests/WileyWidget.WinForms.Tests package Moq`
- [ ] Add FluentAssertions: `dotnet add tests/WileyWidget.WinForms.Tests package FluentAssertions`
- [ ] Add test SDK: `dotnet add tests/WileyWidget.WinForms.Tests package Microsoft.NET.Test.Sdk`
- [ ] Verify test project builds: `dotnet build tests/WileyWidget.WinForms.Tests`

### Quality Checks

- [ ] Regenerate manifest: `python scripts/tools/generate_repo_urls.py -o ai-fetchable-manifest.json`
- [ ] Run Trunk: `trunk check --ci`
- [ ] Fix any issues: `trunk check --fix`

**Success Criteria**: ✅ Clean build, app runs, test project compiles, Trunk passes

---

## Phase 1: Model & Service Layer ⏱️ 2-4 hours

### 1.1 Extract Dashboard Models

- [ ] Create file: `src/WileyWidget.Models/Models/DashboardMetric.cs`
- [ ] Add properties: Name, Value, Unit, Description, LastUpdated, Trend
- [ ] Create enum: `MetricTrend` (Up, Down, Neutral)
- [ ] Compile check: `dotnet build src/WileyWidget.Models`

- [ ] Create file: `src/WileyWidget.Models/Models/DashboardSummary.cs`
- [ ] Add properties: Metrics (list), GeneratedAt, MunicipalityName, FiscalYear
- [ ] Compile check: `dotnet build src/WileyWidget.Models`

### 1.2 Create Service Interface

- [ ] Create file: `src/WileyWidget.Services.Abstractions/IDashboardService.cs`
- [ ] Add method: `Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken)`
- [ ] Add method: `Task RefreshDataAsync(CancellationToken)`
- [ ] Compile check: `dotnet build src/WileyWidget.Services.Abstractions`

### 1.3 Implement Mock Service

- [ ] Create file: `src/WileyWidget.Services/DashboardService.cs`
- [ ] Implement `IDashboardService`
- [ ] Add constructor with `ILogger<DashboardService>`
- [ ] Implement `GetDashboardSummaryAsync` with 5 mock metrics:
  - [ ] Total Revenues ($2,450,000)
  - [ ] Total Expenditures ($2,200,000)
  - [ ] Budget Balance ($250,000)
  - [ ] Active Accounts (127)
  - [ ] Budget Utilization (89.8%)
- [ ] Implement `RefreshDataAsync` with mock delay
- [ ] Compile check: `dotnet build src/WileyWidget.Services`

### Validation

- [ ] Full solution build: `dotnet build WileyWidget.sln`
- [ ] No warnings or errors
- [ ] Commit: `git commit -m "feat(dashboard): create models and mock service"`

**Success Criteria**: ✅ Models compile, service interface clean, mock service returns data

---

## Phase 2: Enhanced ViewModel ⏱️ 2-3 hours

### 2.1 Update DashboardViewModel

- [ ] Open file: `WileyWidget.WinForms/ViewModels/DashboardViewModel.cs`
- [ ] Add service injection in constructor: `IDashboardService`, `ILogger<DashboardViewModel>`
- [ ] Add observable properties:
  - [ ] `bool IsLoading`
  - [ ] `string? ErrorMessage`
  - [ ] `string MunicipalityName`
  - [ ] `int FiscalYear`
  - [ ] `DateTime LastUpdated`
- [ ] Create `LoadDashboardAsync` command with `[RelayCommand]`
  - [ ] Set `IsLoading = true`
  - [ ] Clear `ErrorMessage`
  - [ ] Call `_dashboardService.GetDashboardSummaryAsync()`
  - [ ] Populate `Metrics` collection
  - [ ] Handle exceptions with `ErrorMessage`
  - [ ] Set `IsLoading = false` in `finally`
- [ ] Create `RefreshDashboardAsync` command with `[RelayCommand]`
  - [ ] Call `_dashboardService.RefreshDataAsync()`
  - [ ] Call `LoadDashboardAsync`
  - [ ] Handle errors

### 2.2 Update Project References

- [ ] Open `WileyWidget.WinForms/WileyWidget.WinForms.csproj`
- [ ] Add reference to `WileyWidget.Services.Abstractions`
- [ ] Add reference to `WileyWidget.Services`
- [ ] Add reference to `WileyWidget.Models`

### Validation

- [ ] Compile: `dotnet build WileyWidget.WinForms`
- [ ] Verify no errors
- [ ] Commit: `git commit -m "feat(dashboard): enhance ViewModel with service injection and commands"`

**Success Criteria**: ✅ ViewModel compiles, commands are async, error handling in place

---

## Phase 3: WinForms Dashboard UI ⏱️ 3-4 hours

### 3.1 Create DashboardForm

- [ ] Create file: `WileyWidget.WinForms/Forms/DashboardForm.cs`
- [ ] Add constructor with `DashboardViewModel` injection
- [ ] Create `InitializeComponent()` method
  - [ ] Add header label (municipality name + fiscal year)
  - [ ] Add refresh button
  - [ ] Add loading progress bar
  - [ ] Add error label (red, hidden by default)
  - [ ] Add metrics panel (TableLayoutPanel, 2 columns)
- [ ] Create `SetupDataBindings()` method
  - [ ] Subscribe to `ViewModel.PropertyChanged`
  - [ ] Subscribe to `ViewModel.Metrics.CollectionChanged`
  - [ ] Handle property updates (IsLoading, ErrorMessage, MunicipalityName)
- [ ] Create `RenderMetrics()` method
  - [ ] Clear panel
  - [ ] Loop through metrics
  - [ ] Create metric cards
- [ ] Create `CreateMetricCard(DashboardMetric)` method
  - [ ] Panel with border and padding
  - [ ] Name label (bold)
  - [ ] Value label (large font, formatted with unit)
  - [ ] Description label (gray, small)
  - [ ] Trend label (colored based on trend)
- [ ] Create `GetTrendSymbol(MetricTrend)` helper
- [ ] Create `GetTrendColor(MetricTrend)` helper
- [ ] Add `LoadInitialData()` method (calls LoadDashboardCommand)

### 3.2 Update Program.cs for DI

- [ ] Open `WileyWidget.WinForms/Program.cs`
- [ ] Replace current code with Host builder pattern
- [ ] Register services:
  - [ ] `services.AddSingleton<IDashboardService, DashboardService>()`
  - [ ] `services.AddTransient<DashboardViewModel>()`
  - [ ] `services.AddTransient<DashboardForm>()`
  - [ ] Logging configuration
- [ ] Resolve `DashboardForm` from DI container
- [ ] Run app with `Application.Run(dashboardForm)`

### Validation

- [ ] Compile: `dotnet build WileyWidget.WinForms`
- [ ] Run app: `dotnet run --project WileyWidget.WinForms/WileyWidget.WinForms.csproj`
- [ ] Verify dashboard displays with 5 metric cards
- [ ] Click refresh button - verify it works
- [ ] Commit: `git commit -m "feat(dashboard): create WinForms UI with metric cards"`

**Success Criteria**: ✅ Form displays, refresh works, loading indicator shows, no crashes

---

## Phase 4: Unit Testing ⏱️ 2-3 hours

### 4.1 DashboardService Tests

- [ ] Create file: `tests/WileyWidget.WinForms.Tests/Services/DashboardServiceTests.cs`
- [ ] Create test class with mock logger
- [ ] Test: `GetDashboardSummaryAsync_ReturnsValidSummary`
  - [ ] Assert summary is not null
  - [ ] Assert 5 metrics returned
  - [ ] Assert municipality name is correct
- [ ] Test: `GetDashboardSummaryAsync_ReturnsCorrectMetrics`
  - [ ] Assert all 5 metric names exist
- [ ] Test: `RefreshDataAsync_CompletesSuccessfully`
  - [ ] Assert no exceptions thrown

### 4.2 DashboardViewModel Tests

- [ ] Create file: `tests/WileyWidget.WinForms.Tests/ViewModels/DashboardViewModelTests.cs`
- [ ] Create test class with mocks for service and logger
- [ ] Test: `LoadDashboardAsync_PopulatesMetrics`
  - [ ] Setup mock service to return test data
  - [ ] Execute LoadDashboardCommand
  - [ ] Assert metrics collection populated
  - [ ] Assert municipality name and fiscal year set
- [ ] Test: `LoadDashboardAsync_HandlesErrors`
  - [ ] Setup mock service to throw exception
  - [ ] Execute LoadDashboardCommand
  - [ ] Assert ErrorMessage contains error text
- [ ] Test: `LoadDashboardAsync_SetsLoadingState`
  - [ ] Track IsLoading property changes
  - [ ] Assert true during execution, false after
- [ ] Test: `RefreshDashboardAsync_CallsServiceAndReloads`
  - [ ] Execute RefreshDashboardCommand
  - [ ] Verify service methods called

### Validation

- [ ] Run all tests: `dotnet test tests/WileyWidget.WinForms.Tests --logger "console;verbosity=detailed"`
- [ ] Verify 8/8 tests pass (3 service + 4 ViewModel + 1 refresh)
- [ ] Check coverage: `dotnet test --collect:"XPlat Code Coverage"`
- [ ] Commit: `git commit -m "test(dashboard): add comprehensive unit tests"`

**Success Criteria**: ✅ All tests pass, coverage > 80%, no flaky tests

---

## Phase 5: Integration & Validation ⏱️ 1-2 hours

### 5.1 Manual Testing

- [ ] Launch app
- [ ] Verify dashboard opens as main window
- [ ] Check header displays "Town of Wiley - FY 2025"
- [ ] Verify 5 metric cards display in 2-column grid
- [ ] Check metric formatting (currency, percentages)
- [ ] Verify trend indicators show correct colors
- [ ] Click Refresh button
- [ ] Verify loading indicator appears
- [ ] Verify button disables during refresh
- [ ] Verify metrics update (timestamps change)

### 5.2 Error Testing

- [ ] Modify `DashboardService` to throw exception
- [ ] Rebuild and run app
- [ ] Verify error message displays in red
- [ ] Verify app doesn't crash
- [ ] Revert error code

### 5.3 Performance Testing

- [ ] Measure dashboard load time (< 1 second)
- [ ] Measure refresh time (< 1 second)
- [ ] Check memory usage (< 100 MB after 5 refreshes)
- [ ] Verify no UI freezing

### 5.4 CI/CD Integration

- [ ] Open `.github/workflows/ci-optimized.yml`
- [ ] Add Dashboard test step with filter
- [ ] Add artifact upload for test results
- [ ] Commit workflow changes
- [ ] Push to remote and verify CI passes

### 5.5 Documentation

- [ ] Update `README.md` with Dashboard feature section
- [ ] Add running instructions
- [ ] Add testing instructions
- [ ] Document architecture (ViewModel → Service → Models)
- [ ] Commit: `git commit -m "docs(dashboard): update README with Dashboard feature info"`

### Final Validation

- [ ] Run full quality gate: `trunk check --ci`
- [ ] Run all Dashboard tests: `dotnet test --filter FullyQualifiedName~Dashboard`
- [ ] Build release: `dotnet build WileyWidget.sln --configuration Release`
- [ ] Push to remote: `git push origin upgrade-to-NET10`
- [ ] Verify GitHub Actions CI passes
- [ ] Tag release: `git tag v0.1.0-dashboard` (optional)

**Success Criteria**: ✅ All tests pass, docs updated, CI green, zero warnings

---

## 📊 Definition of Done (DoD)

### Code Quality

- [ ] Zero compiler errors
- [ ] Zero compiler warnings
- [ ] Zero analyzer warnings
- [ ] Trunk checks pass (`trunk check --ci`)
- [ ] No TODO/FIXME comments in production code

### Testing

- [ ] All unit tests pass (8/8 minimum)
- [ ] Code coverage > 80% for Dashboard slice
- [ ] Manual test checklist 100% complete
- [ ] CI/CD pipeline runs tests automatically

### Documentation

- [ ] README.md updated with Dashboard section
- [ ] Inline XML comments for public APIs
- [ ] Architecture documented in plan
- [ ] Commit messages follow conventional commits

### Performance

- [ ] Dashboard loads in < 1 second
- [ ] Refresh completes in < 1 second
- [ ] Memory usage < 100 MB
- [ ] No UI freezing

### Integration

- [ ] DI configured correctly in Program.cs
- [ ] All dependencies injected properly
- [ ] Error handling in place
- [ ] Logging statements added

---

## 🚨 Blockers & Risks

### Potential Issues

1. **Project reference errors**: Ensure all projects reference correct abstractions
2. **DI registration**: Verify service lifetimes (Singleton vs Transient)
3. **Async deadlocks**: Always use `async/await`, never `.Result` or `.Wait()`
4. **WinForms threading**: Use `Invoke()` for cross-thread UI updates

### Mitigation

- Run `dotnet build` frequently
- Test early and often
- Use MCP filesystem tools for all file operations
- Consult Microsoft Learn docs for WinForms best practices

---

## 📝 Commit Log Template

```bash
# Phase 0
git commit -m "chore(dashboard): setup test infrastructure for Dashboard E2E"

# Phase 1
git commit -m "feat(dashboard): extract DashboardMetric and DashboardSummary models"
git commit -m "feat(dashboard): create IDashboardService interface"
git commit -m "feat(dashboard): implement mock DashboardService with 5 metrics"

# Phase 2
git commit -m "feat(dashboard): enhance DashboardViewModel with service injection"
git commit -m "feat(dashboard): add async Load and Refresh commands"

# Phase 3
git commit -m "feat(dashboard): create WinForms DashboardForm with metric cards"
git commit -m "feat(dashboard): implement data binding and UI updates"
git commit -m "feat(dashboard): setup DI container in Program.cs"

# Phase 4
git commit -m "test(dashboard): add DashboardService unit tests (3 tests)"
git commit -m "test(dashboard): add DashboardViewModel unit tests (4 tests)"

# Phase 5
git commit -m "ci(dashboard): add Dashboard tests to CI pipeline"
git commit -m "docs(dashboard): update README with Dashboard feature documentation"
git commit -m "chore(dashboard): mark Dashboard E2E as complete"
```

---

## ✅ Final Checklist

- [ ] All phases 0-5 complete
- [ ] All tasks checked off
- [ ] DoD criteria met
- [ ] No blockers or risks unresolved
- [ ] Code pushed to remote
- [ ] CI/CD passing
- [ ] Documentation updated
- [ ] Manifest regenerated
- [ ] Team notified (if applicable)

---

**Status**: 🚧 IN PROGRESS  
**Started**: [Date]  
**Target Completion**: [Date + 2-3 days]  
**Completed**: \***\*\_\_\_\*\***
