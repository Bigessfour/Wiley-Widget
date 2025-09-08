using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services;

/// <summary>
/// High-level pipeline for creating document exports (PDF, Excel) in a consistent async pattern.
/// Keeps implementation thin and only uses documented Syncfusion APIs.
/// </summary>
public interface IExportPipeline
{
    Task<Stream> CreatePdfAsync(Action<IPdfBuilder> buildAction, CancellationToken cancellationToken = default);
    Task<Stream> CreateExcelAsync(Action<IExcelBuilder> buildAction, CancellationToken cancellationToken = default);

    // Strongly-typed DTO mapping helpers
    Task<Stream> CreatePdfFromDtoAsync(DocumentExportDescriptor descriptor, CancellationToken cancellationToken = default);
    Task<Stream> CreateExcelFromDtoAsync(DocumentExportDescriptor descriptor, CancellationToken cancellationToken = default);

    Task SaveToFileAtomicAsync(Stream source, string destinationPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fluent PDF builder abstraction (thin wrapper) to isolate direct Syncfusion usage.
/// </summary>
public interface IPdfBuilder
{
    IPdfBuilder AddText(string text, float x, float y, float fontSize = 12f);
    IPdfBuilder AddImage(byte[] imageBytes, float x, float y, float width, float height);
    IPdfBuilder AddTable(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows, float startX, float startY, float rowHeight = 18f, float colWidth = 100f, float fontSize = 10f);
}

/// <summary>
/// Fluent Excel builder abstraction.
/// </summary>
public interface IExcelBuilder
{
    IExcelBuilder SetCell(int row, int column, string text);
    IExcelBuilder AutoFit();
}

/// <summary>
/// Descriptor for a generic export (simple abstraction, can be extended later).
/// </summary>
public sealed class DocumentExportDescriptor
{
    public string Title { get; set; }
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
    public string Footer { get; set; }
    public byte[] LogoImage { get; set; } // optional
}
