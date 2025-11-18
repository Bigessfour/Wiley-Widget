# MunicipalAccountView — Test Plan

This test plan maps the Syncfusion WPF View Maturity Checklist to concrete, focused tests for `MunicipalAccountView` and its `MunicipalAccountViewModel` so the view can be validated end-to-end in CI and via Test Explorer.

Goals

- Verify `MunicipalAccountView` is MVVM-compliant, initializes cleanly, binds to the ViewModel, and displays expected state for typical scenarios (empty, populated, error).
- Verify `MunicipalAccountViewModel` loads accounts, handles QuickBooks sync absence, and exposes validation and analysis behavior.

Test categories and key cases

1. ViewModel unit tests (fast)
   - LoadAccountsAsync populates `MunicipalAccounts` when repository returns data.
   - LoadAccountsAsync sets `StatusMessage`, `IsBusy`, and clears `HasError` on success.
   - LoadAccountsAsync sets `HasError`/`ErrorMessage` when repository throws.
   - InitializeAsync calls `LoadAccountsAsync` and `LoadBudgetAnalysisAsync` (use mocks to assert calls).
   - AnalyzeSelectedAccountAsync returns early when `SelectedAccount` is null and sets `AccountAnalysisResult` otherwise (mock `IGrokSupercomputer`).
   - SyncFromQuickBooksAsync returns graceful error when `_quickBooksService` is null (assert `HasError` and `ErrorMessage`).

2. View integration tests (lightweight host)
   - MunicipalAccountView loads without exceptions with `prism:ViewModelLocator.AutoWireViewModel="True"` disabled in test and with a test ViewModel assigned.
   - When ViewModel has `MunicipalAccounts` populated, the `SfDataGrid` renders rows (assert row count on the control or that `ItemsSource` is set).
   - Automation properties exist on the grid: `AutomationProperties.Name` and `HelpText` non-empty.

3. UI smoke tests (optional heavier tests)
   - Start the app shell and navigate to the Municipal region, assert view visible and interactive buttons wired to commands work (requires UI automation driver).

Test data & mocks

- Provide small in-memory lists of `MunicipalAccount` objects for unit tests.
- Mock `IMunicipalAccountRepository` for success/failure scenarios.
- Mock `IGrokSupercomputer` to return a deterministic analysis string for `AnalyzeSelectedAccountAsync` tests.

How to run

- Add these tests to a test project (xUnit) in the `tests` folder, reference the main project, and run via Test Explorer or `dotnet test`.

Acceptance criteria

- All ViewModel unit tests pass in CI.
- Integration tests run in a separate job that is allowed to reference Syncfusion assemblies; they must pass in pre-merge checks for UI-affecting PRs.

Files added (templates)

- `tests/Municipal/MunicipalAccountViewModelTests.cs` (unit test template)
- `tests/Municipal/MunicipalAccountViewIntegrationTests.cs` (lightweight integration template)

## Filtering Design

Uses ICollectionView for client-side filtering on TypeDescription. Supports 'Asset' and 'Cash'. Test coverage: 80%+. Reference: Syncfusion WPF DataGrid Filtering - https://help.syncfusion.com/wpf/datagrid/filtering.
