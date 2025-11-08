using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Excel;
using Xunit;

namespace WileyWidget.Tests;

public class BudgetImporterTests
{
    [Fact]
    public async Task ValidateImportFileAsync_ReturnsError_ForMissingFile()
    {
        var mockExcel = new Mock<IExcelReaderService>();
        var mockLogger = new Mock<ILogger<BudgetImporter>>();
        var mockRepo = new Mock<WileyWidget.Business.Interfaces.IBudgetRepository>();

        var importer = new BudgetImporter(mockExcel.Object, mockLogger.Object, mockRepo.Object);

        var errors = await importer.ValidateImportFileAsync("nonexistent.xlsx");
        errors.Should().ContainSingle().Which.Should().Contain("does not exist");
    }

    [Fact]
    public async Task ValidateImportFileAsync_ReturnsError_ForUnsupportedExtension()
    {
        var mockExcel = new Mock<IExcelReaderService>();
        var mockLogger = new Mock<ILogger<BudgetImporter>>();
        var mockRepo = new Mock<WileyWidget.Business.Interfaces.IBudgetRepository>();

        var importer = new BudgetImporter(mockExcel.Object, mockLogger.Object, mockRepo.Object);

        // create a temp file with unsupported extension
        var tmp = Path.GetTempFileName();
        var newPath = Path.ChangeExtension(tmp, ".txt");
        File.Move(tmp, newPath);
        try
        {
            var errors = await importer.ValidateImportFileAsync(newPath);
            errors.Should().Contain(e => e.Contains("Unsupported file extension"));
        }
        finally
        {
            File.Delete(newPath);
        }
    }

    [Fact]
    public async Task ImportBudgetAsync_UsesExcelReader_AndReturnsValidatedEntries()
    {
        var mockExcel = new Mock<IExcelReaderService>();
        var mockLogger = new Mock<ILogger<BudgetImporter>>();
        var mockRepo = new Mock<WileyWidget.Business.Interfaces.IBudgetRepository>();

        // create temp csv file (supported extension)
        var tmp = Path.GetTempFileName();
        var csvPath = Path.ChangeExtension(tmp, ".csv");
        File.Move(tmp, csvPath);

        mockExcel
            .Setup(x => x.ReadBudgetDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<BudgetEntry>
            {
                new BudgetEntry { AccountNumber = "100", Description = "Desc", FiscalYear = 2025 }
            });

        var importer = new BudgetImporter(mockExcel.Object, mockLogger.Object, mockRepo.Object);

        var result = await importer.ImportBudgetAsync(csvPath);
        result.Should().NotBeNull();
        result.Count().Should().BeGreaterThan(0);

        File.Delete(csvPath);
    }
}
