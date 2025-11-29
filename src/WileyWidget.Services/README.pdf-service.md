# WileyWidget.Services - PDF service extensions

This file documents the newly-added PDF capabilities backed by Syncfusion.

New IPdfService methods added

- ExtractTextAsync(byte[] pdfBytes, CancellationToken) -> returns text extracted from the PDF
- FillFormAsync(byte[] pdfBytes, IDictionary<string,string> values, bool flatten, CancellationToken) -> fills common AcroForm fields (text, checkbox, radio, combo) by name; optionally flattens
- FlattenPdfFormsAsync(byte[] pdfBytes, CancellationToken) -> sets Flatten=true on all fields to render static content

Implementation notes

- Methods are implemented in `SyncfusionPdfService` using `PdfLoadedDocument` and `Syncfusion.Pdf.Interactive` types.
- Form filling attempts common field types; unsupported types are left untouched. Flattening sets per-field `Flatten` flag which makes the field appearance part of the page content.
- All new methods are best-effort and tolerant of missing fields; long-running operations honor `CancellationToken` where practical.

Examples

Extract text:

var text = await pdfService.ExtractTextAsync(pdfBytes);

Fill and flatten a text field:

var filled = await pdfService.FillFormAsync(pdfBytes, new Dictionary<string,string>{{"FirstName","Alice"}}, flatten: true);

Flatten an existing document:

var flattened = await pdfService.FlattenPdfFormsAsync(pdfBytes);
