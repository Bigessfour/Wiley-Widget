using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Xunit;
using FluentAssertions;

namespace WileyWidget.Services.UnitTests
{
    public class QuickBooksServiceTests
    {
        private readonly Mock<ILogger<QuickBooksService>> _loggerMock;
        private readonly Mock<ISettingsService> _settingsMock;
        private readonly Mock<ISecretVaultService> _secretVaultMock;
        private readonly Mock<IQuickBooksApiClient> _apiClientMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;

        public QuickBooksServiceTests()
        {
            _loggerMock = new Mock<ILogger<QuickBooksService>>();
            _settingsMock = new Mock<ISettingsService>();
            _secretVaultMock = new Mock<ISecretVaultService>();
            _apiClientMock = new Mock<IQuickBooksApiClient>();
            _serviceProviderMock = new Mock<IServiceProvider>();
        }

        [Fact]
        public void QuickBooksService_CanBeConstructed()
        {
            // Arrange
            var configBuilder = new Microsoft.Extensions.Configuration.ConfigurationBuilder();
            var config = configBuilder.Build();
            var settingsService = new SettingsService(config, new Mock<ILogger<SettingsService>>().Object);
            _secretVaultMock.Setup(s => s.GetSecretAsync(It.IsAny<string>()))
                           .ReturnsAsync("test-token");

            // Act
            var service = new QuickBooksService(
                settingsService,
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
        public void GetInvoicesAsync_MethodExists()
        {
            // Act - Verify the method exists via reflection
            var method = typeof(QuickBooksService).GetMethod("GetInvoicesAsync");
            
            // Assert
            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(Task<List<Intuit.Ipp.Data.Invoice>>));
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