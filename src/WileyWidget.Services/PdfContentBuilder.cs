using System;
using System.Collections.Generic;
using System.IO;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Public PDF content builder for DI scenarios. This is a lightweight implementation
    /// that mirrors the internal Syncfusion builder used by SyncfusionPdfService.
    /// </summary>
    public class PdfContentBuilder : IPdfContentBuilder
    {
        private readonly PdfDocument _doc;
        private PdfPage? _page;
        private PdfGraphics? _gfx;

        public PdfContentBuilder()
        {
            _doc = new PdfDocument();
        }

        public PdfDocument Document => _doc;

        public void AddImage(Stream imageStream, float x, float y, float width, float height)
        {
            if (_page == null) AddPage();
            try
            {
                using var ms = new MemoryStream();
                imageStream.CopyTo(ms);
                using var bmp = new PdfBitmap(ms);
                _gfx!.DrawImage(bmp, x, y, width, height);
            }
            catch { }
        }

        public void AddPage()
        {
            _page = _doc.Pages.Add();
            _gfx = _page.Graphics;
        }

        public void AddTable(IEnumerable<IEnumerable<string>> rows, float x, float y)
        {
            if (_page == null) AddPage();
            var yOffset = y;
            foreach (var r in rows)
            {
                var line = string.Join("\t", r);
                _gfx!.DrawString(line, new PdfStandardFont(PdfFontFamily.Helvetica, 10), PdfBrushes.Black, new System.Drawing.PointF(x, yOffset));
                yOffset += 14;
            }
        }

        public void DrawText(string text, float x, float y, string? fontName = null, float fontSize = 12f)
        {
            if (_page == null) AddPage();
            var font = new PdfStandardFont(PdfFontFamily.Helvetica, fontSize);
            _gfx!.DrawString(text ?? string.Empty, font, PdfBrushes.Black, new System.Drawing.PointF(x, y));
        }

        public void SetMetadata(string? title = null, string? author = null)
        {
            if (!string.IsNullOrEmpty(title)) _doc.DocumentInformation.Title = title;
            if (!string.IsNullOrEmpty(author)) _doc.DocumentInformation.Author = author;
        }
    }
}
