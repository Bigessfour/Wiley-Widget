extern alias pdfnet;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using pdfnet::Syncfusion.Pdf; // documented namespace per PDF overview
using pdfnet::Syncfusion.Pdf.Graphics;
using Syncfusion.XlsIO; // documented namespace per Excel (XlsIO) overview
using System.Drawing; // for PointF & fonts (legacy GDI+ permitted Windows)

namespace WileyWidget.Services;

internal sealed class SyncfusionExportPipeline : IExportPipeline
{
    public Task<Stream> CreatePdfAsync(Action<IPdfBuilder> buildAction, CancellationToken cancellationToken = default)
    {
        if (buildAction == null) throw new ArgumentNullException(nameof(buildAction));
        var memory = new MemoryStream();
        using (var document = new PdfDocument())
        {
            var page = document.Pages.Add();
            var builder = new PdfBuilder(page);
            cancellationToken.ThrowIfCancellationRequested();
            buildAction(builder);
            cancellationToken.ThrowIfCancellationRequested();
            document.Save(memory);
        }
        memory.Position = 0;
        return Task.FromResult<Stream>(memory);
    }

    public Task<Stream> CreateExcelAsync(Action<IExcelBuilder> buildAction, CancellationToken cancellationToken = default)
    {
        if (buildAction == null) throw new ArgumentNullException(nameof(buildAction));
        var stream = new MemoryStream();
        using (var engine = new ExcelEngine())
        {
            var app = engine.Excel;
            app.DefaultVersion = ExcelVersion.Xlsx; // documented pattern
            var workbook = app.Workbooks.Create(1);
            var sheet = workbook.Worksheets[0];
            var builder = new ExcelBuilder(sheet);
            cancellationToken.ThrowIfCancellationRequested();
            buildAction(builder);
            cancellationToken.ThrowIfCancellationRequested();
            workbook.SaveAs(stream);
        }
        stream.Position = 0;
        return Task.FromResult<Stream>(stream);
    }

    public Task<Stream> CreatePdfFromDtoAsync(DocumentExportDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
        return CreatePdfAsync(pdf =>
        {
            float y = 40f;
            if (!string.IsNullOrWhiteSpace(descriptor.Title))
            {
                pdf.AddText(descriptor.Title, 40, y, 18f); y += 30f;
            }
            if (descriptor.LogoImage != null)
            {
                pdf.AddImage(descriptor.LogoImage, 450, 20, 120, 60);
            }
            if (descriptor.Headers.Count > 0)
            {
                pdf.AddTable(descriptor.Headers, descriptor.Rows, 40, y); y += (descriptor.Rows.Count + 1) * 18f + 20f;
            }
            if (!string.IsNullOrWhiteSpace(descriptor.Footer))
            {
                pdf.AddText(descriptor.Footer, 40, y + 10, 10f);
            }
        }, cancellationToken);
    }

    public Task<Stream> CreateExcelFromDtoAsync(DocumentExportDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
        return CreateExcelAsync(xl =>
        {
            int row = 1;
            if (!string.IsNullOrWhiteSpace(descriptor.Title))
            {
                xl.SetCell(row, 1, descriptor.Title); row += 2;
            }
            if (descriptor.Headers.Count > 0)
            {
                for (int c = 0; c < descriptor.Headers.Count; c++) xl.SetCell(row, c + 1, descriptor.Headers[c]);
                row++;
                foreach (var dataRow in descriptor.Rows)
                {
                    for (int c = 0; c < dataRow.Count; c++) xl.SetCell(row, c + 1, dataRow[c]);
                    row++;
                }
            }
            if (!string.IsNullOrWhiteSpace(descriptor.Footer))
            {
                xl.SetCell(row + 1, 1, descriptor.Footer);
            }
            xl.AutoFit();
        }, cancellationToken);
    }

    public async Task SaveToFileAtomicAsync(Stream source, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentNullException(nameof(destinationPath));
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
        var tempPath = destinationPath + ".tmp";
        source.Position = 0;
        using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true))
        {
            await source.CopyToAsync(fs, cancellationToken);
        }
        // Replace existing safely
        if (File.Exists(destinationPath)) File.Delete(destinationPath);
        File.Move(tempPath, destinationPath);
    }

    private sealed class PdfBuilder : IPdfBuilder
    {
        private readonly PdfPage _page;
        private readonly PdfFont _defaultFont;

        public PdfBuilder(PdfPage page)
        {
            _page = page;
            _defaultFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12f);
        }

        public IPdfBuilder AddText(string text, float x, float y, float fontSize = 12f)
        {
            var font = fontSize == 12f ? _defaultFont : new PdfStandardFont(PdfFontFamily.Helvetica, fontSize);
            _page.Graphics.DrawString(text, font, PdfBrushes.Black, x, y);
            return this;
        }

        public IPdfBuilder AddImage(byte[] imageBytes, float x, float y, float width, float height)
        {
            if (imageBytes == null || imageBytes.Length == 0) return this;
            using var ms = new MemoryStream(imageBytes);
#pragma warning disable CA1416 // 'Image.FromStream(Stream)' is only supported on: 'windows'
            using var img = System.Drawing.Image.FromStream(ms);
#pragma warning restore CA1416
            ms.Position = 0;
            using var pdfImage = new PdfBitmap(ms);
            _page.Graphics.DrawImage(pdfImage, x, y, width, height);
            return this;
        }

        public IPdfBuilder AddTable(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows, float startX, float startY, float rowHeight = 18f, float colWidth = 100f, float fontSize = 10f)
        {
            if (headers == null || headers.Count == 0) return this;
            var font = fontSize == 12f ? _defaultFont : new PdfStandardFont(PdfFontFamily.Helvetica, fontSize);
            float y = startY;
            // Header
            for (int c = 0; c < headers.Count; c++)
            {
                _page.Graphics.DrawString(headers[c], font, PdfBrushes.Black, startX + c * colWidth, y);
            }
            y += rowHeight;
            if (rows != null)
            {
                foreach (var r in rows)
                {
                    for (int c = 0; c < headers.Count && c < r.Count; c++)
                    {
                        _page.Graphics.DrawString(r[c] ?? string.Empty, font, PdfBrushes.Black, startX + c * colWidth, y);
                    }
                    y += rowHeight;
                }
            }
            return this;
        }
    }

    private sealed class ExcelBuilder : IExcelBuilder
    {
        private readonly IWorksheet _sheet;
        public ExcelBuilder(IWorksheet sheet) => _sheet = sheet;

        public IExcelBuilder SetCell(int row, int column, string text)
        {
            _sheet[row, column].Text = text;
            return this;
        }

        public IExcelBuilder AutoFit()
        {
            _sheet.UsedRange.AutofitColumns();
            return this;
        }
    }
}
