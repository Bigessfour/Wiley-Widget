using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels
{
    [Trait("Category", "Unit")]
    public class ReportsViewModelTests
    {
        [Fact]
        public async Task GenerateReport_SetsParametersAndCallsReportService()
        {
            // Arrange
            var mockReportSvc = new Mock<IReportService>();
            mockReportSvc
                .Setup(x => x.LoadReportAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var mockAudit = new Mock<IAuditService>();
            var mockLogger = new Mock<ILogger<ReportsViewModel>>();
            var fakeViewer = new object();

            var vm = new ReportsViewModel(mockReportSvc.Object, mockLogger.Object, mockAudit.Object);
            vm.ReportViewer = fakeViewer;
            vm.FromDate = new DateTime(2024, 1, 1);
            vm.ToDate = new DateTime(2024, 6, 1);
            vm.SelectedReportType = "Budget Summary";

            // Act
            await vm.GenerateReportAsync();

            // Assert
            mockReportSvc.Verify(x => x.LoadReportAsync(vm.ReportViewer!, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()), Times.Once);
            // NOTE: Parameter validation still happens internally via ValidateParameters()
            // but SetReportParametersAsync is no longer called (BoldReports WPF limitation)
        }

        [Fact]
        public async Task GenerateReport_InvalidDates_DoesNotCallLoad()
        {
            // Arrange
            var mockReportSvc = new Mock<IReportService>();
            var mockAudit = new Mock<IAuditService>();
            var mockLogger = new Mock<ILogger<ReportsViewModel>>();
            var fakeViewer = new object();

            var vm = new ReportsViewModel(mockReportSvc.Object, mockLogger.Object, mockAudit.Object);
            vm.ReportViewer = fakeViewer;
            vm.FromDate = new DateTime(2025, 1, 1);
            vm.ToDate = new DateTime(2024, 1, 1); // invalid range

            // Act
            await vm.GenerateReportAsync();

            // Assert
            mockReportSvc.Verify(x => x.LoadReportAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
            Assert.Contains("earlier than or equal", vm.ErrorMessage, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ExportToPdf_ShowsNotSupportedMessage()
        {
            // Arrange - Export methods are now supported in FastReport Open Source
            var mockReportSvc = new Mock<IReportService>();
            mockReportSvc
                .Setup(x => x.ExportToPdfAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var mockAudit = new Mock<IAuditService>();
            var mockLogger = new Mock<ILogger<ReportsViewModel>>();
            var fakeViewer = new object();
            var vm = new ReportsViewModel(mockReportSvc.Object, mockLogger.Object, mockAudit.Object);
            vm.ReportViewer = fakeViewer;
            vm.SelectedReportType = "Budget Summary";

            // Act
            await vm.ExportToPdfAsync();

            // Assert - Should call export service successfully
            mockReportSvc.Verify(x => x.ExportToPdfAsync(fakeViewer, It.IsAny<string>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.True(string.IsNullOrEmpty(vm.ErrorMessage));
        }

        [Fact]
        public async Task ExportToExcel_CallsExportService()
        {
            // Arrange - Export methods are now supported in FastReport Open Source
            var mockReportSvc = new Mock<IReportService>();
            mockReportSvc
                .Setup(x => x.ExportToExcelAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var mockAudit = new Mock<IAuditService>();
            var mockLogger = new Mock<ILogger<ReportsViewModel>>();
            var fakeViewer = new object();
            var vm = new ReportsViewModel(mockReportSvc.Object, mockLogger.Object, mockAudit.Object);
            vm.ReportViewer = fakeViewer;
            vm.SelectedReportType = "Budget Summary";

            // Act
            await vm.ExportToExcelAsync();

            // Assert - Should call export service successfully
            mockReportSvc.Verify(x => x.ExportToExcelAsync(fakeViewer, It.IsAny<string>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.True(string.IsNullOrEmpty(vm.ErrorMessage));
        }
    }
}
