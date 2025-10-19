# The Big TODO List

A living backlog of technical debt, follow-ups, and “swing back” work. Keep items small and actionable. Move done items to the Changelog when complete.

## High

- QuickBooks OAuth: Serilog package mismatch crash
  - Symptom: MissingMethodException in Intuit.Ipp.OAuth2PlatformClient when constructing OAuthAdvancedLogging (Serilog.FileLoggerConfigurationExtensions.File overload with FileLifecycleHooks not found).
  - Evidence: logs/wiley-widget-20251019*.log at 09:41:33–09:42:16 while opening Settings > QuickBooks; stack points to AcquireTokensInteractiveAsync()/RefreshTokenIfNeededAsync() in `src/Services/QuickBooksService.cs`.
  - Likely cause: Serilog.Sinks.File version mismatch vs. the version Intuit SDK was compiled against.
  - Proposed fixes (choose one):
    - Align package versions: pin Serilog.Sinks.File to a compatible version (e.g., 4.x) or upgrade Intuit.Ipp.OAuth2PlatformClient to a version compatible with our Serilog stack; ensure only one Serilog.Sinks.File loads at runtime.
    - Temporary mitigation: disable Intuit “advanced logging” to Serilog file sink via client options/flags so OAuth no longer touches the incompatible API.
  - Acceptance criteria:
    - Navigating to Settings > QuickBooks no longer throws MissingMethodException.
    - TestConnectionAsync completes without crash (handles auth flow or reports a controlled error).
    - Normal application logging remains intact (no Serilog binding errors in logs).
  - Owner: Settings/OAuth
  - Status 2025-10-19:
    - Repro persists. Fresh run still logs MissingMethodException in Intuit.Ipp.OAuth2PlatformClient.Diagnostics when calling GetAuthorizationURL.
    - Output DLL confirms Serilog.Sinks.File.dll ProductVersion: 7.0.0 (present in bin). Likely runtime binding conflict or API mismatch inside Intuit Diagnostics assembly.
  - Next actions:
    - Force-align Serilog stack used by Intuit packages: explicitly pin Serilog, Serilog.Enrichers.Thread/Environment, Serilog.Settings.Configuration, Serilog.Sinks.Console/Debug to versions compatible with IppOAuth2PlatformSdk 14.0.0 and IppDotNetSdkForQuickBooksApiV3 14.7.0.1.
    - If mismatch remains, disable Intuit advanced logging to file sink for OAuth by configuring the OAuth2 client/logging helper (avoid invoking the File(...) sink overload with FileLifecycleHooks).
    - Validate by launching Settings > QuickBooks and running Test Connection; confirm no new MissingMethodException in logs.

- Remove blocking waits on UI paths
  - Replace .Wait()/.Result with async/await end-to-end.
  - If unavoidable during non-UI startup, isolate on background thread and document rationale.
  - Files: DatabaseConfiguration.cs (fixed: async TryGetFromSecretVaultAsync + GetAwaiter().GetResult at registration), QuickBooksService.cs (pending under Settings/OAuth follow-ups)

Done: QuickBooks service made async and lazy-initialized; Settings tab flow validated. Added URL ACL readiness check and surfaced status in Settings > QuickBooks Integration.

## Medium

- Async I/O: stop wrapping async repo calls in Task.Run
  - Replace await Task.Run(() => repo.AsyncMethod()) with await repo.AsyncMethod().
  - Files updated: DashboardViewModel, BudgetViewModel, BudgetAnalysisViewModel, AIAssistViewModel, EnterpriseViewModel (commands refactor).
  - Validate repositories use true async EF Core calls.
  - Owner: Data layer

- OAuth listener binding fix automation
  - Detect and apply netsh http add urlacl when binding fails; log the exact command and outcome.
  - Add post-token verification and persistence checks.
  - See also: docs/progress/2025-10-19-oauth-followups.md

- QuickBooks integration hardening
  - Timeouts, retry, and structured error messages (non-blocking UI).

- Observability & diagnostics
  - Structured logging alignment: ensure correlation IDs for long-running ops; enrich logs with Region/VM context.
  - UI freeze diagnostics checklist: document steps and VS diagnostics tools; add a scriptable “debug mode” switch.

- Performance & startup
  - Defer heavy initialization until after shell shows: convert startup blocking work to background async tasks with UI-ready checks.
  - Repository batching: reduce chatty calls; prefer batched/async streaming where possible.

## Low

- WPF/Prism UI polish
  - Regions: verify initial activation states; ensure SettingsPanelView activation and review RightPanelRegion auto-hide behavior; add breadcrumbs when regions are collapsed.
  - Busy indicators standardization: ensure IsBusy/BusyIndicator usage is consistent; prefer central BusyService.
  - Validation UX pass: consolidate validation styles; ensure consistent tooltip and inline messages across Settings tabs.

- Add DEBUG-only UI thread guard
  - Introduce EnsureOnUIThread() used before UI mutations; assert/log when off-thread.
  - Implemented: DispatcherGuard + usage in MainViewModel AddTestEnterpriseAsync and applied in additional hotspots.
  - Scope: Extend to other ViewModels that touch UI/ObservableCollection as needed.

- Future candidates (schedule when capacity allows)
  - Multiple UI windows on separate dispatchers (only if we add independent top-level tools)
  - Background indexing for large datasets (progress reporting to UI)
  - Unified Dialog/Interaction service abstraction with async patterns

---

Process notes
- Keep items scoped; link to commits/PRs.
- Update status during weekly tech-debt review.
- Prefer smallest viable changes that move us forward.
