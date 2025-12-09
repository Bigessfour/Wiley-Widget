using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Data;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WileyWidget.WinForms.Tests
{
    public class CancellationHandlingTests
    {
        [Fact]
        public async Task ChartViewModel_LoadChartDataAsync_Canceled_ReturnsGracefully()
        {
            // Arrange
            var logger = new Mock<ILogger<ChartViewModel>>();
            var mockChartService = new Mock<IChartService>();
            var mockDashboardService = new Mock<IMainDashboardService>();
            var vm = new ChartViewModel(logger.Object, mockChartService.Object, mockDashboardService.Object);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cts.Cancel(); // token is already canceled

            // Act
            var act = async () => await vm.LoadChartsAsync(null, null, cts.Token);

            // Assert - should complete without throwing and collections remain empty
            await act.Should().NotThrowAsync();
            vm.LineChartData.Should().BeEmpty();
            vm.PieChartData.Should().BeEmpty();
        }

        [Fact]
        public async Task AccountsViewModel_LoadAccountsAsync_Canceled_DoesNotThrowAndLeavesState()
        {
            // Arrange - empty in-memory database with AccountService
            var services = new ServiceCollection();
            var dbName = $"CancelTest_{System.Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
            var provider = services.BuildServiceProvider();

            var scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(provider);
            var logger = new Mock<ILogger<AccountsViewModel>>();
            var mapper = new WileyWidget.Business.Services.AccountMapper();
            var accountServiceLogger = Mock.Of<ILogger<AccountService>>();
            var accountService = new AccountService(accountServiceLogger, scopeFactory, mapper);
            var vm = new AccountsViewModel(logger.Object, accountService, mapper);

            using (var scope = provider.CreateScope())
            {
                var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDbContext>(scope.ServiceProvider);
                // ensure db is created
                await db.Database.EnsureCreatedAsync();

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                cts.Cancel();

                // Act
                var act = async () => await vm.LoadAccountsCommand.ExecuteAsync(cts.Token);

                // Assert - ensure cancellation doesn't throw an unhandled exception and viewmodel finishes the operation
                await act.Should().NotThrowAsync();
                vm.IsLoading.Should().BeFalse();
            }
        }
    }
}
