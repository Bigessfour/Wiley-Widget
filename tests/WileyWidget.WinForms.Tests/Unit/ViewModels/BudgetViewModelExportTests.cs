using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels;

public sealed class BudgetViewModelExportTests
{
    [Fact]
    public async Task ExportToPdfCommand_UsesBrandedFilteredBudgetDocument()
    {
        var logger = new Mock<ILogger<BudgetViewModel>>();
        var budgetRepository = new Mock<IBudgetRepository>();
        var exportService = new Mock<IReportExportService>();
        var enterpriseRepository = new Mock<IEnterpriseRepository>();
        ReportExportDocument? capturedDocument = null;

        exportService
            .Setup(service => service.ExportToPdfAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<object, string, CancellationToken>((data, _, _) => capturedDocument = data as ReportExportDocument)
            .Returns(Task.CompletedTask);

        var viewModel = new BudgetViewModel(logger.Object, budgetRepository.Object, exportService.Object, enterpriseRepository.Object)
        {
            SelectedFiscalYear = 2026,
            SelectedEntity = "Water Fund"
        };

        viewModel.BudgetEntries.Add(CreateEntry("410.1", "Water Sales", 125000m, 118500m, 2026, "Water Fund", "Utilities"));
        viewModel.BudgetEntries.Add(CreateEntry("510.1", "Plant Maintenance", 84200m, 102900m, 2026, "Water Fund", "Operations"));
        viewModel.SearchText = "Plant";

        await viewModel.ApplyFiltersCommand.ExecuteAsync(null);

        await viewModel.ExportToPdfCommand.ExecuteAsync(Path.Combine(Path.GetTempPath(), "budget-detail-report.pdf"));

        capturedDocument.Should().NotBeNull();
        capturedDocument!.Title.Should().Be("Budget Detail Report");
        capturedDocument.Metadata.Should().ContainKey("Scope").WhoseValue.Should().Be("Water Fund");
        capturedDocument.Metadata.Should().ContainKey("Rows Exported").WhoseValue.Should().Be("1");
        capturedDocument.Sections.Select(section => section.Title).Should().Contain(["Executive Summary", "Budget Entries", "Largest Variances"]);

        var entriesSection = capturedDocument.Sections.Single(section => section.Title == "Budget Entries");
        entriesSection.Rows.Should().HaveCount(1);
        entriesSection.Rows[0]["Account"].Should().Be("510.1");
        entriesSection.Rows[0]["Description"].Should().Be("Plant Maintenance");
        entriesSection.Rows[0]["Variance"].Should().Be("-$18,700.00");
        entriesSection.Rows[0]["Used"].Should().Be("122.2%");
    }

    [Fact]
    public async Task ExportToExcelCommand_UsesBrandedBudgetDocument()
    {
        var logger = new Mock<ILogger<BudgetViewModel>>();
        var budgetRepository = new Mock<IBudgetRepository>();
        var exportService = new Mock<IReportExportService>();
        var enterpriseRepository = new Mock<IEnterpriseRepository>();
        object? capturedPayload = null;
        var originalLogoPath = Environment.GetEnvironmentVariable("WILEYWIDGET_REPORT_LOGO_PATH");
        var temporaryLogoPath = Path.Combine(Path.GetTempPath(), $"wiley-widget-logo-{Guid.NewGuid():N}.jpg");

        File.WriteAllText(temporaryLogoPath, "logo");
        Environment.SetEnvironmentVariable("WILEYWIDGET_REPORT_LOGO_PATH", temporaryLogoPath);

        try
        {
            exportService
                .Setup(service => service.ExportToExcelAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<object, string, CancellationToken>((data, _, _) => capturedPayload = data)
                .Returns(Task.CompletedTask);

            var viewModel = new BudgetViewModel(logger.Object, budgetRepository.Object, exportService.Object, enterpriseRepository.Object)
            {
                SelectedFiscalYear = 2026
            };

            viewModel.BudgetEntries.Add(CreateEntry("405", "Sales Tax", 250000m, 243100m, 2026, "General Fund", "Finance"));

            await viewModel.ExportToExcelCommand.ExecuteAsync(Path.Combine(Path.GetTempPath(), "budget-detail-report.xlsx"));

            capturedPayload.Should().BeOfType<ReportExportDocument>();

            var document = (ReportExportDocument)capturedPayload!;
            document.Branding.LogoPath.Should().Be(temporaryLogoPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_REPORT_LOGO_PATH", originalLogoPath);
            if (File.Exists(temporaryLogoPath))
            {
                File.Delete(temporaryLogoPath);
            }
        }
    }

    private static BudgetEntry CreateEntry(
        string accountNumber,
        string description,
        decimal budgeted,
        decimal actual,
        int fiscalYear,
        string fundName,
        string departmentName)
    {
        return new BudgetEntry
        {
            AccountNumber = accountNumber,
            Description = description,
            BudgetedAmount = budgeted,
            ActualAmount = actual,
            FiscalYear = fiscalYear,
            DepartmentId = 1,
            Department = new Department { Name = departmentName },
            Fund = new Fund { Name = fundName },
            FundType = FundType.GeneralFund,
        };
    }
}
