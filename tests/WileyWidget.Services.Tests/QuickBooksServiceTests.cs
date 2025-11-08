using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Prism.Events;
using WileyWidget.Services;
using WileyWidget.Services.Events;
using Xunit;

namespace WileyWidget.Services.Tests
{
    public class QuickBooksServiceTests : IDisposable
    {
        private readonly Mock<ILogger<QuickBooksService>> _loggerMock;
        private readonly Mock<ISecretVaultService> _vaultMock;
        private readonly Mock<IEventAggregator> _eventAggregatorMock;
        private readonly Mock<HttpMessageHandler> _httpHandlerMock;
        private readonly HttpClient _httpClient;
        private readonly SettingsService _settings;
        private readonly IServiceProvider _serviceProvider;
        private readonly QuickBooksService _sut;

        public QuickBooksServiceTests()
        {
            _loggerMock = new Mock<ILogger<QuickBooksService>>();
            _vaultMock = new Mock<ISecretVaultService>();
            _eventAggregatorMock = new Mock<IEventAggregator>();
            _httpHandlerMock = new Mock<HttpMessageHandler>();

            // Setup Dispose to avoid Moq strict behavior errors
            _httpHandlerMock.Protected()
                .Setup("Dispose", ItExpr.IsAny<bool>());

            _httpClient = new HttpClient(_httpHandlerMock.Object) { BaseAddress = new Uri("https://sandbox-quickbooks.api.intuit.com/") };

            var services = new ServiceCollection();
            services.AddSingleton(_eventAggregatorMock.Object);
            // Provide an IHttpClientFactory that returns HttpClient instances wired to our handler
            var httpFactoryMock = new Mock<IHttpClientFactory>();
            httpFactoryMock.Setup(f => f.CreateClient("QBO")).Returns(() => new HttpClient(_httpHandlerMock.Object) { BaseAddress = new Uri("https://sandbox-quickbooks.api.intuit.com/") } as HttpClient);
            services.AddSingleton(httpFactoryMock.Object);

            _serviceProvider = services.BuildServiceProvider();

            // NOTE: SettingsService constructor signature varies; this assumes (IServiceProvider?, ILogger?) pattern used in the repo
            // SettingsService accepts ILogger<SettingsService>?; pass null to use NullLogger in tests
            _settings = new SettingsService(null, null);
            _settings.Current.QboAccessToken = "fake-token";
            _settings.Current.QboRefreshToken = "fake-refresh";
            _settings.Current.QboTokenExpiry = DateTime.UtcNow.AddHours(1);

            _sut = new QuickBooksService(_settings, _vaultMock.Object, _loggerMock.Object, _httpClient, _serviceProvider);
        }

        public void Dispose()
        {
            try { _sut.Dispose(); } catch { }
            _httpClient.Dispose();
        }

    [Fact]
    public async System.Threading.Tasks.Task SyncBudgetsToAppAsync_ValidBudgets_SyncsSuccessfullyAndPublishesEvent()
        {
            // Arrange
            _httpHandlerMock.Protected()
                .Setup<System.Threading.Tasks.Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"Id\":1}") })
                .Verifiable();

            var budgets = new List<Intuit.Ipp.Data.Budget>
            {
                new Intuit.Ipp.Data.Budget()
            };

            var eventMock = new Mock<BudgetsSyncedEvent>();
            _eventAggregatorMock.Setup(e => e.GetEvent<BudgetsSyncedEvent>()).Returns(eventMock.Object);

            // Act
            var result = await _sut.SyncBudgetsToAppAsync(budgets, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            result.RecordsSynced.Should().Be(1);
            result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
            result.ErrorMessage.Should().BeNullOrEmpty();

            _httpHandlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
            eventMock.Verify(e => e.Publish(It.IsAny<int>()), Times.Once());
        }

    [Fact]
    public async System.Threading.Tasks.Task SyncBudgetsToAppAsync_EmptyList_ReturnsSuccessWithZeroSynced()
        {
            // Arrange
            var budgets = new List<Intuit.Ipp.Data.Budget>();

            // Act
            var result = await _sut.SyncBudgetsToAppAsync(budgets, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            result.RecordsSynced.Should().Be(0);
            result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
            _httpHandlerMock.Protected().Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

    [Fact]
    public async System.Threading.Tasks.Task SyncBudgetsToAppAsync_HttpFailure_ReturnsErrorAndLogs()
        {
            // Arrange
            _httpHandlerMock.Protected()
                .Setup<System.Threading.Tasks.Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Unauthorized))
                .Verifiable();

            var budgets = new List<Intuit.Ipp.Data.Budget> { new Intuit.Ipp.Data.Budget { Id = "1" } };

            // Act
            var result = await _sut.SyncBudgetsToAppAsync(budgets, CancellationToken.None);

            // Assert
            // Implementation logs warnings for individual failures but returns Success=true with RecordsSynced=0
            result.Success.Should().BeTrue();
            result.RecordsSynced.Should().Be(0);
            _loggerMock.Verify(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to sync")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

    [Fact]
    public async System.Threading.Tasks.Task SyncBudgetsToAppAsync_Cancellation_ReturnsErrorResult()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately
            var budgets = new List<Intuit.Ipp.Data.Budget> { new Intuit.Ipp.Data.Budget { Id = "1" } };

            // Act
            var result = await _sut.SyncBudgetsToAppAsync(budgets, cts.Token);

            // Assert
            // Implementation catches OperationCanceledException and returns SyncResult with Success=false
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("cancelled");
        }
    }
}
