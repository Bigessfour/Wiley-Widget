# CSX Test Naming Convention

## Naming Standard

All CSX test files follow this pattern:

```
[number][status]-[descriptive-name].csx
```

### Status Indicators

- **No suffix** - Test is new or has not been validated
- **P suffix** - Test **PASSES** (100% success rate)
- **F suffix** - Test **FAILS** (known issues, needs fixing)
- **R suffix** - Test needs **REFACTORING** (too complex, outdated dependencies, or architecture changes needed)
- **W suffix** - Test has **WARNINGS** (passes but with issues)

### Examples

```
60P-dashboardviewmodel-unit-test.csx    ✓ PASSING (21/21 tests)
61P-quickbooksservice-unit-test.csx     ✓ PASSING (10/10 tests)
45R-dbcontextfactory-integration-test.csx  ⚠ NEEDS REFACTORING (DryIoc API changes)
11R-encrypted-vault-test-clean.csx      ⚠ NEEDS REFACTORING (Secret vault updates)
32-dashboard-navigation-analyzer.csx    ⚠ Not yet validated
```

## Workflow

1. **Create Test**: `62-new-feature-test.csx`
2. **Run Test**: `docker run --rm -v "${PWD}:/app:ro" wiley-widget/csx-mcp:local scripts/examples/csharp/62-new-feature-test.csx`
3. **On Success (100%)**: Rename to `62P-new-feature-test.csx`
4. **On Failure**: Keep as `62-new-feature-test.csx` or mark as `62F-new-feature-test.csx`
5. **Update VS Code Task**: Update `.vscode/tasks.json` with new filename

## Benefits

- **Quick Status Check**: `ls *P-*.csx` shows all passing tests
- **CI/CD Ready**: Only run P-suffixed tests in pipeline for reliable builds
- **Test Suite Health**: Track passing vs. failing tests at a glance
- **Git History**: Filename changes indicate test status improvements

## Current Passing Tests (2025-11-09)

| Test                                       | Description                | Tests | Pass Rate | Last Updated |
| ------------------------------------------ | -------------------------- | ----- | --------- | ------------ |
| `01P-basic-test.csx`                       | Basic C# evaluation demo   | N/A   | 100%      | 2025-11-09   |
| `06P-audit-test.csx`                       | Audit trail testing        | N/A   | 100%      | 2025-11-09   |
| `07P-theme-test.csx`                       | Theme system testing       | N/A   | 100%      | 2025-11-09   |
| `08P-excelexport-test.csx`                 | Excel export functionality | N/A   | 100%      | 2025-11-09   |
| `09P-xai-test.csx`                         | XAI integration testing    | N/A   | 100%      | 2025-11-09   |
| `50P-startup-orchestrator-test.csx`        | Startup orchestration      | 48    | 100%      | 2025-11-09   |
| `52P-database-initializer-fluent-test.csx` | DB initializer fluent API  | 15    | 100%      | 2025-11-09   |
| `60P-dashboardviewmodel-unit-test.csx`     | ViewModel MVVM patterns    | 21    | 100%      | 2025-11-09   |
| `61P-quickbooksservice-unit-test.csx`      | OAuth2 & API integration   | 10    | 100%      | 2025-11-09   |

**Total Passing Tests**: 9
**Total Test Assertions**: 94+
**Overall Pass Rate**: 100%

## Tests Needing Refactoring (2025-11-09)

| Test                                               | Reason                                      | Priority |
| -------------------------------------------------- | ------------------------------------------- | -------- |
| `05R-repository-tests-simplified.csx`              | Update for current repository patterns      | Medium   |
| `11R-encrypted-vault-test-clean.csx`               | Secret vault API changes                    | Medium   |
| `45R-dbcontextfactory-integration-test.csx`        | DryIoc.Microsoft.DependencyInjection v6.2.0 | High     |
| `45R-dbcontextfactory-integration-test-simple.csx` | Simplified version needs same updates       | High     |
| `46R-dbcontext-configuration-test.csx`             | EF Core configuration updates               | Medium   |
| `47R-inmemory-migration-test.csx`                  | EF9 migration patterns                      | Medium   |
| `48R-ef9-warnings-validation-test.csx`             | EF9 API changes                             | Low      |
| `49R-database-initializer-test.csx`                | Database initialization refactor            | Medium   |
| `51R-database-initializer-dependencies-test.csx`   | DI pattern updates                          | Medium   |

**Total Tests Needing Refactoring**: 9

## Maintenance

When a P-suffixed test starts failing:

1. Investigate the root cause
2. If code changed: Fix the code
3. If test needs updating: Fix the test
4. If persistently broken: Rename to F-suffix temporarily
5. Re-validate and restore P-suffix when fixed

## Integration with VS Code Tasks

Tasks are named with the suffix for clarity:

```json
"csx:run-60P-dashboardviewmodel-test"  // Runs passing test
"csx:run-61P-quickbooksservice-test"   // Runs passing test
"csx:run-viewmodel-service-tests"      // Runs all passing ViewModel/Service tests
```
