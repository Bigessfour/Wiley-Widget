# Wiley Widget Pre-Release Checklist

This checklist is for release sign-off, not for optimistic progress reporting. A release candidate is ready only when the items below are true in the current repository state.

Use this together with `docs/V1_0_RELEASE_SCOPE.md` and `docs/V1_0_BLOCKER_MATRIX.md` so the team is validating a frozen release slice instead of an open-ended product wish list.

## 1. Scope Is Frozen

- [x] The release slice is explicitly defined.
- [ ] Out-of-scope panels and workflows are deferred, hidden, or documented as known limitations.
- [ ] No unrelated cleanup or opportunistic refactors are riding with release fixes.

Current evidence as of 2026-03-18:

- `docs/V1_0_RELEASE_SCOPE.md` defines the current proposed v1.0 slice and stop-ship conditions.
- `docs/V1_0_BLOCKER_MATRIX.md` converts that slice into explicit blockers and proof lanes.

## 2. Build Health Is Green

- [x] `build` completes without unresolved errors.
- [ ] If analyzers are part of the release lane, analyzer output is reviewed and clean enough for sign-off.
- [ ] A release publish build succeeds and the artifact can be launched on a clean Windows machine.

Current evidence as of 2026-03-19:

- `dotnet build WileyWidget.sln -m:2` completed successfully.
- `build: fast` completed successfully again on 2026-03-19 after the JARVIS automation visibility fix.
- Analyzer/sign-off cleanliness is not yet complete: the current build still reports `CA2024` in `src/WileyWidget.Services/QuickBooksDesktopImportService.cs`.
- Release publish and clean-machine launch validation remain open.

## 3. Proof Is Meaningful

- [ ] Every release-blocking fix has proof that the intended behavior works.
- [ ] Every high-risk fix also has proof that previously working behavior still works.
- [ ] Shared method changes are covered by regression proof for their existing callers.
- [x] Shell changes are covered by startup, navigation, theme, docking, or layout proof as appropriate.
- [x] No filtered zero-test run is being counted as evidence.
- [x] No stale report, old screenshot, or historical `.trx` is being treated as current proof.

Current evidence as of 2026-03-19:

- `MainFormStartupIntegrationTests.FullStartup_NormalConfig_SucceedsWithoutExceptions` passed 1/1.
- `PanelRegistryNavigationProofTests` passed 32/32.
- `JarvisChatFlaUiTests.JarvisChat_RendersNativeAssistView_WhenTabSelected` passed 1/1 after restoring the automation status marker for `WILEYWIDGET_UI_AUTOMATION_JARVIS`.
- Focused QuickBooks Desktop import proof passed 6/6 across `QuickBooksDesktopIifParserTests` and `QuickBooksDesktopImportServiceTests`.
- A fresh focused MainForm lane still fails 2 tests and skips 2 tests, so shared-method and high-risk shell regression proof is still incomplete for sign-off.
- Current focused MainForm failures are `MainFormIntegrationTests.RibbonBudgetButton_Click_ShowsInitializedBudgetPanel` and `MainFormIntegrationTests.UnifiedNavigationDropdown_SelectingJarvis_ActivatesRightDockTab`.

Additional current evidence as of 2026-03-21:

- `dotnet test tests/WileyWidget.LayerProof.Tests/WileyWidget.LayerProof.Tests.csproj --logger "console;verbosity=minimal"` passed 39/39.
- The Docker-backed data proof lane passed 2/2 through `ContainerizedSqlRepositoryProofTests`, using Testcontainers SQL Server rather than a developer-local database.
- The data proof lane now covers the live query surface for `IAccountsRepository`, `IBudgetRepository`, `IDepartmentRepository`, `IEnterpriseRepository`, `IMunicipalAccountRepository`, `IPaymentRepository`, `IUtilityBillRepository`, and `IScenarioSnapshotRepository`, plus scenario snapshot persistence round-trip proof.
- The abstractions proof lane passed 6/6 through `AbstractionsContractProofTests`, covering `Result`, `Result<T>`, `ResourceLoadResult`, `ResourceLoadException`, `RegionValidationResult`, interface contract signatures, and parity between `IErrorHandler` and `IExceptionHandler`.
- `dotnet test tests/WileyWidget.LayerProof.Tests/WileyWidget.LayerProof.Tests.csproj --filter "Category=Business" --logger "console;verbosity=minimal"` passed 22/22.
- The business proof lane now covers `AccountTypeValidator`, `GrokRecommendationOptions`, `RecommendationResult`, `AuditService`, `DepartmentExpenseService`, `GrokRecommendationService`, and `QuickBooksBudgetSyncService`.
- The business proof lane now includes deterministic reauthorization and cancellation fallback coverage for `DepartmentExpenseService`, deterministic endpoint-normalization and HTTP health coverage for `GrokRecommendationService`, and real SQL-backed account-name fallback coverage for `QuickBooksBudgetSyncService`.
- The Docker-backed business proof includes QuickBooks-to-budget actuals synchronization against SQL Server, including persisted `BudgetEntry.ActualAmount` updates, account-name fallback mapping, and `BudgetActualsUpdatedEvent` publication proof.
- `dotnet test tests/WileyWidget.LayerProof.Tests/WileyWidget.LayerProof.Tests.csproj --filter "Category=Models" --logger "console;verbosity=minimal"` passed 9/9.
- The models proof lane now covers `ChatMessage`, `EnterpriseSnapshot`, `EnterpriseMonthlyTrendPoint`, `AccountNumber`, `MunicipalAccount`, `Enterprise`, `BudgetEntry`, `BudgetInsights`, `ComplianceReport`, and the `AccountTypeValidator`, `BudgetDataValidator`, and `EnterpriseValidator` contracts.
- `dotnet test tests/WileyWidget.WinForms.Tests/WileyWidget.WinForms.Tests.csproj --filter "Category=ServiceProof" --logger "console;verbosity=minimal"` passed 9/9.
- The service proof lane now covers `AdaptiveTimeoutService`, `CorrelationIdService`, `CorrelatedAIServiceWrapper`, and `DataAnonymizerService`, including timeout percentile/bound behavior, correlation propagation and cleanup across async flows, and deterministic anonymization/cache behavior.

## 4. In-Scope Workflow Certification

- [x] Main startup completes without exceptions.
- [ ] Production navigation can open the in-scope panels.
- [ ] Critical business workflows for this release are exercised and verified.
- [x] Optional integrations included in the release have focused verification.
- [ ] In-scope panels are marked as Certified, Known Limitation, or Deferred using `Done_Checklist.md`.

Current evidence as of 2026-03-19:

- Startup proof is green: `MainFormStartupIntegrationTests.FullStartup_NormalConfig_SucceedsWithoutExceptions` passed 1/1.
- Focused panel-registry proof is green: `PanelRegistryNavigationProofTests` passed 32/32.
- The Payments production-navigation blocker has current passing proof in the release lane: `PaymentsPanelFlaUiTests` passed 1/1 and `PanelRegistrySmokeTests.ShellPanel_CanBeActivatedFromMainWindow(displayName: "Payments")` passed 1/1.
- A fresh explicit production-navigation rerun is still red for `QuickBooks`, so production navigation remains open for section sign-off.
- Focused QuickBooks integration proof is green: `QuickBooksDesktopIifParserTests` and `QuickBooksDesktopImportServiceTests` passed 6/6.
- `Done_Checklist.md` exists as the certification rubric, but the in-scope panel state tracking is not yet recorded there as Certified, Known Limitation, or Deferred.

## 5. Docs Match Reality

- [x] `README.md` describes the current product and release posture.
- [x] `QUICK_START.md` only lists commands and scripts that exist now.
- [x] `CONTRIBUTING.md` reflects the stabilization workflow.
- [x] Internal workflow docs under `.vscode/` and `.github/` do not point at missing files.
- [x] Release notes or changelog entries describe what actually shipped.

Current evidence as of 2026-03-18:

- Verified referenced workflow docs exist under `.vscode/` and `.github/`.
- Verified `README.md`, `QUICK_START.md`, and `CONTRIBUTING.md` reflect the current stabilization workflow.
- `CHANGELOG.md` contains shipped documentation and release entries for the current desktop/WinForms posture.

## 6. Packaging And Distribution

- [ ] Packaging instructions are current.
- [ ] Signing requirements are understood for the release artifact.
- [ ] The output artifact, version tag, and distribution notes are consistent.

Current gap as of 2026-03-18:

- No fresh release publish artifact or clean-machine launch proof was recorded in this pass.

## 7. Stop-Ship Check

- [ ] No unresolved P0 or P1 defects remain for the release slice.
- [ ] No unexplained startup, layout, or panel-regression instability remains in the release lane.
- [ ] No contributor is relying on a known-false validation path for sign-off.

Current gap as of 2026-03-19:

- The dedicated JARVIS FlaUI blocker now has current passing proof, but the focused MainForm regression lane still has 2 failures: `MainFormIntegrationTests.RibbonBudgetButton_Click_ShowsInitializedBudgetPanel` and `MainFormIntegrationTests.UnifiedNavigationDropdown_SelectingJarvis_ActivatesRightDockTab`.
- The Payments production-navigation blocker is cleared in the current rerun evidence, but `QuickBooks` still keeps production navigation blocked for sign-off.
- Some previously defined VS Code proof tasks required repair because PowerShell was mis-parsing filter and logger arguments; do not treat older failed task runs as release evidence.
- Data-layer, abstractions-layer, business-layer, models-layer, and focused service-layer proof are current and green as of 2026-03-21, so the remaining release risk is concentrated in shell and production-navigation lanes rather than these lower-level contracts.

## Release Sign-Off Rule

Do not ship because the list of open items feels smaller. Ship when the current release slice is frozen, proven, documented truthfully, and repeatably validated.
