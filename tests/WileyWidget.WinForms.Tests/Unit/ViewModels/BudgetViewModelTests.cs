using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels;

public sealed class BudgetViewModelTests
{
    [Fact]
    public async Task ApplyFiltersCommand_DoesNotThrow_WhenSelectedFundTypeIsNull()
    {
        var budgetRepository = new Mock<IBudgetRepository>(MockBehavior.Strict);
        var reportExportService = new Mock<IReportExportService>(MockBehavior.Strict);
        var enterpriseRepository = new Mock<IEnterpriseRepository>(MockBehavior.Strict);
        var logger = new Mock<ILogger<BudgetViewModel>>();
        var eventBus = new AppEventBus();

        using var viewModel = new BudgetViewModel(
            logger.Object,
            budgetRepository.Object,
            reportExportService.Object,
            enterpriseRepository.Object,
            eventBus);

        viewModel.BudgetEntries = new ObservableCollection<BudgetEntry>
        {
            new()
            {
                AccountNumber = "101",
                Description = "General fund test entry",
                FiscalYear = 2026,
                StartPeriod = new DateTime(2026, 1, 1),
                EndPeriod = new DateTime(2026, 12, 31),
                FundType = FundType.GeneralFund,
                DepartmentId = 1,
                BudgetedAmount = 100m,
                ActualAmount = 50m
            }
        };

        viewModel.SelectedFundType = null;

        await viewModel.ApplyFiltersCommand.ExecuteAsync(null);

        viewModel.FilteredBudgetEntries.Should().HaveCount(1);
        viewModel.FilteredBudgetEntries[0].AccountNumber.Should().Be("101");
    }

    [Fact]
    public async Task ApplyFiltersCommand_DoesNotThrow_WhenNullableDepartmentAndVarianceFiltersAreNull()
    {
        var budgetRepository = new Mock<IBudgetRepository>(MockBehavior.Strict);
        var reportExportService = new Mock<IReportExportService>(MockBehavior.Strict);
        var enterpriseRepository = new Mock<IEnterpriseRepository>(MockBehavior.Strict);
        var logger = new Mock<ILogger<BudgetViewModel>>();
        var eventBus = new AppEventBus();

        using var viewModel = new BudgetViewModel(
            logger.Object,
            budgetRepository.Object,
            reportExportService.Object,
            enterpriseRepository.Object,
            eventBus);

        viewModel.BudgetEntries = new ObservableCollection<BudgetEntry>
        {
            new()
            {
                AccountNumber = "202",
                Description = "Nullable filter regression entry",
                FiscalYear = 2026,
                StartPeriod = new DateTime(2026, 1, 1),
                EndPeriod = new DateTime(2026, 12, 31),
                FundType = FundType.GeneralFund,
                DepartmentId = 1,
                BudgetedAmount = 100m,
                ActualAmount = 50m
            }
        };

        viewModel.SelectedDepartmentId = null;
        viewModel.VarianceThreshold = null;

        await viewModel.ApplyFiltersCommand.ExecuteAsync(null);

        viewModel.FilteredBudgetEntries.Should().HaveCount(1);
        viewModel.FilteredBudgetEntries[0].AccountNumber.Should().Be("202");
    }

    [Fact]
    public async Task QuickBooksDesktopImportCompletedEvent_TriggersBudgetReload()
    {
        var budgetLoadSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var enterpriseLoadSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var budgetRepository = new Mock<IBudgetRepository>(MockBehavior.Strict);
        budgetRepository
            .Setup(repository => repository.GetByFiscalYearAsync(2026, It.IsAny<CancellationToken>()))
            .Callback(() => budgetLoadSignal.TrySetResult(true))
            .Returns(Task.FromResult<IEnumerable<BudgetEntry>>(Array.Empty<BudgetEntry>()));

        var enterpriseRepository = new Mock<IEnterpriseRepository>(MockBehavior.Strict);
        enterpriseRepository
            .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .Callback(() => enterpriseLoadSignal.TrySetResult(true))
            .Returns(Task.FromResult<IEnumerable<Enterprise>>(Array.Empty<Enterprise>()));

        var reportExportService = new Mock<IReportExportService>(MockBehavior.Strict);
        var logger = new Mock<ILogger<BudgetViewModel>>();
        var eventBus = new AppEventBus();

        using var viewModel = new BudgetViewModel(
            logger.Object,
            budgetRepository.Object,
            reportExportService.Object,
            enterpriseRepository.Object,
            eventBus);

        viewModel.SelectedFiscalYear = 2026;

        eventBus.Publish(new QuickBooksDesktopImportCompletedEvent(
            "quickbooks-export.csv",
            "Payments",
            12,
            0,
            0,
            TimeSpan.FromSeconds(3)));

        var reloadTask = Task.WhenAll(budgetLoadSignal.Task, enterpriseLoadSignal.Task);
        var completedTask = await Task.WhenAny(reloadTask, Task.Delay(TimeSpan.FromSeconds(5)));

        completedTask.Should().Be(reloadTask);
        budgetRepository.Verify(repository => repository.GetByFiscalYearAsync(2026, It.IsAny<CancellationToken>()), Times.Once);
        enterpriseRepository.Verify(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
