using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels;

public sealed class QuickBooksViewModelTests
{
    [Fact]
    public async Task CheckConnectionCommand_WhenServiceReturnsNull_StatusFallsBackToNotConnected()
    {
        var logger = new Mock<ILogger<QuickBooksViewModel>>();
        var quickBooksService = new Mock<IQuickBooksService>(MockBehavior.Strict);
        quickBooksService
            .Setup(service => service.GetConnectionStatusAsync(It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync((ConnectionStatus)null!);

        using var viewModel = new QuickBooksViewModel(logger.Object, quickBooksService.Object);

        await viewModel.CheckConnectionCommand.ExecuteAsync(null);

        viewModel.IsConnected.Should().BeFalse();
        viewModel.ConnectionStatus.Should().Be("Not connected");
        viewModel.ConnectionStatusMessage.Should().Be("Not connected");
        viewModel.StatusText.Should().Be("Not connected. Click 'Connect' to establish connection.");
        viewModel.ErrorMessage.Should().BeNull();

        quickBooksService.Verify(service => service.GetConnectionStatusAsync(It.IsAny<System.Threading.CancellationToken>()), Times.Once);
    }
}
