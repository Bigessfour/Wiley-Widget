using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions
{
    public record PdfInfo(int PageCount, string? Title, string? Author, long FileSize);

    public interface IPdfContentBuilder
    {
        void AddPage();
        void DrawText(string text, float x, float y, string? fontName = null, float fontSize = 12f);
        void AddImage(Stream imageStream, float x, float y, float width, float height);
        void AddTable(IEnumerable<IEnumerable<string>> rows, float x, float y);
        void SetMetadata(string? title = null, string? author = null);
    }

    public interface IPdfService
    {
        /// <summary>
        /// Create a PDF by invoking the provided builder action which uses the provided <see cref="IPdfContentBuilder"/>.
        /// Returns the PDF bytes.
        /// </summary>
        Task<byte[]> CreatePdfAsync(Func<IPdfContentBuilder, Task> buildAsync, CancellationToken cancellationToken = default);

        /// <summary>
        /// Save raw PDF bytes to disk (overwrites existing file).
        /// </summary>
        Task SavePdfAsync(byte[] pdfBytes, string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Merge multiple PDF byte arrays into a single PDF and return combined bytes.
        /// </summary>
        Task<byte[]> MergePdfsAsync(IEnumerable<byte[]> pdfFiles, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get basic metadata (page count, title, author) from a PDF byte array.
        /// </summary>
        Task<PdfInfo> GetPdfInfoAsync(byte[] pdfBytes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extract all text from a PDF as a single string.
        /// </summary>
        Task<string> ExtractTextAsync(byte[] pdfBytes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fill form fields on a PDF. The returned bytes represent the modified document. If <paramref name="flatten"/> is true the form fields will be flattened to static content when possible.
        /// Only common field types are supported by the service (text, checkbox, radio, combo); unsupported fields will be left untouched.
        /// </summary>
        Task<byte[]> FillFormAsync(byte[] pdfBytes, IDictionary<string, string> values, bool flatten = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Flatten form fields in an existing PDF into static page content where possible and return the flattened document bytes.
        /// </summary>
        Task<byte[]> FlattenPdfFormsAsync(byte[] pdfBytes, CancellationToken cancellationToken = default);
    }
}
