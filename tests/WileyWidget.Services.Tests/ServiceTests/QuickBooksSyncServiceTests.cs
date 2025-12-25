using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Tests.TestHelpers;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;


namespace WileyWidget.Services.Tests.ServiceTests
{
    public class QuickBooksSyncServiceTests
    {
        [Fact]
        public async Task SyncBudgetsToAppAsync_PartialFailure_ReturnsPartialResult()
        {
            // Arrange
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "valid-token", QboTokenExpiry = DateTime.UtcNow.AddHours(1) };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mockSettings.Setup(x => x.Save()).Verifiable();

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            int calls = 0;
            var handler = new FakeHttpMessageHandler((req, ct) =>
            {
                calls++;
                if (calls == 1)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}")
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("error")
                });
            });

            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            using var service = new QuickBooksService(mockSettings.Object, mockSecretVault.Object, mockLogger.Object, mockApiClient.Object, httpClient, new Mock<IServiceProvider>().Object);

            var budgets = new List<QuickBooksBudget>
            {
                new QuickBooksBudget { QuickBooksId = "B1", Name = "Budget 1", FiscalYear = 2024 },
                new QuickBooksBudget { QuickBooksId = "B2", Name = "Budget 2", FiscalYear = 2024 }
            };

            // Act
            var result = await service.SyncBudgetsToAppAsync(budgets, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(1, result.RecordsSynced);
            Assert.Equal("Partial HTTP error", result.ErrorMessage);
        }

        [Fact]
        public async Task SyncBudgetsToAppAsync_Cancellation_ReturnsCancelled()
        {
            // Arrange
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "valid-token", QboTokenExpiry = DateTime.UtcNow.AddHours(1) };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var handler = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse("{}"));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            using var service = new QuickBooksService(mockSettings.Object, mockSecretVault.Object, mockLogger.Object, mockApiClient.Object, httpClient, new Mock<IServiceProvider>().Object);

            var budgets = new List<QuickBooksBudget>
            {
                new QuickBooksBudget { QuickBooksId = "B1", Name = "Budget 1", FiscalYear = 2024 }
            };

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // cancel immediately

            // Act
            var result = await service.SyncBudgetsToAppAsync(budgets, cts.Token);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Operation cancelled", result.ErrorMessage);
        }

        [Fact]
        public async Task SyncVendorsToAppAsync_AllSuccess_ReturnsSuccess()
        {
            // Arrange
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "valid-token", QboTokenExpiry = DateTime.UtcNow.AddHours(1), QuickBooksRealmId = "realm" };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            int posts = 0;
            var handler = new FakeHttpMessageHandler((req, ct) =>
            {
                posts++;
                Assert.Equal(HttpMethod.Post, req.Method);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
            });

            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            using var service = new QuickBooksService(mockSettings.Object, mockSecretVault.Object, mockLogger.Object, mockApiClient.Object, httpClient, new Mock<IServiceProvider>().Object);

            var vendors = new List<Intuit.Ipp.Data.Vendor>
            {
                new Intuit.Ipp.Data.Vendor { Id = "V1" },
                new Intuit.Ipp.Data.Vendor { Id = "V2" }
            };

            // Act
            var result = await service.SyncVendorsToAppAsync(vendors, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.RecordsSynced);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task SyncVendorsToAppAsync_PartialFailure_ReturnsPartialResult()
        {
            // Arrange
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "valid-token", QboTokenExpiry = DateTime.UtcNow.AddHours(1), QuickBooksRealmId = "realm" };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            int attempts = 0;
            var handler = new FakeHttpMessageHandler((req, ct) =>
            {
                attempts++;
                if (attempts == 1)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("fail") });
            });

            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            using var service = new QuickBooksService(mockSettings.Object, mockSecretVault.Object, mockLogger.Object, mockApiClient.Object, httpClient, new Mock<IServiceProvider>().Object);

            var vendors = new List<Intuit.Ipp.Data.Vendor>
            {
                new Intuit.Ipp.Data.Vendor { Id = "V1" },
                new Intuit.Ipp.Data.Vendor { Id = "V2" }
            };

            // Act
            var result = await service.SyncVendorsToAppAsync(vendors, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(1, result.RecordsSynced);
            Assert.Equal("One or more vendors failed to sync", result.ErrorMessage);
        }

        [Fact]
        public async Task SyncVendorsToAppAsync_Cancellation_ReturnsCancelled()
        {
            // Arrange
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "valid-token", QboTokenExpiry = DateTime.UtcNow.AddHours(1), QuickBooksRealmId = "realm" };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var handler = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse("{}"));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            using var service = new QuickBooksService(mockSettings.Object, mockSecretVault.Object, mockLogger.Object, mockApiClient.Object, httpClient, new Mock<IServiceProvider>().Object);

            var vendors = new List<Intuit.Ipp.Data.Vendor> { new Intuit.Ipp.Data.Vendor { Id = "V1" } };

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var result = await service.SyncVendorsToAppAsync(vendors, cts.Token);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Operation cancelled", result.ErrorMessage);
        }
    }
}
