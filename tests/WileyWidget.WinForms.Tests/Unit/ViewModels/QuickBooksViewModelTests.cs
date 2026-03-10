using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels;

public sealed class QuickBooksViewModelTests
{
    [Fact]
    public async Task CheckConnectionCommand_WhenServiceReturnsNullStatus_FallsBackToNotConnectedState()
    {
        var quickBooksService = new Mock<IQuickBooksService>();
        quickBooksService
            .Setup(service => service.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) => Task.FromResult<ConnectionStatus>(null!));

        var viewModel = new QuickBooksViewModel(
            NullLogger<QuickBooksViewModel>.Instance,
            quickBooksService.Object);

        await viewModel.CheckConnectionCommand.ExecuteAsync(null);

        viewModel.IsConnected.Should().BeFalse();
        viewModel.ConnectionStatus.Should().Be("Not connected");
        viewModel.ConnectionStatusMessage.Should().Be("Not connected");
        viewModel.ErrorMessage.Should().BeNull();
        viewModel.StatusText.Should().Be("Not connected. Click 'Connect' to establish connection.");
    }

    [Fact]
    public async Task ConnectCommand_WhenStoredAuthorizationIsInvalid_ShowsReauthorizeGuidance()
    {
        var quickBooksService = new Mock<IQuickBooksService>();
        quickBooksService
            .Setup(service => service.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        quickBooksService
            .Setup(service => service.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatus
            {
                IsConnected = false,
                StatusMessage = "Error: Token refresh failed: Token refresh failed. Please re-authorize the application."
            });
        quickBooksService
            .Setup(service => service.RunDiagnosticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuickBooksDiagnosticsResult(
                Environment: "sandbox",
                RedirectUri: "http://localhost:5000/callback/",
                RedirectUriValid: true,
                RedirectUriGuidance: "OK",
                HasClientId: true,
                HasClientSecret: true,
                HasRealmId: true,
                UrlAclRegistered: true,
                UrlAclUrl: "http://localhost:5000/callback/",
                HasValidToken: false,
                TokenExpiry: "expired"));

        var viewModel = new QuickBooksViewModel(
            NullLogger<QuickBooksViewModel>.Instance,
            quickBooksService.Object);

        await viewModel.ConnectCommand.ExecuteAsync(null);

        viewModel.StatusText.Should().Contain("Re-authorize");
        viewModel.ErrorMessage.Should().Contain("Re-authorize");
        viewModel.SyncHistory.Should().ContainSingle(record =>
            record.Operation == "Connect" &&
            record.Status == "Failed" &&
            record.Message.Contains("Re-authorize", System.StringComparison.OrdinalIgnoreCase));
    }
}
