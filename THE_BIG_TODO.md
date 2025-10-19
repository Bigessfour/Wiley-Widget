# The Big TODO List

A living backlog of technical debt, follow-ups, and “swing back” work. Keep items small and actionable. Move done items to the Changelog when complete.

## High-priority (near-term wins)

- Threading: replace Task.Run around UI with Dispatcher calls
  - Replace Task.Run(() => DispatcherHelper.Invoke(...)) with direct DispatcherHelper.Invoke/InvokeAsync.
  - Ensure all ObservableCollection mutations and selection changes occur on the UI thread.
  - Owner: Core UI team
  - Status: Done (core sweep completed across primary ViewModels; retained Task.Run only for CPU/disk-bound exports).

- Async I/O: stop wrapping async repo calls in Task.Run
  - Replace await Task.Run(() => repo.AsyncMethod()) with await repo.AsyncMethod().
  - Files updated: DashboardViewModel, BudgetViewModel, BudgetAnalysisViewModel, AIAssistViewModel, EnterpriseViewModel (commands refactor).
  - Validate repositories use true async EF Core calls.
  - Owner: Data layer

- Remove blocking waits on UI paths
  - Replace .Wait()/.Result with async/await end-to-end.
  - If unavoidable during non-UI startup, isolate on background thread and document rationale.
  - Files: DatabaseConfiguration.cs (fixed: async TryGetFromSecretVaultAsync + GetAwaiter().GetResult at registration), QuickBooksService.cs (pending under Settings/OAuth follow-ups)

- Add DEBUG-only UI thread guard
  - Introduce EnsureOnUIThread() used before UI mutations; assert/log when off-thread.
  - Implemented: DispatcherGuard + usage in MainViewModel AddTestEnterpriseAsync and applied in additional hotspots.
  - Scope: Extended to other ViewModels that touch UI/ObservableCollection.

Done: QuickBooks service made async and lazy-initialized; Settings tab flow validated. Added URL ACL readiness check and surfaced status in Settings > QuickBooks Integration.

## WPF/Prism UI polish

- Regions: verify initial activation states
  - Ensure SettingsPanelView is activated when expected; review RightPanelRegion auto-hide behavior.
  - Add navigation breadcrumbs or visual cues when a region is collapsed/hidden.

- Busy indicators standardization
  - Ensure IsBusy/BusyIndicator usage is consistent; prefer central BusyService.

- Validation UX pass
  - Consolidate validation styles; ensure consistent tooltip and inline messages across Settings tabs.

## Settings/OAuth follow-ups

- See: docs/progress/2025-10-19-oauth-followups.md

- OAuth listener binding fix automation
  - Detect and apply netsh http add urlacl when binding fails; log the exact command and outcome.
  - Add post-token verification and persistence checks.

- QuickBooks integration hardening
  - Timeouts, retry, and structured error messages (non-blocking UI).

## Observability & diagnostics

- Structured logging alignment
  - Ensure correlation IDs for long-running ops; enrich logs with Region/VM context.

- UI freeze diagnostics checklist
  - Document steps and VS diagnostics tools; add a scriptable “debug mode” switch.

## Performance & startup

- Defer heavy initialization until after shell shows
  - Convert startup blocking work to background async tasks with UI-ready checks.

- Repository batching
  - Reduce chatty calls; prefer batched/async streaming where possible.

## Future candidates (schedule when capacity allows)

- Multiple UI windows on separate dispatchers (only if we add independent top-level tools)
- Background indexing for large datasets (progress reporting to UI)
- Unified Dialog/Interaction service abstraction with async patterns

---

Process notes
- Keep items scoped; link to commits/PRs.
- Update status during weekly tech-debt review.
- Prefer smallest viable changes that move us forward.
