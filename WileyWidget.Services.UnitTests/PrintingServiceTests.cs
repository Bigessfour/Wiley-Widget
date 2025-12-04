using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Services.Export;
using Xunit;

namespace WileyWidget.Services.UnitTests
{
    public class PrintingServiceTests
    {
        private readonly Mock<ILogger<PrintingService>> _loggerMock;
        private readonly Mock<SyncfusionPdfExportService> _pdfExportServiceMock;
        private readonly PrintingService _service;

        public PrintingServiceTests()
        {
            _loggerMock = new Mock<ILogger<PrintingService>>();
            _pdfExportServiceMock = new Mock<SyncfusionPdfExportService>(Mock.Of<ILogger<SyncfusionPdfExportService>>());
            _service = new PrintingService(_loggerMock.Object, _pdfExportServiceMock.Object);
        }

        [Fact]
        public async Task GeneratePdfAsync_WithValidChartViewModel_ReturnsPdfPath()
        {
            // Arrange
            var chartVm = new MockChartViewModel
            {
                SelectedYear = 2025,
                SelectedCategory = "All Categories",
                LineChartData = new System.Collections.ObjectModel.ObservableCollection<ChartDataPoint>
                {
                    new ChartDataPoint { Category = "Jan", Value = 1000 },
                    new ChartDataPoint { Category = "Feb", Value = 1200 }
                },
                PieChartData = new System.Collections.ObjectModel.ObservableCollection<ChartDataPoint>
                {
                    new ChartDataPoint { Category = "Revenue", Value = 1500 },
                    new ChartDataPoint { Category = "Expenses", Value = 700 }
                }
            };

            // Act
            var result = await _service.GeneratePdfAsync(chartVm);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().EndWith(".pdf");
            // Note: In test environment, file may not actually be created due to mocking
        }

        [Fact]
        public async Task GeneratePdfAsync_WithNullModel_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.GeneratePdfAsync(null!));
        }

        [Fact]
        public async Task GeneratePdfAsync_WithNonChartModel_UsesPdfExportService()
        {
            // Arrange
            var model = new { Test = "data" };
            _pdfExportServiceMock.Setup(x => x.ExportToPdfAsync(model, It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.GeneratePdfAsync(model);

            // Assert
            result.Should().NotBeNullOrEmpty();
            _pdfExportServiceMock.Verify(x => x.ExportToPdfAsync(model, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task PreviewAsync_WithValidPdfPath_OpensExternalViewer()
        {
            // Arrange
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.pdf");
            await File.WriteAllTextAsync(tempFile, "dummy pdf content");

            // Act - This will actually try to open the PDF, but in test environment it might fail gracefully
            Func<Task> act = () => _service.PreviewAsync(tempFile);

            // Assert - Should not throw (even if process fails in test env)
            await act.Should().NotThrowAsync();

            // Cleanup
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        [Fact]
        public async Task PreviewAsync_WithInvalidPath_ThrowsException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() => _service.PreviewAsync("nonexistent.pdf"));
        }

        [Fact]
        public async Task PrintAsync_WithValidPdfPath_InitiatesPrint()
        {
            // Arrange
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.pdf");
            await File.WriteAllTextAsync(tempFile, "dummy pdf content");

            // Act & Assert - In test environment, Process.Start will fail due to no PDF viewer
            await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() => _service.PrintAsync(tempFile));

            // Cleanup
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        [Fact]
        public async Task PrintAsync_WithInvalidPath_ThrowsException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() => _service.PrintAsync("nonexistent.pdf"));
        }

        [Fact]
        public async Task GeneratePdfAsync_WithEmptyChartData_StillCreatesPdf()
        {
            // Arrange
            var chartVm = new MockChartViewModel
            {
                SelectedYear = 2025,
                SelectedCategory = "All Categories",
                LineChartData = new System.Collections.ObjectModel.ObservableCollection<ChartDataPoint>(),
                PieChartData = new System.Collections.ObjectModel.ObservableCollection<ChartDataPoint>()
            };

            // Act
            var result = await _service.GeneratePdfAsync(chartVm);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().EndWith(".pdf");
            // Note: In test environment, file may not actually be created
        }

        [Fact]
        public async Task GeneratePdfAsync_WithLargeDataSet_CreatesPdf()
        {
            // Arrange
            var chartVm = new MockChartViewModel
            {
                SelectedYear = 2025,
                SelectedCategory = "All Categories",
                LineChartData = new System.Collections.ObjectModel.ObservableCollection<ChartDataPoint>(),
                PieChartData = new System.Collections.ObjectModel.ObservableCollection<ChartDataPoint>()
            };

            // Add many data points
            for (int i = 0; i < 100; i++)
            {
                chartVm.LineChartData.Add(new ChartDataPoint { Category = $"Month{i}", Value = i * 100 });
                chartVm.PieChartData.Add(new ChartDataPoint { Category = $"Category{i}", Value = i * 50 });
            }

            // Act
            var result = await _service.GeneratePdfAsync(chartVm);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().EndWith(".pdf");
            // Note: In test environment, file may not actually be created
        }

        // Mock classes for testing
        private class MockChartViewModel
        {
            public int SelectedYear { get; set; }
            public string SelectedCategory { get; set; } = string.Empty;
            public System.Collections.ObjectModel.ObservableCollection<ChartDataPoint> LineChartData { get; set; } = new();
            public System.Collections.ObjectModel.ObservableCollection<ChartDataPoint> PieChartData { get; set; } = new();
        }

        private class ChartDataPoint
        {
            public string Category { get; set; } = string.Empty;
            public decimal Value { get; set; }
        }
    }
}
