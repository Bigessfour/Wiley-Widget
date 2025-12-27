using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels
{
    public class QuickBooksViewModelTests
    {
        [Fact]
        public async Task ConnectCommand_CallsServiceAndUpdatesState()
        {
            var svc = new Mock<IQuickBooksService>();
            svc.Setup(s => s.ConnectAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            svc.Setup(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ConnectionStatus
            {
                IsConnected = true,
                CompanyName = "TestCo",
                LastSyncTime = "Now"
            });

            var logger = new Mock<ILogger<QuickBooksViewModel>>();
            var vm = new QuickBooksViewModel(logger.Object, svc.Object);

            await vm.ConnectCommand.ExecuteAsync(null);

            svc.Verify(s => s.ConnectAsync(It.IsAny<CancellationToken>()), Times.Once);
            svc.Verify(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
            vm.IsConnected.Should().BeTrue();
            vm.ConnectionLabel.Should().Contain("Connected to");
        }

        [Fact]
        public async Task DisconnectCommand_CallsServiceAndUpdatesState()
        {
            var svc = new Mock<IQuickBooksService>();
            svc.Setup(s => s.DisconnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            svc.Setup(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ConnectionStatus
            {
                IsConnected = false,
                CompanyName = null,
                LastSyncTime = null
            });

            var logger = new Mock<ILogger<QuickBooksViewModel>>();
            var vm = new QuickBooksViewModel(logger.Object, svc.Object);
            vm.IsConnected = true; // simulate connected state

            await vm.DisconnectCommand.ExecuteAsync(null);

            svc.Verify(s => s.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Once);
            svc.Verify(s => s.GetConnectionStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
            vm.IsConnected.Should().BeFalse();
        }

        [Fact]
        public async Task SyncCommand_Success_UpdatesLogs()
        {
            var svc = new Mock<IQuickBooksService>();
            svc.Setup(s => s.SyncDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SyncResult
            {
                Success = true,
                RecordsSynced = 42,
                Duration = TimeSpan.FromSeconds(1)
            });

            var logger = new Mock<ILogger<QuickBooksViewModel>>();
            var vm = new QuickBooksViewModel(logger.Object, svc.Object);
            vm.IsConnected = true;

            await vm.SyncCommand.ExecuteAsync(null);

            svc.Verify(s => s.SyncDataAsync(It.IsAny<CancellationToken>()), Times.Once);
            vm.Logs.Should().Contain(l => l.Contains("Successfully synced 42"));
        }

        [Fact]
        public async Task ImportAccountsCommand_Success_UpdatesLogs()
        {
            var svc = new Mock<IQuickBooksService>();
            svc.Setup(s => s.ImportChartOfAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ImportResult
            {
                Success = true,
                AccountsImported = 10,
                AccountsUpdated = 2,
                AccountsSkipped = 1,
                Duration = TimeSpan.FromSeconds(1)
            });

            var logger = new Mock<ILogger<QuickBooksViewModel>>();
            var vm = new QuickBooksViewModel(logger.Object, svc.Object);
            vm.IsConnected = true;

            await vm.ImportAccountsCommand.ExecuteAsync(null);

            svc.Verify(s => s.ImportChartOfAccountsAsync(It.IsAny<CancellationToken>()), Times.Once);
            vm.Logs.Should().Contain(l => l.Contains("Successfully imported 10"));
        }
    }
}
