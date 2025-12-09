using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.Tests.Unit.ViewModels
{
    [Trait("Category", "Unit")]
    public class ReportsViewModelTests
    {
        [Fact]
        public async Task GenerateReport_SetsParametersAndCallsReportService()
        {
            // Arrange
            var mockReportSvc = new Mock<IBoldReportService>();
            mockReportSvc
                .Setup(x => x.LoadReportAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Returns(Task.CompletedTask);
            mockReportSvc
                .Setup(x => x.SetReportParametersAsync(It.IsAny<object>(), It.IsAny<Dictionary<string, object>>()))
                .Returns(Task.CompletedTask);

            var mockAudit = new Mock<IAuditService>();
            var mockLogger = new Mock<ILogger<ReportsViewModel>>();

            var vm = new ReportsViewModel(mockReportSvc.Object, mockLogger.Object, mockAudit.Object);
            vm.ReportViewer = new object();
            vm.FromDate = new DateTime(2024, 1, 1);
            vm.ToDate = new DateTime(2024, 6, 1);
            vm.SelectedReportType = "Budget Summary";

            // Act
            await vm.GenerateReportAsync();

            // Assert
            mockReportSvc.Verify(x => x.LoadReportAsync(vm.ReportViewer!, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()), Times.Once);
            mockReportSvc.Verify(x => x.SetReportParametersAsync(vm.ReportViewer!, It.Is<Dictionary<string, object>>(d =>
                d.ContainsKey("FromDate") && (DateTime)d["FromDate"] == vm.FromDate &&
                d.ContainsKey("ToDate") && (DateTime)d["ToDate"] == vm.ToDate &&
                d.ContainsKey("ReportTitle") && (string)d["ReportTitle"] == vm.SelectedReportType
            )), Times.Once);
        }

        [Fact]
        public async Task GenerateReport_InvalidDates_DoesNotCallLoad()
        {
            // Arrange
            var mockReportSvc = new Mock<IBoldReportService>();
            var mockAudit = new Mock<IAuditService>();
            var mockLogger = new Mock<ILogger<ReportsViewModel>>();

            var vm = new ReportsViewModel(mockReportSvc.Object, mockLogger.Object, mockAudit.Object);
            vm.ReportViewer = new object();
            vm.FromDate = new DateTime(2025, 1, 1);
            vm.ToDate = new DateTime(2024, 1, 1); // invalid range

            // Act
            await vm.GenerateReportAsync();

            // Assert
            mockReportSvc.Verify(x => x.LoadReportAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()), Times.Never);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
            Assert.Contains("earlier than or equal", vm.ErrorMessage);
        }

        [Fact]
        public async Task ExportToPdf_CallsAuditOnSuccess()
        {
            // Arrange
            var mockReportSvc = new Mock<IBoldReportService>();
            mockReportSvc
                .Setup(x => x.ExportToPdfAsync(It.IsAny<object>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var mockAudit = new Mock<IAuditService>();
            mockAudit
                .Setup(a => a.AuditAsync(It.IsAny<string>(), It.IsAny<object>()))
                .Returns(Task.CompletedTask);

            var mockLogger = new Mock<ILogger<ReportsViewModel>>();
            var vm = new ReportsViewModel(mockReportSvc.Object, mockLogger.Object, mockAudit.Object);
            vm.ReportViewer = new object();
            vm.SelectedReportType = "Budget Summary";

            // Act
            await vm.ExportToPdfAsync();

            // Assert
            mockReportSvc.Verify(x => x.ExportToPdfAsync(vm.ReportViewer!, It.IsAny<string>()), Times.Once);
            mockAudit.Verify(a => a.AuditAsync("ReportGenerated", It.Is<object>(o => o!.ToString()!.Contains("Budget Summary") || o!.ToString()!.Contains("BudgetSummary"))), Times.Once);
        }

        [Fact]
        public async Task ExportToExcel_CallsAuditOnFailure()
        {
            // Arrange
            var mockReportSvc = new Mock<IBoldReportService>();
            mockReportSvc
                .Setup(x => x.ExportToExcelAsync(It.IsAny<object>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Export failed"));

            var mockAudit = new Mock<IAuditService>();
            mockAudit
                .Setup(a => a.AuditAsync(It.IsAny<string>(), It.IsAny<object>()))
                .Returns(Task.CompletedTask);

            var mockLogger = new Mock<ILogger<ReportsViewModel>>();
            var vm = new ReportsViewModel(mockReportSvc.Object, mockLogger.Object, mockAudit.Object);
            vm.ReportViewer = new object();
            vm.SelectedReportType = "Budget Summary";

            // Act
            await vm.ExportToExcelAsync();

            // Assert
            mockReportSvc.Verify(x => x.ExportToExcelAsync(vm.ReportViewer!, It.IsAny<string>()), Times.Once);
            mockAudit.Verify(a => a.AuditAsync("ReportExportFailed", It.Is<object>(o => o!.ToString()!.Contains("Export failed"))), Times.Once);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
        }
    }
}
