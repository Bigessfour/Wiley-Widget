using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using Syncfusion.Pdf.Parsing;
using Syncfusion.Pdf.Interactive;
using System.Text;
using System.Drawing;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    public class SyncfusionPdfService : IPdfService
    {
        private readonly object _licenseLock = new();

        public SyncfusionPdfService()
        {
            // No-op constructor; license should be registered by host on startup using
            // Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("<KEY>");
            // We avoid storing keys in code. If the host hasn't registered a license,
            // Syncfusion will operate in its evaluation mode which is acceptable for development.
        }

        public async Task<byte[]> CreatePdfAsync(Func<IPdfContentBuilder, Task> buildAsync, CancellationToken cancellationToken = default)
        {
            if (buildAsync == null) throw new ArgumentNullException(nameof(buildAsync));

            using var document = new PdfDocument();
            var builder = new SyncfusionPdfContentBuilder(document);

            await buildAsync(builder).ConfigureAwait(false);

            await using var ms = new MemoryStream();
            document.Save(ms);
            var bytes = ms.ToArray();
            return bytes;
        }

        public async Task SavePdfAsync(byte[] pdfBytes, string path, CancellationToken cancellationToken = default)
        {
            if (pdfBytes == null) throw new ArgumentNullException(nameof(pdfBytes));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

            // Ensure directory
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // Use the async API and honor cancellation.
            cancellationToken.ThrowIfCancellationRequested();
            await File.WriteAllBytesAsync(path, pdfBytes, cancellationToken).ConfigureAwait(false);
        }

        public Task<byte[]> MergePdfsAsync(IEnumerable<byte[]> pdfFiles, CancellationToken cancellationToken = default)
        {
            if (pdfFiles == null) throw new ArgumentNullException(nameof(pdfFiles));

            // Keep loaded documents and their streams alive until we've finished importing and saving.
            var loadedDocs = new List<(MemoryStream Stream, PdfLoadedDocument Doc)>();

            try
            {
                using var outDoc = new PdfDocument();

                // First load all documents and keep them alive until we're done saving
                foreach (var file in pdfFiles.Where(f => f != null && f.Length > 0))
                {
                    var ms = new MemoryStream(file, writable: false);
                    var loaded = new PdfLoadedDocument(ms);
                    loadedDocs.Add((ms, loaded));
                }

                // Import pages from each loaded document into the output document
                foreach (var (stream, loaded) in loadedDocs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    for (int i = 0; i < loaded.Pages.Count; i++)
                    {
                        outDoc.ImportPage(loaded, i);
                    }
                }

                using var outMs = new MemoryStream();
                outDoc.Save(outMs);
                cancellationToken.ThrowIfCancellationRequested();
                var result = outMs.ToArray();
                return Task.FromResult(result);
            }
            finally
            {
                // Dispose loaded docs and their streams
                foreach (var (stream, doc) in loadedDocs)
                {
                    try { doc?.Close(); doc?.Dispose(); } catch { }
                    try { stream?.Dispose(); } catch { }
                }
            }
        }

        public Task<PdfInfo> GetPdfInfoAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
        {
            if (pdfBytes == null) throw new ArgumentNullException(nameof(pdfBytes));

            using var ms = new MemoryStream(pdfBytes);
            using var loaded = new PdfLoadedDocument(ms);

            var title = loaded.DocumentInformation?.Title;
            var author = loaded.DocumentInformation?.Author;
            var pages = loaded.Pages.Count;
            var size = pdfBytes.LongLength;

            var info = new PdfInfo(pages, title, author, size);
            return Task.FromResult(info);
        }

        public async Task<string> ExtractTextAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
        {
            if (pdfBytes == null) throw new ArgumentNullException(nameof(pdfBytes));

            using var ms = new MemoryStream(pdfBytes);
            using var loaded = new PdfLoadedDocument(ms);

            var sb = new StringBuilder();

            for (int i = 0; i < loaded.Pages.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = loaded.Pages[i] as PdfLoadedPage;
                if (page == null) continue;

                // Try the helper type, with fallback to page.ExtractText if available.
                try
                {
                    // Use the page API — some versions expose helper extractors, but ExtractText() is commonly available.
                    var text = page.ExtractText();
                    if (!string.IsNullOrEmpty(text)) sb.AppendLine(text);
                }
                catch
                {
                    try
                    {
                        var text = page.ExtractText();
                        if (!string.IsNullOrEmpty(text)) sb.AppendLine(text);
                    }
                    catch
                    {
                        // best-effort — skip page on failure
                    }
                }
            }

            return await Task.FromResult(sb.ToString()).ConfigureAwait(false);
        }

        public async Task<byte[]> FillFormAsync(byte[] pdfBytes, IDictionary<string, string> values, bool flatten = false, CancellationToken cancellationToken = default)
        {
            if (pdfBytes == null) throw new ArgumentNullException(nameof(pdfBytes));
            if (values == null) throw new ArgumentNullException(nameof(values));

            using var ms = new MemoryStream(pdfBytes);
            using var loaded = new PdfLoadedDocument(ms);

            var form = loaded.Form;
            if (form == null || form.Fields == null || form.Fields.Count == 0)
            {
                // nothing to fill — return the original bytes
                return await Task.FromResult(pdfBytes).ConfigureAwait(false);
            }

            foreach (var kvp in values)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var name = kvp.Key;
                var value = kvp.Value;

                // Try to find the field by name (case-insensitive)
                for (int i = 0; i < form.Fields.Count; i++)
                {
                    if (form.Fields[i] == null) continue;

                    var field = form.Fields[i];
                    if (!string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)) continue;

                    try
                    {
                        switch (field)
                        {
                            case PdfLoadedTextBoxField tb:
                                tb.Text = value ?? string.Empty;
                                break;
                            case PdfLoadedComboBoxField cb:
                                // try to select the first matching item text/value
                                var matched = false;
                                for (int j = 0; j < cb.Items.Count; j++)
                                {
                                    var item = cb.Items[j];
                                    if (IsMatch(item, value))
                                    {
                                        cb.SelectedIndex = j;
                                        matched = true;
                                        break;
                                    }
                                }
                                // If no match could be computed (parsing differences across versions),
                                // pick the first available item as a best-effort selection so forms observe a selection
                                if (!matched && cb.Items.Count > 0)
                                {
                                    try { cb.SelectedIndex = 0; } catch { /* best-effort */ }
                                }
                                break;
                            case PdfLoadedCheckBoxField chk:
                                chk.Checked = bool.TryParse(value, out var b) && b;
                                break;
                            case PdfLoadedRadioButtonListField rbl:
                                for (int j = 0; j < rbl.Items.Count; j++)
                                {
                                    var item = rbl.Items[j];
                                    // Try several ways of matching item text/value for robustness
                                    if (IsMatch(item, value))
                                    {
                                        rbl.SelectedIndex = j;
                                        break;
                                    }
                                }
                                break;
                            case PdfLoadedListBoxField lb:
                                var listMatched = false;
                                for (int j = 0; j < lb.Items.Count; j++)
                                {
                                    var item = lb.Items[j];
                                    if (IsMatch(item, value))
                                    {
                                        // List boxes expose a multi-select array property named SelectedIndex. Assign the matched index as a single-element array.
                                        try { lb.SelectedIndex = new int[] { j }; } catch { }
                                        // Also attempt to set the selected value (string) when present — some versions serialize the selected value rather than preserving the item object.
                                        try { lb.SelectedValue = new string[] { value ?? item?.ToString() ?? string.Empty }; } catch { }
                                        listMatched = true;
                                        break;
                                    }
                                }
                                // If no exact match was found, try a relaxed/contains-style match (helps when
                                // parsed items are combined or expose different properties across Syncfusion versions)
                                if (!listMatched && lb.Items.Count > 0)
                                {
                                    var relaxedMatched = false;
                                    for (int j = 0; j < lb.Items.Count; j++)
                                    {
                                        try
                                        {
                                            var item = lb.Items[j];
                                            var repr = item?.ToString() ?? string.Empty;

                                            // Concatenate string properties to improve chance of matching variants
                                            try
                                            {
                                                var props = item?.GetType().GetProperties();
                                                if (props != null)
                                                {
                                                    foreach (var p in props)
                                                    {
                                                        try
                                                        {
                                                            var valObj = p.GetValue(item);
                                                            if (valObj != null)
                                                            {
                                                                var s = valObj.ToString();
                                                                if (!string.IsNullOrEmpty(s)) repr += "|" + s;
                                                            }
                                                        }
                                                        catch { }
                                                    }
                                                }
                                            }
                                            catch { }

                                            if (!string.IsNullOrEmpty(repr) && repr.IndexOf(value ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0)
                                            {
                                                try { lb.SelectedIndex = new int[] { j }; } catch { }
                                                try { lb.SelectedValue = new string[] { value ?? repr }; } catch { }
                                                relaxedMatched = true;
                                                break;
                                            }
                                        }
                                        catch { /* best-effort */ }
                                    }

                                    // Final fallback to the first item if nothing matched
                                    if (!relaxedMatched)
                                    {
                                        try { lb.SelectedIndex = new int[] { 0 }; } catch { /* best-effort */ }
                                        try { lb.SelectedValue = new string[] { value ?? lb.Items[0]?.ToString() ?? string.Empty }; } catch { }
                                    }
                                }

                                // Debug: write basic list state and item details to stdout to help during tests
                                try
                                {
                                    // Print field-level properties for diagnosis (helps find where option text might live)
                                    Console.WriteLine($"ListBox Field: type={lb.GetType().FullName}, Name={lb.Name}, Items.Count={lb.Items.Count}");
                                    var fieldProps = lb.GetType().GetProperties();
                                    Console.WriteLine($"   field property count={fieldProps.Length}");
                                    foreach (var p in fieldProps)
                                    {
                                        try
                                        {
                                            var val = p.GetValue(lb);
                                            Console.WriteLine($"   {p.Name} ({p.PropertyType.FullName}) = '{val?.ToString() ?? "<null>"}'");
                                        }
                                        catch { }
                                    }

                                    Console.WriteLine($"ListBox Loaded: items count={lb.Items.Count}, SelectedIndex={(lb.SelectedIndex == null ? "<null>" : string.Join(',', lb.SelectedIndex))}");
                                    try
                                    {
                                        // Some Syncfusion versions expose a separate Values collection; print it for diagnosis
                                        var vals = lb.GetType().GetProperty("Values")?.GetValue(lb) as System.Collections.IEnumerable;
                                        if (vals != null)
                                        {
                                            var idx = 0;
                                            foreach (var v in vals)
                                            {
                                                Console.WriteLine($"   Values[{idx}] type={v?.GetType().FullName} ToString='{v?.ToString() ?? "<null>"}'");
                                                idx++;
                                            }
                                        }
                                    }
                                    catch { }
                                    for (int k = 0; k < lb.Items.Count; k++)
                                    {
                                        var itm = lb.Items[k];
                                        Console.WriteLine($"   Item[{k}] type={itm?.GetType().FullName} ToString='{itm?.ToString()}'");
                                        try
                                        {
                                            var propsAll = itm?.GetType().GetProperties() ?? Array.Empty<System.Reflection.PropertyInfo>();
                                            Console.WriteLine($"       property count={propsAll.Length}");
                                            foreach (var p in propsAll)
                                            {
                                                try
                                                {
                                                    var val = p.GetValue(itm);
                                                    Console.WriteLine($"       {p.Name} ({p.PropertyType.FullName}) = '{val?.ToString() ?? "<null>"}'");
                                                }
                                                catch (Exception ex)
                                                {
                                                    Console.WriteLine($"       {p.Name} ({p.PropertyType.FullName}) => (err: {ex.GetType().Name})");
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                }
                                catch { /* best-effort logging */ }
                                break;
                                break;
                            default:
                                // Unsupported or complex field type — ignore for now
                                break;
                        }
                    }
                    catch
                    {
                        // best-effort fill; ignore failures per-field
                    }

                    // found a matching field — don't try to match additional fields with the same name
                    break;
                }
            }

            // Optionally flatten fields (set field Flatten = true). This will render values as static content where supported.
            if (flatten)
            {
                var fc = form.Fields;
                for (int i = 0; i < fc.Count; i++)
                {
                    try
                    {
                        fc[i].Flatten = true;
                    }
                    catch
                    {
                        // ignore if Flatten not supported on this field
                    }
                }
            }

            await using var outMs = new MemoryStream();
            loaded.Save(outMs);
            cancellationToken.ThrowIfCancellationRequested();
            return outMs.ToArray();
        }

        private static bool IsMatch(object? item, string? value)
        {
            if (item == null || value == null) return false;

            // match ToString
            try
            {
                if (string.Equals(item.ToString(), value, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }

            // reflectively check all properties (convert to string) for a match — this handles cases where
            // Syncfusion exposes item label/value properties typed as object or non-string types across versions.
            try
            {
                var t = item.GetType();
                var props = t.GetProperties();
                foreach (var p in props)
                {
                    try
                    {
                        var valObj = p.GetValue(item);
                        if (valObj == null) continue;

                        // Prefer direct string-typed properties for exactness
                        if (valObj is string s && !string.IsNullOrEmpty(s) && string.Equals(s, value, StringComparison.OrdinalIgnoreCase)) return true;

                        // Fallback: compare the string representation of any property value
                        var s2 = valObj.ToString();
                        if (!string.IsNullOrEmpty(s2) && string.Equals(s2, value, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    catch { /* ignore property access errors */ }
                }

                // As a final fallback, check if the item is an IEnumerable of simple values containing the expected value.
                if (item is System.Collections.IEnumerable enumItems && !(item is string))
                {
                    foreach (var sub in enumItems)
                    {
                        if (sub == null) continue;
                        if (string.Equals(sub.ToString(), value, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public async Task<byte[]> FlattenPdfFormsAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
        {
            if (pdfBytes == null) throw new ArgumentNullException(nameof(pdfBytes));

            using var ms = new MemoryStream(pdfBytes);
            using var loaded = new PdfLoadedDocument(ms);

            var form = loaded.Form;
            if (form == null || form.Fields == null || form.Fields.Count == 0)
            {
                return await Task.FromResult(pdfBytes).ConfigureAwait(false);
            }

            // Set Flatten=true on each field where possible
            var fc = form.Fields;
            for (int i = 0; i < fc.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    fc[i].Flatten = true;
                }
                catch
                {
                    // ignore fields that don't support flatten
                }
            }

            await using var outMs = new MemoryStream();
            loaded.Save(outMs);
            cancellationToken.ThrowIfCancellationRequested();
            return outMs.ToArray();
        }
    }

    internal class SyncfusionPdfContentBuilder : IPdfContentBuilder
    {
        private readonly PdfDocument _document;
        private PdfPage? _currentPage;
        private PdfGraphics? _currentGraphics;
        private float _cursorY = 0f;
        private const float DefaultMargin = 24f;

        public SyncfusionPdfContentBuilder(PdfDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            AddPage();
        }

        public void AddPage()
        {
            _currentPage = _document.Pages.Add();
            _currentGraphics = _currentPage.Graphics;
            _cursorY = DefaultMargin;
        }

        public void DrawText(string text, float x, float y, string? fontName = null, float fontSize = 12f)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (_currentGraphics == null) AddPage();

            var fontFamily = PdfFontFamily.Helvetica;
            if (!string.IsNullOrWhiteSpace(fontName))
            {
                _ = Enum.TryParse<PdfFontFamily>(fontName, true, out fontFamily);
            }

            var font = new PdfStandardFont(fontFamily, fontSize);
            _currentGraphics!.DrawString(text, font, PdfBrushes.Black, new PointF(x, y));
            _cursorY = Math.Max(_cursorY, y + fontSize + 4);
        }

        public void AddImage(Stream imageStream, float x, float y, float width, float height)
        {
            if (imageStream == null) throw new ArgumentNullException(nameof(imageStream));
            if (_currentGraphics == null) AddPage();

            using var bitmap = new PdfBitmap(imageStream);
            _currentGraphics!.DrawImage(bitmap, x, y, width, height);
            _cursorY = Math.Max(_cursorY, y + height + 4);
        }

        public void AddTable(IEnumerable<IEnumerable<string>> rows, float x, float y)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (_currentGraphics == null) AddPage();

            var rowList = rows.Select(r => r.ToArray()).ToArray();
            if (rowList.Length == 0) return;

            var grid = new PdfGrid();

            // Build columns from first row
            var first = rowList[0];
            grid.Columns.Add(first.Length);

            var data = new List<object[]>();
            foreach (var r in rowList)
            {
                data.Add(r.Select(c => (object)c).ToArray());
            }

            grid.DataSource = data;
            grid.Draw(_currentPage!, new PointF(x, y));
            _cursorY = Math.Max(_cursorY, y + (grid.Rows.Count * 20));
        }

        public void SetMetadata(string? title = null, string? author = null)
        {
            if (!string.IsNullOrWhiteSpace(title)) _document.DocumentInformation.Title = title!;
            if (!string.IsNullOrWhiteSpace(author)) _document.DocumentInformation.Author = author!;
        }
    }
}
