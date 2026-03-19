using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services;

public sealed class QuickBooksDesktopIifParserTests : IDisposable
{
    private readonly string _tempDirectory;

    public QuickBooksDesktopIifParserTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "WileyWidgetIifParserTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task ParseAsync_SkipsBangHeaderRows_AndMapsAccountRows()
    {
        var filePath = Path.Combine(_tempDirectory, "accounts.iif");
        await File.WriteAllTextAsync(
            filePath,
            string.Join(Environment.NewLine,
                "!ACCNT\tNAME\tACCNTTYPE\tDESC",
                "ACCNT\t1000 Cash\tAsset\tCash operating account",
                "ACCNT\t2000 Accounts Payable\tLiability\tVendor liabilities"));

        var parser = new QuickBooksDesktopIifParser(NullLogger<QuickBooksDesktopIifParser>.Instance);

        var table = await parser.ParseAsync(filePath);

        table.Rows.Count.Should().Be(2);
        table.Columns.Contains("NAME").Should().BeTrue();
        table.Columns.Contains("ACCNTTYPE").Should().BeTrue();
        table.Rows[0]["_RecordType"].Should().Be("ACCNT");
        table.Rows[0]["NAME"].Should().Be("1000 Cash");
        table.Rows[1]["ACCNTTYPE"].Should().Be("Liability");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
