using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.Tests.Services;

public class ExportPipelineTests
{
    private readonly IExportPipeline _pipeline = new SyncfusionExportPipeline();

    [Fact]
    public async Task CreatePdf_FromBuilder_BasicMetadata()
    {
        using var pdfStream = await _pipeline.CreatePdfAsync(p => p.AddText("Hello", 20, 20));
        pdfStream.Length.Should().BeGreaterThan(200); // minimal size
        // Simple magic number check for PDF header
        pdfStream.Position = 0;
        using var reader = new StreamReader(pdfStream, leaveOpen:true);
        var header = new char[5];
        await reader.ReadAsync(header, 0, 5);
        new string(header).Should().StartWith("%PDF-");
    }

    [Fact]
    public async Task CreateExcel_FromDto_HasContent()
    {
        var dto = new DocumentExportDescriptor
        {
            Title = "Sample",
            Headers = { "Name", "Value" },
            Rows = { new() { "Widgets", "42" } },
            Footer = "End"
        };
        using var excel = await _pipeline.CreateExcelFromDtoAsync(dto);
        excel.Length.Should().BeGreaterThan(500); // XLSX container size
        excel.Position = 0;
        var buf = new byte[4];
        await excel.ReadAsync(buf,0,4);
        // PK zip header for OOXML packaging
        buf[0].Should().Be(0x50); // 'P'
        buf[1].Should().Be(0x4B); // 'K'
    }

    [Fact]
    public async Task Cancellation_Triggers_Throw()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => _pipeline.CreatePdfAsync(_ => { }, cts.Token));
    }

    [Fact]
    public async Task AtomicSave_WritesFile()
    {
        using var pdfStream = await _pipeline.CreatePdfAsync(p => p.AddText("SaveTest", 10,10));
        var path = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.pdf");
        await _pipeline.SaveToFileAtomicAsync(pdfStream, path);
        File.Exists(path).Should().BeTrue();
        new FileInfo(path).Length.Should().BeGreaterThan(200);
        File.Delete(path);
    }
}
