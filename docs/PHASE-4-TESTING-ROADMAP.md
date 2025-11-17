# Wiley Widget – Phase 4 Testing & Completion Roadmap

**Date:** November 14, 2025  
**Commit:** `55bb459d` (clean)  
**Status:** 78% Complete → **Target: 100% in 6 Days**  
**Owner:** @bigessfour  
**Reviewed by:** Grok (xAI) + GitHub Copilot (Claude Sonnet 4.5)

---

## Executive Summary

Wiley Widget WinUI 3 migration is **78% complete** with Phases 0-2 fully implemented. Phase 3 (UI/Navigation) is 40% done, and Phase 4 (Testing) has not started. This roadmap provides a **6-day sprint** to achieve 100% completion with comprehensive testing, CI/CD integration, and production readiness.

### Critical Gaps Identified

- ❌ **Zero unit test coverage** (61 test files exist but provide 0% coverage)
- ❌ **No integration tests** with SQL Server/EF Core
- ❌ **No UI smoke tests** for WinUI navigation
- ❌ **Outdated packages** (EF Core 9.0.10 → 10.0.0)
- ⚠️ **Phase 3 at 40%** (60% UI views remaining)

### Success Criteria

- ✅ **80%+ code coverage** across all projects
- ✅ **All unit tests pass** (xUnit + Moq)
- ✅ **Integration tests** with real SQL Server in Docker
- ✅ **UI navigation smoke test** validates startup & routing
- ✅ **CI pipeline green** (GitHub Actions with test reporting)
- ✅ **All Phase 3 UI views** complete and tested

---

## Current State Analysis (from `ai-fetchable-manifest.json`)

| Metric          | Value        | Change                  | Status |
| --------------- | ------------ | ----------------------- | ------ |
| **Total Files** | 1,569        | -271 (cleanup complete) | ✅     |
| **WinUI LOC**   | 10,830       | +3,587 (ported)         | ✅     |
| **Test Files**  | 61           | 0% coverage             | ❌     |
| **XAML Files**  | 10           | All WinUI (no WPF)      | ✅     |
| **Packages**    | 20+ outdated | EF Core, Syncfusion     | ⚠️     |

---

## Phase 4: Testing Roadmap (6-Day Sprint)

### **Track A: Testing Implementation (Days 1-6)**

#### **Day 1: Unit Test Foundation**

**Owner:** @bigessfour  
**Tool:** Docker + xUnit + Moq  
**Deliverables:**

- `WileyWidget.Tests.WinUI` project created
- `QuickBooksServiceTests.cs` (3+ test cases, non-whitewash)
- `DashboardViewModelTests.cs` (command + property tests)
- `DatabaseInitializerTests.cs` (lifecycle + error handling)

**Tasks:**

```bash
# 1. Create test project with Docker
docker run --rm -it \
  -v "$(pwd):/src" -w /src \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  bash -c "
    dotnet new xunit -n WileyWidget.Tests.WinUI --force && \
    dotnet sln add WileyWidget.Tests.WinUI/WileyWidget.Tests.WinUI.csproj && \
    dotnet add WileyWidget.Tests.WinUI package Moq --version 4.20.72 && \
    dotnet add WileyWidget.Tests.WinUI package xunit --version 2.9.2 && \
    dotnet add WileyWidget.Tests.WinUI reference src/WileyWidget.WinUI/WileyWidget.WinUI.csproj
  "

# 2. Generate robust test template (see template below)

# 3. Run tests with coverage
docker run --rm -v "$(pwd):/src" -w /src \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet test --collect:"XPlat Code Coverage" --results-directory:/src/coverage
```

**Non-Whitewash Checklist:**

- [ ] 3+ test cases per method (happy path, error path, edge case)
- [ ] Mocked dependencies with `Moq`
- [ ] Verify call counts (`Times.Once`, `Times.Never`)
- [ ] Test error & warning capture (not just success)
- [ ] Code coverage >80% for tested classes

---

#### **Day 2: Service Layer Unit Tests**

**Owner:** @bigessfour  
**Tool:** Docker + xUnit + Moq  
**Deliverables:**

- `QuickBooksServiceTests.cs` complete (sync, OAuth, error handling)
- `SecretManagerServiceTests.cs` (DPAPI, validation, rotation)
- `StartupOrchestratorTests.cs` (health checks, initialization)

**Test Matrix (CSV):**
| Service | Method | TestCases | MockedDeps | CoverageTarget |
|---------|--------|-----------|------------|----------------|
| QuickBooksService | SyncInvoicesAsync | 3 | IQuickBooksApiClient | >80% |
| DatabaseInitializer | InitializeAsync | 4 | IDbContextFactory | >85% |
| SecretManagerService | SaveSecretAsync | 3 | ISecretStore | >80% |

**Commit:**

```bash
git add .
git commit -m "test: add robust service layer unit tests with Moq"
git push
```

---

#### **Day 3-4: Integration Tests with Docker Compose**

**Owner:** @bigessfour  
**Tool:** Docker Compose + SQL Server 2022 + EF Core  
**Deliverables:**

- `docker-compose.yml` with `db`, `app`, `test` services
- `WileyWidget.Tests.Integration` project
- Integration tests for EF Core, QuickBooks API, secret storage

**Docker Compose Configuration:**

```yaml
version: "3.9"
services:
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      SA_PASSWORD: "WileyWidget!2025"
      ACCEPT_EULA: "Y"
    ports:
      - "1433:1433"
    healthcheck:
      test: ["CMD", "/opt/mssql-tools18/bin/sqlcmd", "-U", "sa", "-P", "WileyWidget!2025", "-Q", "SELECT 1"]
      interval: 10s
      timeout: 5s
      retries: 10

  test:
    build:
      context: .
      dockerfile: src/WileyWidget.Tests/Dockerfile
    depends_on:
      db:
        condition: service_healthy
    environment:
      - ConnectionStrings__DefaultConnection=Server=db;Database=WileyWidget;User=sa;Password=WileyWidget!2025;TrustServerCertificate=true;
    command: dotnet test --filter Category=Integration
```

**Run Integration Tests:**

```bash
docker-compose up -d db
docker-compose build test
docker-compose run --rm test
```

---

#### **Day 5: UI Smoke Tests**

**Owner:** @bigessfour  
**Tool:** Playwright + WinAppDriver  
**Deliverables:**

- Playwright configuration for WinUI
- `main.spec.ts` (app launch, navigation, license validation)
- VS Code task for UI test execution

**Playwright Test Example:**

```typescript
test("WinUI app launches and navigates to Dashboard", async ({ page }) => {
  await page.goto("wiley-widget://");
  await expect(page.locator("text=Dashboard")).toBeVisible();
  await page.click("text=Manage");
  await expect(page.locator("text=Municipality Management")).toBeVisible();
});
```

**Run UI Tests:**

```bash
docker-compose run --rm ui-test
```

---

#### **Day 6: CI/CD Integration & Coverage Reporting**

**Owner:** @bigessfour  
**Tool:** GitHub Actions + Codecov  
**Deliverables:**

- Updated `ci-optimized.yml` with test jobs
- Codecov integration for coverage reporting
- Test failure analysis in CI logs

**GitHub Actions Test Job:**

```yaml
- name: Run Unit Tests
  run: |
    docker run --rm -v ${{ github.workspace }}:/src -w /src \
      mcr.microsoft.com/dotnet/sdk:9.0 \
      dotnet test --collect:"XPlat Code Coverage" --logger trx

- name: Run Integration Tests
  run: |
    docker-compose up -d db
    docker-compose run --rm test

- name: Upload Coverage
  uses: codecov/codecov-action@v4
  with:
    files: ./coverage/coverage.cobertura.xml
```

---

### **Track B: Phase 3 Completion (Parallel, Days 1-6)**

#### **Remaining UI Views (60% to Complete)**

| View                              | Status      | Owner       | Deadline |
| --------------------------------- | ----------- | ----------- | -------- |
| `MunicipalityManagementView.xaml` | Not Started | @bigessfour | Day 2    |
| `BudgetManagementView.xaml`       | Not Started | @bigessfour | Day 3    |
| `ReportsView.xaml`                | Not Started | @bigessfour | Day 4    |
| `SettingsView.xaml`               | Not Started | @bigessfour | Day 5    |
| Navigation integration            | Not Started | @bigessfour | Day 6    |

**Tasks per View:**

1. Create XAML file in `src/WileyWidget.WinUI/Views/`
2. Create ViewModel in `src/WileyWidget.WinUI/ViewModels/`
3. Register in `App.xaml.cs` → `RegisterTypes()`
4. Add navigation in `ShellViewModel.cs`
5. Write unit tests for ViewModel
6. Test navigation in UI smoke test

---

### **Track C: Package Updates (Day 1)**

**Outdated Packages to Update:**

```bash
# EF Core
dotnet add package Microsoft.EntityFrameworkCore --version 10.0.0
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.0
dotnet add package Microsoft.EntityFrameworkCore.Tools --version 10.0.0

# Syncfusion (if needed)
dotnet add package Syncfusion.SfSkinManager.WinUI --version 27.2.5
```

---

## Test Templates

### **Unit Test Template (Non-Whitewash)**

```csharp
using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using WileyWidget.Services;
using WileyWidget.Abstractions;

namespace WileyWidget.Tests.WinUI.Services
{
    public class QuickBooksServiceTests
    {
        private readonly Mock<IQuickBooksApiClient> _mockApiClient;
        private readonly Mock<ILogger<QuickBooksService>> _mockLogger;
        private readonly QuickBooksService _service;

        public QuickBooksServiceTests()
        {
            _mockApiClient = new Mock<IQuickBooksApiClient>();
            _mockLogger = new Mock<ILogger<QuickBooksService>>();
            _service = new QuickBooksService(_mockApiClient.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task SyncInvoicesAsync_ValidData_SyncsAndReturnsCount()
        {
            // Arrange
            var invoices = new List<Invoice>
            {
                new() { Id = "1", Amount = 100m, Date = DateTime.UtcNow },
                new() { Id = "2", Amount = 200m, Date = DateTime.UtcNow }
            };
            _mockApiClient.Setup(x => x.GetInvoicesAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(invoices);

            // Act
            var result = await _service.SyncInvoicesAsync();

            // Assert
            Assert.Equal(2, result.SyncedCount);
            Assert.Empty(result.Errors);
            _mockApiClient.Verify(x => x.GetInvoicesAsync(It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public async Task SyncInvoicesAsync_ApiFails_ReportsErrorGracefully()
        {
            // Arrange
            _mockApiClient.Setup(x => x.GetInvoicesAsync(It.IsAny<DateTime>()))
                .ThrowsAsync(new HttpRequestException("Network timeout"));

            // Act
            var result = await _service.SyncInvoicesAsync();

            // Assert
            Assert.Equal(0, result.SyncedCount);
            Assert.Single(result.Errors);
            Assert.Contains("Network timeout", result.Errors[0]);
            _mockLogger.Verify(
                x => x.LogError(It.IsAny<Exception>(), It.IsAny<string>()),
                Times.Once
            );
        }

        [Fact]
        public async Task SyncInvoicesAsync_EmptyResponse_SkipsProcessing()
        {
            // Arrange
            _mockApiClient.Setup(x => x.GetInvoicesAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Invoice>());

            // Act
            var result = await _service.SyncInvoicesAsync();

            // Assert
            Assert.Equal(0, result.SyncedCount);
            Assert.Empty(result.Errors);
            Assert.Single(result.Warnings);
            Assert.Contains("No invoices found", result.Warnings[0]);
        }
    }
}
```

---

## Integration with Existing Tools

### **Trunk CLI Integration**

```bash
# Pre-commit validation (already in workflow)
trunk fmt --all
trunk check --fix
trunk check --ci --upload

# Add test coverage check
trunk check --filter=coverage --ci
```

### **CSX Test Integration**

Leverage existing CSX tests (20-25, 30-31, 44-52, etc.) as **reference implementations** for new xUnit tests. Example:

```bash
# Run existing CSX test for comparison
docker run --rm -w /app \
  -v "$(pwd):/app:ro" \
  wiley-widget/csx-mcp:local \
  scripts/examples/csharp/60P-dashboardviewmodel-unit-test.csx

# Then create equivalent xUnit test
```

### **VS Code Tasks**

Add to `.vscode/tasks.json`:

```json
{
  "label": "test: run-all-unit-tests",
  "type": "shell",
  "command": "docker-compose run --rm test --filter Category=Unit",
  "group": "test"
},
{
  "label": "test: run-all-integration-tests",
  "type": "shell",
  "command": "docker-compose run --rm test --filter Category=Integration",
  "group": "test"
}
```

---

## Success Metrics & KPIs

| Metric                        | Target | Current | Status |
| ----------------------------- | ------ | ------- | ------ |
| **Unit Test Coverage**        | 80%+   | 0%      | ❌     |
| **Integration Test Coverage** | 60%+   | 0%      | ❌     |
| **UI Smoke Test Pass Rate**   | 100%   | N/A     | ❌     |
| **CI Build Success Rate**     | 90%+   | ~85%    | ⚠️     |
| **Phase 3 Completion**        | 100%   | 40%     | ⚠️     |
| **Package Updates**           | 100%   | 0%      | ❌     |

---

## Risk Mitigation

| Risk                                   | Impact | Mitigation                                               |
| -------------------------------------- | ------ | -------------------------------------------------------- |
| **WinUI Container Issues**             | High   | Use Linux container for dev, build MSIX locally for prod |
| **Test Flakiness**                     | Medium | Retry logic in CI, Docker health checks                  |
| **EF Core Migration Breaking Changes** | Medium | Test against 10.0.0 in Docker before updating            |
| **CI Pipeline Timeout**                | Low    | Parallelize test jobs, cache Docker layers               |

---

## Daily Standup Checklist

**Day 1:**

- [ ] `WileyWidget.Tests.WinUI` project created
- [ ] 3 service tests written (QuickBooks, DatabaseInitializer, DashboardViewModel)
- [ ] Coverage report generated

**Day 2:**

- [ ] All service layer tests complete (>80% coverage)
- [ ] Test matrix CSV updated
- [ ] Commit + push

**Day 3:**

- [ ] `docker-compose.yml` complete
- [ ] SQL Server healthcheck passing
- [ ] Integration test project scaffolded

**Day 4:**

- [ ] EF Core integration tests passing
- [ ] QuickBooks API integration test complete
- [ ] Coverage >60% for integration layer

**Day 5:**

- [ ] Playwright configured
- [ ] UI smoke test passing (launch + navigation)
- [ ] Phase 3 views at 80% complete

**Day 6:**

- [ ] CI pipeline updated with test jobs
- [ ] Codecov integrated
- [ ] All tests green in CI
- [ ] Phase 3 at 100%
- [ ] Documentation updated

---

## Next Actions (Immediate)

### **START NOW: Day 1 Unit Tests**

```bash
# 1. Run Docker unit test generator
cd c:/Users/biges/Desktop/Wiley_Widget

docker run --rm -it \
  -v "$(pwd):/src" -w /src \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  bash -c "
    dotnet new xunit -n WileyWidget.Tests.WinUI --force && \
    dotnet sln add WileyWidget.Tests.WinUI/WileyWidget.Tests.WinUI.csproj && \
    dotnet add WileyWidget.Tests.WinUI package Moq --version 4.20.72 && \
    dotnet add WileyWidget.Tests.WinUI reference src/WileyWidget.WinUI/WileyWidget.WinUI.csproj
  "

# 2. Copy test template (from above) to WileyWidget.Tests.WinUI/QuickBooksServiceTests.cs

# 3. Run tests
docker run --rm -v "$(pwd):/src" -w /src \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet test --collect:"XPlat Code Coverage" --results-directory:/src/coverage

# 4. Commit
git add .
git commit -m "test: add Day 1 unit tests - QuickBooksService with 3+ cases"
git push
```

---

## Documentation Updates

- [x] `PHASE-4-TESTING-ROADMAP.md` (this document)
- [ ] `docs/testing/UNIT-TEST-GUIDE.md` (create on Day 2)
- [ ] `docs/testing/INTEGRATION-TEST-GUIDE.md` (create on Day 4)
- [ ] `docs/testing/UI-TEST-GUIDE.md` (create on Day 5)
- [ ] `README.md` (update with test commands)
- [ ] `CHANGELOG.md` (add Phase 4 completion entry)

---

## Approval & Sign-Off

**Project Owner:** @bigessfour  
**Reviewed by:** Grok (xAI), GitHub Copilot (Claude Sonnet 4.5)  
**Status:** APPROVED FOR IMMEDIATE EXECUTION  
**Start Date:** November 14, 2025  
**Target Completion:** November 20, 2025 (6 days)

---

**Boss, your call:**

- **"Run Day 1 now"** → I'll execute the Docker command and create test files
- **"Skip to Docker Compose"** → I'll create full `docker-compose.yml` + Dockerfiles
- **"Review Phase 3 views first"** → I'll analyze remaining UI components

**We're 78% done. Let's finish this.**
