using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Parsing;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Minimal Syncfusion-backed IPdfService implementation for quick-unblock.
    /// Implements only a simple CreatePdfAsync and basic helpers required by ReportExportService.
    /// </summary>
    public class SyncfusionPdfService : IPdfService
    {
        public async Task<byte[]> CreatePdfAsync(Func<IPdfContentBuilder, Task> buildAsync, CancellationToken cancellationToken = default)
        {
            using var doc = new PdfDocument();

            var builder = new SyncfusionPdfContentBuilder(doc);

            if (buildAsync != null)
            {
                await buildAsync(builder).ConfigureAwait(false);
            }

            using var ms = new MemoryStream();
            doc.Save(ms);
            return ms.ToArray();
        }

        public Task SavePdfAsync(byte[] pdfBytes, string path, CancellationToken cancellationToken = default)
        {
            if (pdfBytes == null) throw new ArgumentNullException(nameof(pdfBytes));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

            File.WriteAllBytes(path, pdfBytes);
            return Task.CompletedTask;
        }

        public Task<byte[]> MergePdfsAsync(IEnumerable<byte[]> pdfFiles, CancellationToken cancellationToken = default)
        {
            // Quick-unblock: return the first usable PDF as a fallback merge behavior.
            var first = pdfFiles?.FirstOrDefault(b => b != null && b.Length > 0);
            return Task.FromResult(first ?? Array.Empty<byte>());
        }

        public Task<PdfInfo> GetPdfInfoAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
        {
            if (pdfBytes == null) throw new ArgumentNullException(nameof(pdfBytes));

            using var loaded = new PdfLoadedDocument(pdfBytes);
            var info = new PdfInfo(loaded.Pages.Count, loaded.DocumentInformation?.Title, loaded.DocumentInformation?.Author, pdfBytes.LongLength);
            return Task.FromResult(info);
        }

        public Task<string> ExtractTextAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
        {
            // Minimal stub: Syncfusion text extraction requires PDF parsing; return empty string for quick-unblock
            return Task.FromResult(string.Empty);
        }

        public Task<byte[]> FillFormAsync(byte[] pdfBytes, IDictionary<string, string> values, bool flatten = false, CancellationToken cancellationToken = default)
        {
            // Not implemented for quick-unblock — return original bytes
            return Task.FromResult(pdfBytes);
        }

        public Task<byte[]> FlattenPdfFormsAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
        {
            // Not implemented for quick-unblock — return original bytes
            return Task.FromResult(pdfBytes);
        }

        private class SyncfusionPdfContentBuilder : IPdfContentBuilder
        {
            private readonly PdfDocument _doc;
            private PdfPage? _currentPage;
            private PdfGraphics? _gfx;

            public SyncfusionPdfContentBuilder(PdfDocument doc)
            {
                _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            }

            public void AddImage(Stream imageStream, float x, float y, float width, float height)
            {
                if (_currentPage == null) AddPage();
                try
                {
                    using var ms = new MemoryStream();
                    imageStream.CopyTo(ms);
                    using var img = new PdfBitmap(ms);
                    _gfx!.DrawImage(img, x, y, width, height);
                }
                catch { }
            }

            public void AddPage()
            {
                _currentPage = _doc.Pages.Add();
                _gfx = _currentPage.Graphics;
            }

            public void AddTable(IEnumerable<IEnumerable<string>> rows, float x, float y)
            {
                // Minimal: render table as plain text lines
                if (_currentPage == null) AddPage();
                var yOffset = y;
                foreach (var r in rows)
                {
                    var line = string.Join(" \t", r);
                    _gfx!.DrawString(line, new PdfStandardFont(PdfFontFamily.Helvetica, 10), PdfBrushes.Black, new System.Drawing.PointF(x, yOffset));
                    yOffset += 14;
                }
            }

            public void DrawText(string text, float x, float y, string? fontName = null, float fontSize = 12f)
            {
                if (_currentPage == null) AddPage();
                try
                {
                    var font = string.IsNullOrEmpty(fontName) ? new PdfStandardFont(PdfFontFamily.Helvetica, fontSize) : new PdfStandardFont(PdfFontFamily.Helvetica, fontSize);
                    _gfx!.DrawString(text ?? string.Empty, font, PdfBrushes.Black, new System.Drawing.PointF(x, y));
                }
                catch { }
            }

            public void SetMetadata(string? title = null, string? author = null)
            {
                if (!string.IsNullOrEmpty(title)) _doc.DocumentInformation.Title = title;
                if (!string.IsNullOrEmpty(author)) _doc.DocumentInformation.Author = author;
            }
        }
    }
}
