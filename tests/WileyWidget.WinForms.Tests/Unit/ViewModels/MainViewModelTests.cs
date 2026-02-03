using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels
{
    public class MainViewModelTests
    {
        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            var dashboardMock = new Mock<IDashboardService>();
            var aiLoggingMock = new Mock<IAILoggingService>();
            var qbMock = new Mock<IQuickBooksService>();
            var searchMock = new Mock<IGlobalSearchService>();

            var act = () => new MainViewModel(null!, dashboardMock.Object, aiLoggingMock.Object, qbMock.Object, searchMock.Object);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void ProcessDashboard_PopulatesPropertiesAndActivityItems()
        {
            var loggerMock = new Mock<ILogger<MainViewModel>>();
            var dashboardMock = new Mock<IDashboardService>();
            var aiLoggingMock = new Mock<IAILoggingService>();
            var qbMock = new Mock<IQuickBooksService>();
            var searchMock = new Mock<IGlobalSearchService>();

            var vm = new MainViewModel(loggerMock.Object, dashboardMock.Object, aiLoggingMock.Object, qbMock.Object, searchMock.Object, Mock.Of<IStatusProgressService>());

            var items = new List<DashboardItem>
            {
                new DashboardItem { Category = "budget", Value = "1000", Title = "Budget" },
                new DashboardItem { Category = "actual", Value = "800", Title = "Actual" },
                new DashboardItem { Category = "variance", Value = "200", Title = "Variance" },
                new DashboardItem { Category = "accounts", Value = "3", Title = "Accounts" },
                new DashboardItem { Category = "departments", Value = "5", Title = "Departments" },
                new DashboardItem { Category = "activity", Value = "Opened", Title = "Dashboard" }
            };

            vm.ProcessDashboard(items);

            vm.TotalBudget.Should().Be(1000m);
            vm.TotalActual.Should().Be(800m);
            vm.Variance.Should().Be(200m);
            vm.ActiveAccountCount.Should().Be(3);
            vm.TotalDepartments.Should().Be(5);
            vm.ActivityItems.Should().HaveCount(1);
            vm.LastUpdateTime.Should().NotBeNullOrWhiteSpace();
        }
    }
}
