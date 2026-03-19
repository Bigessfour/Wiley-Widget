# QuickBooks Desktop Import Specification

Version: 0.1
Status: Approved for implementation
Last updated: 2026-03-18
Owner: Wiley Widget

## Purpose

Define the local-file import path required when a municipality keeps QuickBooks data on the clerk workstation instead of syncing through QuickBooks Online APIs. This specification is the implementation source of truth for desktop import work and must be satisfied before merge.

## Scope

- In scope: local file selection, local parsing, preview, mapping, validation, routing into Wiley Widget data/view-model targets, audit trail, and UI progress.
- In scope: QuickBooks Desktop exports in CSV, IIF, and Excel formats.
- Out of scope: QuickBooks COM automation, QuickBooks SDK session hosting, direct reads of `.QBW` company files, network shares, and cloud OAuth replacement.

## References

Official Intuit references only:

1. CSV export/import: https://quickbooks.intuit.com/learn-support/en-us/help-article/manage-lists/import-export-csv-files/L9AiGRdT9_US_en_US
2. IIF export/import: https://quickbooks.intuit.com/learn-support/en-us/help-article/import-export-data-files/export-import-edit-iif-files/L56LT9Z0Q_US_en_US
3. IIF overview, headers, sample kit: https://quickbooks.intuit.com/learn-support/en-us/help-article/list-management/iif-overview-import-kit-sample-files-headers/L5CZIpJne_US_en_US
4. Excel export/import: https://quickbooks.intuit.com/learn-support/en-us/help-article/list-management/import-export-ms-excel-files/L9BDPsTTX_US_en_US

Project references:

1. `src/WileyWidget.WinForms/Controls/Panels/QuickBooksPanel.cs`
2. `src/WileyWidget.WinForms/ViewModels/QuickBooksViewModel.cs`
3. `src/WileyWidget.Services/QuickBooksService.cs`
4. `src/WileyWidget.WinForms/Controls/Supporting/CsvMappingWizardPanel.cs`
5. `src/WileyWidget.Services/CsvExcelImportService.cs`
6. `src/WileyWidget.WinForms/Services/DataImportService.cs`
7. `src/WileyWidget.WinForms/Services/StatusProgressService.cs`
8. `docs/WileyWidgetUIStandards.md`

## Problem Statement

Wiley Widget currently includes a QuickBooks Online integration path. That path depends on OAuth and Intuit cloud APIs and does not satisfy municipalities whose QuickBooks Desktop data stays on a local workstation. The application therefore needs a local, file-based import flow that lets the clerk export supported data from QuickBooks Desktop and bring that data into Wiley Widget without network dependency.

## Supported Data Types

| Data type               | Primary export source in QuickBooks Desktop      | Preferred file format                               | Wiley Widget target                                        |
| ----------------------- | ------------------------------------------------ | --------------------------------------------------- | ---------------------------------------------------------- |
| Chart of Accounts       | Lists -> Chart of Accounts / Item export tooling | CSV or Excel; IIF supported                         | Accounts workflows and account-oriented view models        |
| Customers               | Customer Center export                           | CSV or Excel; IIF supported                         | Customer-oriented workflows                                |
| Vendors                 | Vendor Center export                             | CSV or Excel; IIF supported                         | Vendor/payables-oriented workflows                         |
| Payments / Transactions | Reports exported from QuickBooks Desktop         | CSV primary; IIF where available                    | Payments workflows and transaction import targets          |
| Items                   | Item List export                                 | CSV or Excel; IIF for cases Intuit reserves for IIF | Deferred until Wiley Widget adds a first-class item target |

Constraints derived from Intuit documentation:

- CSV is appropriate for lists and report exports.
- Excel is a clerk-friendly fallback for lists and report exports.
- IIF is a tab-delimited advanced format with limited validation; imported data must be checked carefully after parse and before commit.

## Supported Formats

| Format          | Status    | Notes                                                                             |
| --------------- | --------- | --------------------------------------------------------------------------------- |
| `.csv`          | Primary   | Lowest-friction path for list and report exports from QuickBooks Desktop          |
| `.iif`          | Supported | Tab-separated ASCII text; parser must respect `!` header rows and section markers |
| `.xlsx`, `.xls` | Fallback  | Reuse existing Excel reader/import surfaces                                       |

## User Flow Diagram

```text
QuickBooks Desktop clerk export
    -> Save local file (.csv, .iif, .xlsx, .xls)
    -> Open Wiley Widget QuickBooks panel
    -> Choose "Import QuickBooks Desktop File"
    -> Validate file path, extension, and size
    -> Parse source file into normalized table
  -> Auto-detect supported export profile
    -> Route rows into existing Wiley Widget targets
    -> Audit log entry + progress completion + visible summary
```

Current implementation note:

- Wiley Widget now auto-detects and imports chart of accounts, customers, vendors, and payment/check exports.
- Item exports remain explicitly unsupported until the application gains an item persistence surface.

## Architecture Decision Record

Decision: extend the existing QuickBooks/import pipeline rather than build a new subsystem.

Why:

- `QuickBooksPanel` already provides the user-facing QuickBooks surface.
- `QuickBooksViewModel` already owns QuickBooks actions, progress text, and sync history.
- `CsvMappingWizardPanel` already provides preview and mapping mechanics suitable for desktop exports.
- `CsvExcelImportService` already handles CSV/Excel ingestion and is the correct seam to normalize file inputs.
- `StatusProgressService` already propagates progress into the shell.

Implementation consequence:

- Add a desktop-import contract to `IQuickBooksService` / `QuickBooksService`.
- Add a small IIF parser that produces a normalized `DataTable` so CSV, IIF, and Excel can share the same downstream mapping flow.
- Keep the existing QuickBooks Online path intact; desktop import is additive, not a replacement.
- Do not introduce COM/SDK dependencies or new docking/navigation systems.

## UI Specification

### Existing shell surface to extend

- Extend `QuickBooksPanel` instead of creating a disconnected panel type.
- Keep `RibbonControlAdv` and the existing QuickBooks panel as the user entry point.
- If a ribbon shortcut is added later, it must navigate to the same QuickBooks panel action and not create a parallel workflow.

### Syncfusion 32.2.3 controls to use

| Concern             | Control / service                                 |
| ------------------- | ------------------------------------------------- |
| Action button       | `SfButton` created via `SyncfusionControlFactory` |
| Progress            | `ProgressBarAdv` and `IStatusProgressService`     |
| Preview grid        | `SfDataGrid`                                      |
| Theme               | `SfSkinManager` / project theme helpers only      |
| Docking integration | Existing `DockingManager`/panel navigation path   |

UI rules:

- No manual `BackColor` or `ForeColor` assignments except semantic status colors allowed by repo policy.
- Create Syncfusion controls through `SyncfusionControlFactory` where the factory already has a pattern.
- Any new file-pick action may use the standard WinForms `OpenFileDialog` if wrapped in the existing panel flow.
- Progress must remain responsive and non-blocking.

## Service Specification

### New contract surface

The QuickBooks service must expose a local desktop-import method that:

1. Accepts a clerk-selected local path.
2. Validates extension and file existence.
3. Parses CSV, IIF, or Excel into a normalized table.
4. Applies a QuickBooks Desktop preset mapping or launches the existing mapping wizard when needed.
5. Routes the normalized data into existing app import targets.
6. Emits audit and progress updates.

### Parsing rules

- CSV: use the existing CSV ingestion path.
- Excel: use the existing Excel ingestion path.
- IIF: parse as tab-delimited text.
- Ignore header rows whose first token begins with `!`.
- Preserve record type markers such as `TRNS`, `SPL`, `ENDTRNS`, `ACCNT`, `CUST`, and equivalent list row tokens in a dedicated column when present.
- Fail fast on empty files, unsupported extensions, unreadable paths, or files with no usable data rows.

### Routing rules

- Reuse existing target import seams wherever possible.
- Desktop-import routing must not bypass validation already present in the downstream view models or repositories.
- Desktop-import operations must write an audit entry that identifies:
  - source = QuickBooks Desktop
  - local file path
  - file type
  - rows processed
  - result status

## Security and Validation Rules

- Local file only. No network fetch, no upload, no remote share requirement.
- Validate file existence and extension before opening the file.
- Reject unsupported formats.
- Cap accepted file size according to the same safety envelope used by existing import services where practical.
- Do not log file contents.
- Logging may include file name, extension, row count, and outcome.
- Optional hash capture is allowed for audit consistency, but import must not depend on hashing.
- For IIF, surface explicit warnings that Intuit describes IIF as having limited validation.

## Acceptance Criteria

The feature is merge-ready only when all criteria below are met:

1. Wiley Widget can import a local QuickBooks Desktop export file in CSV format from the existing QuickBooks surface.
2. Wiley Widget can parse a local `.iif` file with Intuit-style `!` header rows into a normalized table.
3. Wiley Widget can import Excel exports through the same desktop-import command path.
4. The QuickBooks Online integration path still builds and its existing commands still execute.
5. UI changes follow Syncfusion factory/theming rules and do not introduce manual color styling.
6. Progress and completion states surface through the existing progress/status pattern.
7. A focused automated test proves IIF parsing behavior.
8. A focused automated test proves desktop-import orchestration for at least one supported file type.
9. DI validation passes with the new service graph.
10. The implementation does not require QuickBooks Desktop COM automation, SDK setup, or a cloud login.

## Proof Plan

- Unit tests for the IIF parser:
  - skips `!` header lines
  - preserves non-header row data
  - returns the expected columns and row count
- Unit tests for desktop-import orchestration:
  - rejects unsupported extensions
  - routes CSV/IIF/Excel through the correct parser path
  - reports a successful import summary when routing succeeds
- Validation tasks:
  - focused build for the affected projects
  - focused unit-test run for the new parser/service tests
  - DI validation path already used by the WinForms app

## Non-Goals

- Direct QuickBooks company file reads
- Writing data back into QuickBooks Desktop
- Automatic scheduling or background watching of exported files
- A separate wizard framework outside the current panel/mapping infrastructure

## Rollout Notes

- Desktop import is additive. Municipal customers using QuickBooks Online keep the existing OAuth path.
- Clerk training should point users to the companion guide in `docs/QuickBooksDesktopImport-UserGuide.md`.
