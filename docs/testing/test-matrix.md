# Test Matrix Audit

This document tracks test coverage for critical components in Wiley Widget.

**Philosophy**: Track what matters, not everything. Update after adding tests.

## System Testing (Evidence)

- **Defined criteria**: `WileyWidget: Build` succeeds; `test: viewmodels` succeeds; `test: ui-integration` succeeds; `test: ui-e2e` succeeds.
- **Most recent run** (from [TEST_COVERAGE_REGISTRY.md](docs/reference/TEST_COVERAGE_REGISTRY.md)): 433 tests (404 pass, 29 fail); coverage ~10-64%.
- **Result**: 🟡 Partial (UI gaps remain).

## Test Coverage by Category

### Repositories

| Component                  | Key Methods                            | Tests | Coverage | Notes                                                                                             |
| -------------------------- | -------------------------------------- | ----- | -------- | ------------------------------------------------------------------------------------------------- |
| BudgetRepository           | CRUD/GetPagedAsync/Variance            | 52    | 95%      | [BudgetRepositoryTests.cs](WileyWidget.Tests/RepositoryTests/BudgetRepositoryTests.cs); High prio |
| MunicipalAccountRepository | SearchByNameAsync/AddAsync/UpdateAsync | 65    | 85%+     | EF InMemory, Moq; Paging/sort; QB sync                                                            |
| DepartmentRepository       | GetAllAsync/Filter                     | 22    | Unknown  | EF InMemory; Medium prio                                                                          |
| EnterpriseRepository       | \*Async                                | 25    | Unknown  | EF InMemory; Medium prio                                                                          |
| AuditRepository            | \*Async                                | 0     | 0%       | **Untested**; Medium prio                                                                         |
| Utility\*Repository        | \*Async                                | 0     | 0%       | **Untested**; High prio                                                                           |

### Services

| Component         | Key Methods                                       | Tests | Coverage | Notes                                                                      |
| ----------------- | ------------------------------------------------- | ----- | -------- | -------------------------------------------------------------------------- |
| QuickBooksService | GetAuthorizationUrlAsync                          | 2+    | >70%     | ISecretVaultService, HttpClient; OAuth; [QuickBooksServiceTests](inferred) |
| QuickBooksService | ExchangeCodeForTokenAsync/RefreshAccessTokenAsync | 6+    | >70%     | HttpClient, ISecretVaultService; Token handling; SyncBudgetsToAppAsync     |
| QuickBooksService | GetConnectionStatusAsync/Sync\*                   | 4+    | >60%     | ISecretVaultService; Status/sync                                           |

### ViewModels

| Component          | Key Methods                    | Tests | Coverage | Notes      |
| ------------------ | ------------------------------ | ----- | -------- | ---------- |
| DashboardViewModel | WelcomeMessage/PropertyChanged | 4+    | >80%     | None; Unit |

### UI/E2E

| Component           | Key Methods              | Tests | Coverage | Notes                                                                                                             |
| ------------------- | ------------------------ | ----- | -------- | ----------------------------------------------------------------------------------------------------------------- |
| AnalyticsPanel      | RunScenarioCommand/Grid  | 10+   | ~60%     | Fake\*Services; [AnalyticsPanelE2ETests.cs](tests/WileyWidget.WinForms.E2ETests/AnalyticsPanelE2ETests.cs); UI=UI |
| UI E2E (Panels)     | Load/Interact (7 panels) | 100+  | 60%      | TestAppFixture; FlaUI; `test: ui` task                                                                            |
| Theme/SfSkinManager | ApplyTheme/Propagation   | 6+    | 100%     | None; SyncfusionThemingTests                                                                                      |

## Summary

- **Total Test Files**: 50+
- **Total Tests**: ~600+ (433 xUnit measured)
- **Coverage Target**: 80% critical paths (met for Budget/MuniAccount)
- **Whitewash Enforcement**: FALSE

## Test Categories

### Unit (~433): xUnit + FluentAssertions + Moq

### Integration (~50): TestContainers + Respawn

### UI/E2E (100+): FlaUI + [StaFact]

### CSX/MCP: 6+ scripts (exploratory)

## Adding New Rows

When you add a new test, add a row to the appropriate category table above with the same columns (Component, Key Methods, Tests, Coverage, Notes).

## Audit Frequency

- Update: After each test session
- Review: Weekly (Friday)
- Cleanup: Monthly (remove obsolete tests)
