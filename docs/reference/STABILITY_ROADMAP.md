# Wiley-Widget Stability Roadmap: 100% Completion Checklist

**Version**: 1.0  
**Last Updated**: [Insert Date, e.g., 2025-11-XX]  
**Owner**: [Your Name/Solo Dev]  
**Goal**: Achieve 100% stability on existing features (no new features until complete). All work on `hotfix/` branches; merge via PR with Trunk checks, 100% tests, and self-review.  
**Principles**: Use `@Sequential Thinking MCP` for planning; `@C# MCP` for code audits; Trunk for lint/build; validators (e.g., `startup_validator.py`) for verification. Track progress here—commit updates to `main` after PR approval.  
**Current Baseline Metrics** (Update after Phase 1 diagnostics):

- Test Coverage: [e.g., 65%]
- Open TODOs/Stubs: [e.g., 5]
- Validators Passing: [e.g., 80%]
- Warnings (EF/Nullability): [e.g., 3]

## Phase 1: Diagnostics and Baseline (Days 1-2 | Setup & Audit)

- [ ] **Run Comprehensive Diagnostics** (Estimated: 2-3 hours)
  - [ ] Execute `trunk check --fix` for lint/format.
    - Validation Notes: [Output summary; e.g., "Fixed 2 Python formats"].
  - [ ] Run `dotnet test --coverage` (C#) and `pytest -v` (Python) via tasks.json.
    - Validation Notes: [Coverage %; attach log if failures].
  - [ ] Execute validators: `tools/run-startup-validator.ps1` and CSX scripts (e.g., `95-comprehensive-lifecycle-validation.csx`).
    - Validation Notes: [Pass/fail; e.g., "EF warnings in 48R script"].
  - [ ] Semantic search: Use `codebase "stubs placeholders incomplete features"` to list TODOs.
    - Validation Notes: [List items; cross-ref `TODO_CATALOG.md`].
  - [ ] Branch audit: `git log --oneline -20` and `git status`.
    - Validation Notes: [`main` clean? Y/N].
  - **Phase 1 Complete?** [ ] Yes (Date: \_**\_ | Metrics Update: \_\_**)

- [ ] **Setup Branch Protection and Baseline Report** (Estimated: 1 hour)
  - [ ] Protect `main` in GitHub (require PRs, Trunk checks: Lint/Build/Tests).
    - Validation Notes: [Screenshot/link to settings].
  - [ ] Create baseline summary in this file (or `STABILITY_BASELINE.md`).
    - Validation Notes: [Key findings; e.g., "5 stubs identified"].
  - **Quick Wins Applied?** [ ] (e.g., Auto-fixes from Trunk; `grep_search "TODO|FIXME"` scan).
  - **Phase 1 Complete?** [ ] Yes (Date: \_**\_ | Metrics Update: \_\_**)

**Phase 1 Milestone**: Baseline report done; diagnostics pass without fatals. No uncommitted changes on `main`.

## Phase 2: High-Priority Fixes (Days 3-7 | Core Stability)

- [ ] **Remove/Implement Stubs and Placeholders** (Estimated: 4-6 hours)
  - [ ] Branch: `hotfix/remove-stubs`. Target `Class1.cs` files (e.g., in Services.Uno, Models).
    - Validation Notes: [List deleted/implemented; use `@C# MCP` for suggestions].
  - [ ] Handle incomplete events (e.g., `BudgetsSyncedEvent.cs` handlers).
    - Validation Notes: [Test run: `dotnet test` passes?].
  - [ ] Verify: `trunk check` and `codebase "stub removal validation"`.
    - Validation Notes: [No regressions].
  - [ ] PR/Merge: After unit tests.
  - **Phase 2 Task Complete?** [ ] Yes (Date: \_\_\_\_)

- [ ] **Fix Dependencies and Warnings** (Estimated: 3-4 hours)
  - [ ] Branch: `hotfix/fix-ef-warnings`. Update `AppDbContext.cs` (e.g., [NotNull] attributes).
    - Validation Notes: [Run `48R-ef9-warnings-validation-test.csx`; 0 warnings?].
  - [ ] Nullability: Follow `docs/reference/NULLABILITY_MIGRATION.md`.
    - Validation Notes: [Build with `--warnAsError`].
  - [ ] QuickBooks: Add Polly retries to `QuickBooksService.cs`.
    - Validation Notes: [Offline mock test via `MockFactory.cs`; per `POLLY_ENHANCEMENT_RECOMMENDATIONS.md`].
  - [ ] Verify: `dotnet ef migrations add --dry-run`.
    - Validation Notes: [Full build clean].
  - [ ] PR/Merge: After integration tests.
  - **Phase 2 Task Complete?** [ ] Yes (Date: \_\_\_\_)

- [ ] **Complete Data Seeding and Audit** (Estimated: 2-3 hours)
  - [ ] Branch: `hotfix/complete-seeding`. Enhance `DatabaseSeeder.cs` (e.g., conservation accounts).
    - Validation Notes: [Run seeder in Dev Container; check `sql/` scripts].
  - [ ] Tie audits to telemetry (`AuditInterceptor.cs` + SigNoz).
    - Validation Notes: [Test `tools/explore/utility-customer-repository.csx`].
  - [ ] PR/Merge: After integration tests.
  - **Phase 2 Task Complete?** [ ] Yes (Date: \_\_\_\_)

- [ ] **Edge-Case Handling** (Estimated: 2 hours)
  - [ ] Branch: `hotfix/edge-cases`. Enhance `DispatcherGuard.cs`; test caching leaks.
    - Validation Notes: [Run `85-secret-lifecycle-torture-test.csx` adapted; `scripts/maintenance/validate-di-registrations.ps1`].
  - [ ] Add null checks; verify no leaks.
    - Validation Notes: [Load test output].
  - [ ] PR/Merge.
  - **Phase 2 Complete?** [ ] Yes (Date: \_\_\_\_ | Metrics: Coverage >80%, Zero stubs/warnings)

**Phase 2 Milestone**: High-priority gaps closed; validators pass 100%.

## Phase 3: Testing and Polish (Days 8-14 | Comprehensive Validation)

- [ ] **Expand Testing** (Estimated: 6-8 hours)
  - [ ] Branch: `hotfix/boost-testing`. Add E2E for budget import > QuickBooks sync > export.
    - Validation Notes: [New tests in `WileyWidget.Services.Tests/`; `codebase "untested paths"`].
  - [ ] Python E2E: Enhance `tests/test_docker_python_execution.py`.
    - Validation Notes: [Run `dotnet test --coverage --collect:"XPlat Code Coverage"`; >90%?].
  - [ ] PR/Merge after coverage check.
  - **Phase 3 Task Complete?** [ ] Yes (Date: \_\_\_\_)

- [ ] **Integrate Scanners and Maintenance** (Estimated: 3 hours)
  - [ ] Branch: `hotfix/automate-maintenance`. Add scanners (e.g., `animation_scanner.py`) to `.trunk/trunk.yaml` actions.
    - Validation Notes: [Run `trunk check --changed`; `scripts/maintenance/cleanup-repo-bloat.ps1`].
  - [ ] PR/Merge after CI sim.
  - **Phase 3 Task Complete?** [ ] Yes (Date: \_\_\_\_)

- [ ] **UI and Performance Polish** (Estimated: 3-4 hours)
  - [ ] Branch: `hotfix/ui-polish`. Optimize XAML (e.g., visibility in `BudgetOverviewPage.xaml`).
    - Validation Notes: [Apply `@XAML Optimization`; test `96-winui-theme-validation-test.csx`].
  - [ ] Threading fixes per `docs/reference/UI_THREADING_GUIDELINES.md`.
    - Validation Notes: [Runtime in Dev Container; SigNoz metrics].
  - [ ] PR/Merge.
  - **Phase 3 Task Complete?** [ ] Yes (Date: \_\_\_\_)

- [ ] **Docs and Final Audit** (Estimated: 4 hours)
  - [ ] Branch: `hotfix/update-docs`. Update `TODO_CATALOG.md`; consolidate migration notes.
    - Validation Notes: [Full validators/CSX pass; `@Sequential Thinking MCP` review].
  - [ ] Create `STABILITY_REPORT.md` summary.
    - Validation Notes: [100% confirmation].
  - [ ] PR/Merge.
  - **Phase 3 Complete?** [ ] Yes (Date: \_\_\_\_ | Metrics: 90%+ coverage, All validators pass)

**Phase 3 Milestone**: E2E runs clean; docs current.

## Phase 4: Ongoing Maintenance and Lockdown (Day 15+ | Sustain)

- [ ] **CI Automation** (Estimated: 2 hours)
  - [ ] Update `.github/workflows/build-and-publish.yml` for validators/Trunk on PRs.
    - Validation Notes: [Test workflow on dummy PR].

- [ ] **Monitoring Setup** (Estimated: 1 hour)
  - [ ] Enable SigNoz dashboards (e.g., cache/exceptions).
    - Validation Notes: [Dashboard link/screenshot].

- [ ] **Lockdown** (Ongoing)
  - [ ] Tag `main` as "v1.0-stable".
    - Validation Notes: [Git tag created].
  - [ ] For new features: Require stability sign-off before `feature/` branches.
    - Validation Notes: [Repo rule updated].

- [ ] **Quarterly Reviews** (Ongoing)
  - [ ] Schedule: `trunk check` + full tests; `@Gh MCP` for issues.
    - Validation Notes: [Next date: ____].

**Phase 4 Complete?** [ ] Yes (Date: \_\_\_\_ | Overall Metrics: 100% Stable)

## Progress Metrics Table

| Phase       | Status      | Test Coverage % | Open Issues | Last Updated  | Notes                   |
| ----------- | ----------- | --------------- | ----------- | ------------- | ----------------------- |
| 1           | [ ] Planned | [ ]             | [ ]         | [ ]           | Baseline pending        |
| 2           | [ ] Planned | [ ]             | [ ]         | [ ]           | High-priority fixes     |
| 3           | [ ] Planned | [ ]             | [ ]         | [ ]           | Testing & Polish        |
| 4           | [ ] Planned | [ ]             | [ ]         | [ ]           | Maintenance             |
| **Overall** | [ ] 0%      | [ ]             | [ ]         | [Insert Date] | Update after each merge |

**Change Log** (Append commits here for history):

- v1.0: Initial creation (Date: \_\_\_\_).
- [Add updates, e.g., "v1.1: Phase 1 complete, coverage 75% (PR #XX)"].
