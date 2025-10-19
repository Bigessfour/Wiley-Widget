using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.Models;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.LifecycleTests;

/// <summary>
/// Integration tests for ReportExportService to validate actual file export functionality
/// </summary>
public sealed class ReportExportServiceIntegrationTests
{
    private readonly IReportExportService _reportExportService;

    public ReportExportServiceIntegrationTests()
    {
        _reportExportService = new ReportExportService();
    }

    [Fact]
    public async Task ExportToCsvAsync_WithEnterpriseData_CreatesValidCsvFile()
    {
        // Arrange
        var testData = new List<Enterprise>
        {
            new Enterprise
            {
                Id = 1,
                Name = "Test Water Company",
                Type = "Water",
                CurrentRate = 25.50m,
                MonthlyExpenses = 15000m,
                CitizenCount = 1200,
                CreatedDate = new DateTime(2024, 1, 15),
                ModifiedDate = new DateTime(2024, 10, 19),
                Status = EnterpriseStatus.Active
            },
            new Enterprise
            {
                Id = 2,
                Name = "Test Electric Company",
                Type = "Electric",
                CurrentRate = 45.75m,
                MonthlyExpenses = 25000m,
                CitizenCount = 800,
                CreatedDate = new DateTime(2024, 2, 10),
                ModifiedDate = new DateTime(2024, 10, 18),
                Status = EnterpriseStatus.Active
            }
        };

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid()}.csv");

        try
        {
            // Act
            await _reportExportService.ExportToCsvAsync(testData.Cast<object>(), tempFilePath);

            // Assert
            Assert.True(File.Exists(tempFilePath), "CSV file should be created");

            var content = await File.ReadAllTextAsync(tempFilePath);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Should have header + 2 data rows
            Assert.Equal(3, lines.Length);

            // Check header
            var header = lines[0];
            Assert.Contains("Id", header);
            Assert.Contains("Name", header);
            Assert.Contains("Type", header);
            Assert.Contains("CurrentRate", header);
            Assert.Contains("MonthlyExpenses", header);
            Assert.Contains("CitizenCount", header);
            Assert.Contains("CreatedDate", header);
            Assert.Contains("ModifiedDate", header);
            Assert.Contains("Status", header);

            // Check first data row
            var firstRow = lines[1];
            Assert.Contains("Test Water Company", firstRow);
            Assert.Contains("Water", firstRow);
            Assert.Contains("25.5", firstRow); // CurrentRate
            Assert.Contains("15000", firstRow); // MonthlyExpenses
            Assert.Contains("1200", firstRow); // CitizenCount

            // Check second data row
            var secondRow = lines[2];
            Assert.Contains("Test Electric Company", secondRow);
            Assert.Contains("Electric", secondRow);
            Assert.Contains("45.75", secondRow); // CurrentRate
            Assert.Contains("25000", secondRow); // MonthlyExpenses
            Assert.Contains("800", secondRow); // CitizenCount
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public async Task ExportToCsvAsync_WithEmptyData_CreatesEmptyFile()
    {
        // Arrange
        var testData = new List<Enterprise>();
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"test_empty_export_{Guid.NewGuid()}.csv");

        try
        {
            // Act
            await _reportExportService.ExportToCsvAsync(testData.Cast<object>(), tempFilePath);

            // Assert
            Assert.True(File.Exists(tempFilePath), "CSV file should be created even for empty data");

            var content = await File.ReadAllTextAsync(tempFilePath);
            Assert.Equal(string.Empty, content); // Should be empty for no data
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public async Task ExportToCsvAsync_WithSpecialCharacters_ProperlyEscapesCsv()
    {
        // Arrange
        var testData = new List<Enterprise>
        {
            new Enterprise
            {
                Id = 1,
                Name = "Company, Inc.", // Contains comma
                Type = "Test \"Type\"", // Contains quotes
                CurrentRate = 10.00m,
                MonthlyExpenses = 5000m,
                CitizenCount = 100,
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                Status = EnterpriseStatus.Active
            }
        };

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"test_special_chars_{Guid.NewGuid()}.csv");

        try
        {
            // Act
            await _reportExportService.ExportToCsvAsync(testData.Cast<object>(), tempFilePath);

            // Assert
            Assert.True(File.Exists(tempFilePath), "CSV file should be created");

            var content = await File.ReadAllTextAsync(tempFilePath);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(2, lines.Length); // Header + 1 data row

            var dataRow = lines[1];
            // Should contain properly escaped values
            Assert.Contains("\"Company, Inc.\"", dataRow); // Comma escaped with quotes
            Assert.Contains("\"Test \"\"Type\"\"\"", dataRow); // Quotes escaped
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }
}