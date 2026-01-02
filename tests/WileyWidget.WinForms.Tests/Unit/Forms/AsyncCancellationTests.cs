using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    [Trait("Category", "Unit")]
    public class AsyncCancellationTests
    {
        public AsyncCancellationTests()
        {
            // Prevent unhandled exception dialogs that freeze tests
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        }
        [Fact]
        public async Task DashboardViewModel_LoadDashboardDataAsync_HandlesCancellation()
        {
            // Arrange
            var mockRepo = new Mock<IBudgetRepository>();
            mockRepo.Setup(r => r.GetBudgetSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TaskCanceledException());
            var mockAccountRepo = new Mock<IMunicipalAccountRepository>();
            var mockLogger = new Mock<ILogger<DashboardViewModel>>();
            using var vm = new DashboardViewModel(mockRepo.Object, mockAccountRepo.Object, mockLogger.Object);

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => vm.LoadCommand.ExecuteAsync(null));

            // Additional: Assert fallback state, e.g., Assert.False(vm.IsLoading);
        }

        [Fact]
        public async Task MainViewModel_InitializeAsync_PropagatesCancellation()
        {
            // Arrange
            var mockServiceProvider = new Mock<IServiceProvider>();
            var mockConfig = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<MainViewModel>>();
            var mockDashboardService = new Mock<IDashboardService>();
            var mockAILoggingService = new Mock<IAILoggingService>();
            using var vm = new MainViewModel(mockLogger.Object, mockDashboardService.Object, mockAILoggingService.Object);

            var cts = new CancellationTokenSource();
            try
            {
                cts.Cancel();

                // Act & Assert
                await Assert.ThrowsAsync<OperationCanceledException>(() => vm.InitializeAsync(cts.Token));
            }
            finally
            {
                cts.Dispose();
            }
        }
    }
}
