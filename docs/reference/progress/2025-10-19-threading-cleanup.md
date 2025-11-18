- [x] Implement quick wins:
  - Remove Task.Run wrappers around UI/async repo calls in MainViewModel, DashboardViewModel, BudgetViewModel, BudgetAnalysisViewModel, AIAssistViewModel.
  - Replace blocking Wait/Result in DatabaseConfiguration secret retrieval with async method (and safe GetAwaiter().GetResult at registration).
  - Add DEBUG-only DispatcherGuard and applied in MainViewModel for ObservableCollection mutation.

- [x] Sweep remaining viewmodels/services for:
  - .Wait()/.Result on UI paths; convert to async/await.
  - Task.Run wrapping of async repository calls.
  - UI-thread mutations without DispatcherHelper.

- [x] Add EnsureOnUIThread usage in other hotspots (DashboardViewModel, EnterpriseViewModel, SettingsViewModel) where ObservableCollections are modified.

- [x] OAuth follow-ups moved to separate doc: 2025-10-19-oauth-followups.md

Notes (2025-10-19):

- Fixed 23 build errors primarily in `EnterpriseViewModel` due to duplicate command properties and constructors created by WPF temp project caching. Consolidated to a single command set and one constructor, added missing state (`EnterpriseList`, `SelectedEnterprise`, `StatusMessage`, `ErrorMessage`).
- Implemented IDisposable correctly for `_loadSemaphore` and removed unnecessary `Prism.Regions` usage to resolve namespace errors.
- Ensured UI-thread-safe collection updates using `DispatcherHelper`; retained Task.Run only for CPU/disk-bound export operations per earlier decision.

- [x] Reviewed `EnterpriseView.xaml.cs` — Task.Run retained for CPU/disk-bound CSV export; UI notifications marshalled via DispatcherHelper.
