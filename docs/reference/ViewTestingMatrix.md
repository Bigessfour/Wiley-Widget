# View testing matrix — map maturity checklist items to test types

This document maps items from `SYNCFUSION_WPF_VIEW_MATURITY_CHECKLIST.md` to concrete test types you should add so view maturity is verifiable from CI and the Test Explorer.

Test types used:

- Unit test: fast, isolated, no UI runtime (xUnit for C# / pytest for Python).
- Integration test: host control or small window in memory, may rely on Syncfusion assemblies.
- UI automation test: end-to-end UI interaction (e.g., Appium/WinAppDriver or a headless XAML test harness).

Matrix (high level)

1. Namespaces
   - Test: Unit
   - What to test: XAML compile/syntax and presence of required xmlns declarations via an XAML parse test or static analyzer.

2. DataContext & MVVM Binding
   - Test: Integration + Unit
   - What to test: ViewModel auto-wire resolved by Prism container (integration) and that bindings map to ViewModel properties (unit via reflection on compiled XAML or simple binding tests in code-behind harness).

3. Syncfusion Controls
   - Test: Integration
   - What to test: For critical controls (e.g., `SfDataGrid`) include integration tests that instantiate the control and assert key properties and that binding templates render without exceptions.

4. Bindings
   - Test: Unit + Integration
   - What to test: Unit tests for value converters; integration tests for binding updates between ViewModel and view elements.

5. Commands
   - Test: Unit
   - What to test: `DelegateCommand` behavior and `CanExecute` transitions (unit testable). Integration tests can assert UI button triggers the command.

6. Themes & Styling
   - Test: Integration
   - What to test: Load theme resources in a small host and verify no resource missing exceptions; assert resolved brushes and styles keys exist.

7. Templates & Resources
   - Test: Integration
   - What to test: Ensure DataTemplate resolves (instantiate template and call .LoadContent()).

8. Performance Optimizations
   - Test: Integration + Performance microbenchmark
   - What to test: Virtualization flags present; simple perf test to render large items with VirtualizingStackPanel enabled.

9. Accessibility & Localization
   - Test: Unit + Integration
   - What to test: Automation properties set; resource files present for locales used in tests.

10. Validation & Error Handling
    - Test: Unit + Integration
    - What to test: `IDataErrorInfo`/`INotifyDataErrorInfo` signals and UI shows validation visuals in integration harness.

How to use

- Add unit tests for converters and ViewModel logic to `tests/` (C# projects for xUnit). Use the templates under `tests/templates` as a starting point.
- Add integration tests that instantiate XAML controls in a test host (WPF test host runner) and assert no exceptions and expected property values.
- Run these tests from the Test Explorer; CI should run `dotnet test` and (optionally) a UI automation job for the heavier UI tests.

The next section provides minimal example templates for C# xUnit converter and ViewModel tests and a small pytest example for any Python-side tooling.
