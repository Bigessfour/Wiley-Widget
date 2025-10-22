# Testing checklist — Test Explorer configuration and Syncfusion converter validation

This checklist helps ensure the workspace is configured so that tests are visible and runnable from the Test Explorer activity bar, and that Syncfusion WPF converters used in views are validated against the Syncfusion API reference.

1. Enable Test Explorer in VS Code
   - Install the recommended extensions from `.vscode/extensions.json` (Test Explorer, .NET Test Explorer, Python).
   - Confirm the Test Explorer appears in the Activity Bar (left side) after installation.

2. Workspace settings to surface tests
   - Confirm `.vscode/settings.json` contains the following relevant entries:
     - `dotnet-test-explorer.testProjectPath` pointing to the workspace or `**/*Test*.csproj` pattern.
     - `dotnet-test-explorer.testFramework` set to `xunit` (or the framework your tests use).
     - `python.testing.pytestEnabled` set to `true` and `python.testing.pytestArgs` pointing at the test folder (e.g. `scripts` or test dir).
     - `testing.autoRun.mode` set to `rerun` (optional but useful for TDD iteration).

3. Running and verifying tests
   - Open the Test Explorer view, click the refresh/run button to discover tests.
   - Run a small set of tests (unit tests) to confirm discovery and reporting work.
   - Verify code lens (run/debug actions above tests) are present in test files if enabled.

4. Failures and triage
   - If tests are not discovered, check:
     - The `dotnet-test-explorer.testProjectPath` pattern matches your test projects.
     - The Python interpreter (`python.defaultInterpreterPath`) points to the virtualenv with test deps installed.
     - That tests run from the CLI (`dotnet test` or `pytest`) — fix CLI issues first.

5. Syncfusion WPF API breadcrumb for converter validation (required)
   - When validating value converters, bindings, or Syncfusion-specific XAML behaviors, use the Syncfusion WPF API reference as the authoritative source.
   - URL (breadcrumb):
     - https://help.syncfusion.com/cr/wpf/Syncfusion.html
   - Validation requirement (checklist item):
     - For each converter referenced in XAML or code-behind (for example, `IValueConverter` implementations used in `*.xaml` files like `MunicipalAccountView.xaml`), confirm expected behavior using the Syncfusion API docs for the specific control.
     - Steps:
       1. Find the control in XAML that uses the converter (search for the converter name or binding in the `.xaml` file).
       2. Open the Syncfusion API reference and locate the control's page (use the breadcrumb URL above and search within it for the control name).
       3. Check the control's property types and event signatures to ensure the converter returns compatible values (types and nullability) for the target property.
       4. If the converter interacts with Syncfusion-specific types (for example `SfDataGrid` cell templates, column binding converters, or formatting helpers), validate parameter contracts (e.g., `CellValue`, `ColumnType`) against the API docs.
       5. Document the validation result inline in the checklist (control name, converter name, API page URL, and pass/fail with notes).

6. Example validation entry (for checklist documentation)
   - Control: `SfDataGrid`
   - XAML: `Views/MunicipalAccountView.xaml` (search for converter usage)
   - Converter: `CurrencyToColorConverter`
   - API reference page: `https://help.syncfusion.com/cr/wpf/Syncfusion.UI.Xaml.Grid.SfDataGrid.html` (example — navigate from the main Syncfusion WPF page)
   - Result: PASS — converter returns SolidColorBrush for `Foreground` and handles nulls.

7. Automation suggestion (optional)
   - Add unit tests for converter logic (pure functions) so converter validation is repeatable:
     - Create small unit tests asserting conversion inputs -> outputs for the converter.
     - For Syncfusion-specific interactions, prefer integration tests that instantiate the control in a headless UI test (if available) and assert property application.

8. Notes
   - The checklist is intended to be used by developers and QA engineers to verify tests are discoverable and converters are safe to use with Syncfusion controls.
   - Keep a short log of validations in this file for auditability.

Happy testing!
