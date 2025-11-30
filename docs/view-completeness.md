# View Completeness Validation Checklist (Wiley-Widget)

This is the mandatory View Completeness Validation Checklist for all view work in the Wiley-Widget project.
Every view (Form / Dialog / Panel) must be validated against this checklist before it is considered complete.

Location: docs/view-completeness.md

How to use
- Copy the checklist below into a view-specific file under `docs/views/<view-name>.md` and fill in the Evidence/Status fields.
- For each view, mark each item as: Passed / Not Applicable (N/A) / Failed
- The view completeness percentage is: (Passed items / Applicable items) * 100
- All items must be Passed (100%) for a view to be considered complete. Where N/A is used, update the 'Applicable items' count accordingly.

---

## Master Checklist (Apply to each view)

For each view create a view-specific document and answer each item with Evidence (code lines, tests, screenshots) and pass/fail/N/A.

1. UI Layout and Controls
   - [ ] Controls present and configured
   - [ ] Docking/anchoring/responsive layout
   - [ ] Toolbars / menus / status bars
   - [ ] Syncfusion controls (if used) properly initialized
   - [ ] Chart controls (if used) configured
   - [ ] No hardcoded UI literals (use resources)

2. Data Binding
   - [ ] ViewModel bindings in place (BindingSource or manual)
   - [ ] Grid / data controls data-source set to ViewModel
   - [ ] Two-way binding for editable fields
   - [ ] UI updates on INotifyPropertyChanged
   - [ ] Async EF Core queries & paging if needed
   - [ ] No direct DB or business logic in View

3. Command and Event Handling
   - [ ] Commands wired to ViewModel (RelayCommand / AsyncRelayCommand)
   - [ ] Minimal code-behind (delegate to VM)
   - [ ] Async commands show loading state / disable UI as needed
   - [ ] Errors surfaced via ViewModel events/messages
   - [ ] Inter-view messaging used when appropriate

4. Styling and Theming
   - [ ] Uses ThemeManager / Syncfusion theme where applicable
   - [ ] Custom colors/fonts use theme tokens or ThemeManager
   - [ ] DPI / scaling considerations are addressed
   - [ ] Syncfusion grid/chart visual customizations complete
   - [ ] High-contrast / accessibility styling options covered

5. Accessibility
   - [ ] AccessibleName / AccessibleDescription set for controls
   - [ ] Tab order, keyboard nav, accelerators checked
   - [ ] Screen reader compatibility validated
   - [ ] Contrast checks (WCAG 2.1 AA for text)
   - [ ] Third-party control accessibility flags enabled

6. Performance Optimization
   - [ ] Data load is async / background where required
   - [ ] Virtualization enabled for large lists/grids
   - [ ] Proper disposal of heavy resources (images, handles)
   - [ ] SuspendLayout/ResumeLayout used when populating UIs
   - [ ] Resilient HTTP/IO (Polly / caches) where applicable

7. Error Handling and Validation
   - [ ] Validation rules in ViewModel (FluentValidation or VM logic)
   - [ ] Try/catch and logging on critical operations
   - [ ] Friendly messages to users (MessageBox / StatusStrip)
   - [ ] EF Core exceptions handled and surfaced appropriately
   - [ ] Fallbacks or offline mode (if applicable)

8. Testing
   - [ ] Unit tests for ViewModel behaviors (Commands, state changes)
   - [ ] Integration tests for data flow (EF Core, services)
   - [ ] UI / E2E tests where appropriate (WinForms UI automation)
   - [ ] Test coverage info recorded (target >=80% for VM)
   - [ ] Tests run in CI and pass reproducibly

9. Documentation and Maintainability
   - [ ] XML/Public member docs present
   - [ ] Inline comments for complex logic
   - [ ] README or view-specific docs (notes, open issues)
   - [ ] No unexplained suppressions/warnings
   - [ ] Code style & lint checks pass

10. Overall Integration & Completeness
   - [ ] DI injections used correctly (ILogger, ViewModel, services)
   - [ ] Aligns with project MVVM architecture (no business code in views)
   - [ ] No secrets in code; uses configuration services
   - [ ] Builds and runs cleanly in Debug/Release modes
   - [ ] Meets feature acceptance criteria

---

## Per-View Tracking (how-to)

Each view file under `docs/views/` should include these fields at the top:

- View: <ViewName>
- Path: <source file path>
- Responsible: <engineer name>
- Last updated: <date>
- Applicable Items: <n> (number of items that apply)
- Passed: <n>
- N/A: <n>
- Failed: <n>
- Completeness %: <computed percent>

Then include the AC 10-section checklist with checkboxes and an "Evidence" subsection for each item.

---

## Master view inventory (initial sweep — update as project evolves)

This repository (WinForms app) initially includes the following view targets that must each get an entry under `docs/views/`:



Optional / future view targets & dialog inventory (add new docs/views entries when new forms are created):
- WidgetEditor
- Dashboard (high-level data panels)
- Export views / ExportDialog (CSV/Excel/PDF export workflows)
- Import views / ImportDialog (CSV/Excel import flows)
- HelpAboutDialog
- LicenseDialog
- ErrorReportingDialog

---

## Complete listing — required views & initial CRUD dialogs (living inventory)

The table below is the canonical inventory for view-level completeness tracking. Each row should be kept up-to-date in the `docs/views/<view>.md` file for that view.

| View Name | Source Path | Status | Completeness % | Notes |
|---|---|---:|---:|---|
| MainForm | WileyWidget.WinForms/Forms/MainForm.cs | Not Started / In Progress / Complete | 0% | Main app shell
| AccountsForm | WileyWidget.WinForms/Forms/AccountsForm.cs | Not Started / In Progress / Complete | 0% | Accounts listing, grid
| AccountCreateForm | WileyWidget.WinForms/Forms/AccountEditForm.cs | Not Started / In Progress / Complete | 0% | Create dialog (same file: AccountEditForm — track create vs edit)
| AccountEditForm | WileyWidget.WinForms/Forms/AccountEditForm.cs | Not Started / In Progress / Complete | 0% | Edit dialog
| AccountDeleteConfirm | WileyWidget.WinForms/Forms/AccountEditForm.cs | Not Started / In Progress / Complete | 0% | Delete confirmation (dialog or UI pattern)
| ChartForm | WileyWidget.WinForms/Forms/ChartForm.cs | Not Started / In Progress / Complete | 0% | Charts & analytics
| Dashboard | WileyWidget.WinForms/Forms/DashboardForm.cs (future) | Not Started | 0% | High-level dashboard panels
| WidgetEditor | WileyWidget.WinForms/Forms/WidgetEditorForm.cs (future) | Not Started | 0% | Widget-specific CRUD
| SettingsForm | WileyWidget.WinForms/Forms/SettingsForm.cs | Not Started / In Progress / Complete | 0% | App settings & theme toggle
| ExportDialog | WileyWidget.WinForms/Forms/ExportForm.cs (future) | Not Started | 0% | Export workflows
| ImportDialog | WileyWidget.WinForms/Forms/ImportForm.cs (future) | Not Started | 0% | Import workflows
| HelpAboutDialog | WileyWidget.WinForms/Forms/HelpAboutForm.cs (future) | Not Started | 0% | Help & about
| LicenseDialog | WileyWidget.WinForms/Forms/LicenseForm.cs (future) | Not Started | 0% | Licensing UI
| ErrorReportingDialog | WileyWidget.WinForms/Forms/ErrorReportingForm.cs (future) | Not Started | 0% | Error reporting fallback UI

---

If you add a new view to the codebase, create the matching `docs/views/<view-name>.md` and add a row to the table above.
- WidgetEditor, Dashboard, Export views, Import views, HelpAboutDialog, LicenseDialog, ErrorReportingDialog, etc.

The `docs/views/` directory is the single source of truth for view completeness records and evidence. PRs that modify views must also update the corresponding `docs/views/<view>.md` file and pass the checklist before merging.

---

## Summary

This checklist enforces consistency across UI views and helps reviewers and CI verify that each view is production-ready with theming, accessibility, testing, and operational concerns addressed.

For maintenance: keep this document concise and canonical. When the checklist evolves, update `docs/copilot-instructions.md` to reflect the new mandatory validation steps.
