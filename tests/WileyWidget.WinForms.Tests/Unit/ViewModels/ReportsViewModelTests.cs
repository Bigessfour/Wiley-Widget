using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels;

public sealed class ReportsViewModelTests
{
    [Fact]
    public void Constructor_FindsBundledReportTemplates_WhenRepoReportsFolderExists()
    {
        var reportService = new Mock<IReportService>(MockBehavior.Strict);
        var auditService = new Mock<IAuditService>(MockBehavior.Strict);
        var budgetRepository = new Mock<IBudgetRepository>(MockBehavior.Strict);

        using var viewModel = new ReportsViewModel(
            reportService.Object,
            NullLogger<ReportsViewModel>.Instance,
            auditService.Object,
            budgetRepository.Object);

        viewModel.ReportTemplateDisplayNames.Should().Contain(name => name.Contains("Budget Comparison"));
        viewModel.GetReportPathIfExists().Should().NotBeNull();
    }
}
