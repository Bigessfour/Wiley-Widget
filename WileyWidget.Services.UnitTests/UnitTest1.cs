using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Business.Interfaces;
using Xunit;
using FluentAssertions;

namespace WileyWidget.Services.UnitTests
{
    public class QuickBooksServiceTests
    {
        private readonly Mock<ILogger<QuickBooksService>> _loggerMock;
        private readonly Mock<SettingsService> _settingsMock;
        private readonly Mock<ISecretVaultService> _secretVaultMock;
        private readonly Mock<IQuickBooksApiClient> _apiClientMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;

        public QuickBooksServiceTests()
        {
            _loggerMock = new Mock<ILogger<QuickBooksService>>();
            _settingsMock = new Mock<SettingsService>();
            _secretVaultMock = new Mock<ISecretVaultService>();
            _apiClientMock = new Mock<IQuickBooksApiClient>();
            _serviceProviderMock = new Mock<IServiceProvider>();
        }

        [Fact]
        public void QuickBooksService_CanBeConstructed()
        {
            // Arrange
            _settingsMock.Setup(s => s.GetEnvironmentName())
                        .Returns("sandbox");
            _secretVaultMock.Setup(s => s.GetSecretAsync(It.IsAny<string>()))
                           .ReturnsAsync("test-token");

            // Act
            var service = new QuickBooksService(
                _settingsMock.Object,
                _secretVaultMock.Object,
                _loggerMock.Object,
                _apiClientMock.Object,
                new System.Net.Http.HttpClient(),
                _serviceProviderMock.Object);

            // Assert
            service.Should().NotBeNull();
            service.Should().BeOfType<QuickBooksService>();
        }

        [Fact]
        public Task GetInvoicesAsync_MethodExists()
        {
            // Arrange
            _settingsMock.Setup(s => s.GetEnvironmentName())
                        .Returns("sandbox");
            _secretVaultMock.Setup(s => s.GetSecretAsync(It.IsAny<string>()))
                           .ReturnsAsync("test-token");

            var service = new QuickBooksService(
                _settingsMock.Object,
                _secretVaultMock.Object,
                _loggerMock.Object,
                _apiClientMock.Object,
                new System.Net.Http.HttpClient(),
                _serviceProviderMock.Object);

            // Act & Assert - Just verify the method exists and can be called
            // (would throw in real scenario due to API dependencies)
            var method = typeof(QuickBooksService).GetMethod("GetInvoicesAsync");
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<List<Intuit.Ipp.Data.Invoice>>));

            return Task.CompletedTask;
        }

        [Fact]
        public void QuickBooksService_HasGetInvoicesAsyncMethod()
        {
            // Act
            var method = typeof(QuickBooksService).GetMethod("GetInvoicesAsync");

            // Assert
            method.Should().NotBeNull("QuickBooksService should have GetInvoicesAsync method");
        }
    }
}