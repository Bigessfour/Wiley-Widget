# Budget View - Export (CSV / PDF / Excel)

This document explains the recent additions to the Budget view for exporting budget entries to CSV, PDF, and Excel formats.

## 1. What was added

- ViewModel
  - `BudgetViewModel` now contains: `ExportToPdfCommand` and `ExportToExcelCommand` (async string commands).
  - These methods call into DI-registered services:
    - `IPdfExportService` (SyncfusionPdfExportService) — generates a styled multi-page PDF using Syncfusion.Pdf.
    - `IExcelExportService` (ExcelExportService) — generates .xlsx files using ClosedXML for a robust, license-free solution.
- Form/UI
  - `BudgetViewForm` toolbar got two new buttons: `Export PDF` and `Export Excel`.
  - Both buttons show `SaveFileDialog` and call the corresponding ViewModel commands.
- Tests
  - Unit tests added for `BudgetViewModel` to validate invocation and basic behavior for CSV/PDF/Excel exports.

## 2. Implementation notes for developers

- Service implementations are in:
  - `WileyWidget.Services.Export.SyncfusionPdfExportService` (implements `IPdfExportService`)
  - `WileyWidget.Services.Export.ExcelExportService` (implements `IExcelExportService`)
- These services are registered in DI in `WileyWidget.WinForms.Configuration.DependencyInjection`.

## 3. Syncfusion licensing

- Syncfusion components require a valid license key to remove trial watermarks and enable certain functionality.
- `Program.RegisterSyncfusionLicense()` already checks for a `SYNCFUSION_LICENSE_KEY` environment variable and appsettings.json `Syncfusion:LicenseKey`.
- Recommended developer paths:
  - Environment variable: `SYNCFUSION_LICENSE_KEY` (CI or dev machine)
  - Local file: `licenses/license.key` (repo-root or app base) for local dev

## 4. Performance and large-export considerations

- Exports are performed on background threads (async) to keep UI responsive.
- For very large exports (10k+ rows):
  - Use streaming where possible, avoid loading every row into memory before writing.
  - Export services currently materialize the BudgetEntries list — consider exposing an IEnumerable/batched API if memory pressure observed.

## 5. Future enhancements / TODOs

- Embed chart graphics (from `ChartControl`) into PDF output for richer reports.
- Add print-preview support and additional pagination/custom headers for different page sizes.
- Add end-to-end UI tests (FlaUI) to cover the export workflows and SaveFileDialog interactions.

## 6. Usage (end-user)

1. Open Budget view.
2. Add, import, or load budgets for the desired year.
3. Click the toolbar `Export CSV` / `Export PDF` / `Export Excel` button.
4. Choose a destination filename in the Save dialog, then save.

If you hit a license issue for Syncfusion PDF generation, set the environment variable `SYNCFUSION_LICENSE_KEY` with a valid key or use the `Excel Export` (ClosedXML) flow which does not need a license.
