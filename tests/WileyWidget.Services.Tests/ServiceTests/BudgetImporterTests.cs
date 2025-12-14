using Xunit;
using Moq;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Excel;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Services.Tests.ServiceTests;

/// <summary>
/// Tests for BudgetImporter service - file import, validation, and data processing.
/// Validates business rules for budget data integrity per project requirements.
/// </summary>
public class BudgetImporterTests : IDisposable
{
    private readonly Mock<IExcelReaderService> _mockExcelReader;
    private readonly Mock<ILogger<BudgetImporter>> _mockLogger;
    private readonly Mock<IBudgetRepository> _mockBudgetRepository;
    private readonly BudgetImporter _importer;

    public BudgetImporterTests()
    {
        _mockExcelReader = new Mock<IExcelReaderService>();
        _mockLogger = new Mock<ILogger<BudgetImporter>>();
        _mockBudgetRepository = new Mock<IBudgetRepository>();

        _importer = new BudgetImporter(
            _mockExcelReader.Object,
            _mockLogger.Object,
            _mockBudgetRepository.Object);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenExcelReaderIsNull()
    {
        // Act
        Action act = () => new BudgetImporter(
            null!,
            _mockLogger.Object,
            _mockBudgetRepository.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("excelReaderService");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act
        Action act = () => new BudgetImporter(
            _mockExcelReader.Object,
            null!,
            _mockBudgetRepository.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void SupportedExtensions_ShouldIncludeExcelAndCsvFormats()
    {
        // Act
        var extensions = _importer.SupportedExtensions.ToList();

        // Assert
        extensions.Should().Contain(".xlsx");
        extensions.Should().Contain(".xls");
        extensions.Should().Contain(".csv");
        extensions.Count.Should().Be(3);
    }

    [Fact]
    public async Task ValidateImportFile_ShouldReturnError_WhenFilePathIsNull()
    {
        // Act
        var errors = await _importer.ValidateImportFileAsync(null!);

        // Assert
        errors.Should().ContainSingle()
            .Which.Should().Contain("File path cannot be null");
    }

    [Fact]
    public async Task ValidateImportFile_ShouldReturnError_WhenFilePathIsEmpty()
    {
        // Act
        var errors = await _importer.ValidateImportFileAsync(string.Empty);

        // Assert
        errors.Should().ContainSingle()
            .Which.Should().Contain("File path cannot be null");
    }

    [Fact]
    public async Task ValidateImportFile_ShouldReturnError_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentPath = "C:\\nonexistent\\budget.xlsx";

        // Act
        var errors = await _importer.ValidateImportFileAsync(nonExistentPath);

        // Assert
        errors.Should().ContainSingle()
            .Which.Should().Contain("File does not exist");
    }

    [Theory]
    [InlineData("budget.txt")]
    [InlineData("budget.pdf")]
    [InlineData("budget.json")]
    public async Task ValidateImportFile_ShouldReturnError_WhenFileExtensionNotSupported(string fileName)
    {
        // Arrange
        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fileName);
        System.IO.File.WriteAllText(tempPath, "test");

        try
        {
            // Act
            var errors = await _importer.ValidateImportFileAsync(tempPath);

            // Assert
            errors.Should().ContainSingle()
                .Which.Should().Contain("Unsupported file extension");
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ImportBudget_ShouldThrowArgumentException_WhenFilePathIsNull()
    {
        // Act
        Func<Task> act = async () => await _importer.ImportBudgetAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("filePath");
    }

    [Fact]
    public async Task ImportBudget_ShouldThrowFileNotFoundException_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentPath = "C:\\nonexistent\\budget.xlsx";

        // Act
        Func<Task> act = async () => await _importer.ImportBudgetAsync(nonExistentPath);

        // Assert
        await act.Should().ThrowAsync<System.IO.FileNotFoundException>()
            .WithMessage("*Budget file not found*");
    }

    public void Dispose()
    {
        // Cleanup if needed
        GC.SuppressFinalize(this);
    }
}
