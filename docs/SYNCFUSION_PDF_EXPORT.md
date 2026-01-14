# Syncfusion PDF Exporter

Quick usage notes for the `SyncfusionPdfExportService` added to `src/WileyWidget.WinForms/Services`.

## Overview

- The service is a minimal helper that renders two DataTables (named `Revenues` and `Expenses`) from a `DataSet` into a printable PDF using `Syncfusion.Pdf` + `PdfGrid`.
- It formats currency and percent columns as strings so the resulting PDF displays `C2` / `P2` values exactly.

## Basic example

Place this code where you assemble the report data (for example in `ReportsViewModel` export command):

```csharp
using System.Data;
using WileyWidget.WinForms.Services;
using System.IO;

// build your dataset with two tables named "Revenues" and "Expenses"
var ds = new DataSet();
// ... populate ds.Tables["Revenues"] and ds.Tables["Expenses"]

var outPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "BudgetComparison.pdf");
SyncfusionPdfExportService.ExportBudgetComparisonPdf(ds, outPath, "Budget Comparison", 2026, "Wiley Sanitation District");

// Open the PDF or return path to consumer
```

## Notes

- This implementation is intentionally small and straightforward to make integration and visual tuning easy. You can expand table styles, fonts, conditional coloring, and layout in `SyncfusionPdfExportService`.
- The project already references `Syncfusion.Pdf.WinForms` (v32.1.19) in `Directory.Packages.props`.
- Syncfusion licensing: register the license on application startup if your environment requires it. See Syncfusion docs for `Syncfusion.Licensing.RegisterLicense`.

## Next steps you might want me to do

- Wire a direct Export command in `ReportsViewModel` to call the service and offer a SaveFile dialog.
- Improve table styling (borders, alternating row shading, column widths, numeric alignment).
- Add a small unit/integration test that generates a sample PDF into `tmp/`.
