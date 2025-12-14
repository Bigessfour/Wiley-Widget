using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Xunit;
using Task = System.Threading.Tasks.Task;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Models;
using WileyWidget.Services.Tests.TestHelpers;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Business.Interfaces;
using QboInvoice = Intuit.Ipp.Data.Invoice;
using QboReferenceType = Intuit.Ipp.Data.ReferenceType;

namespace WileyWidget.Services.Tests.ServiceTests
{
    public class QuickBooksServiceTests
    {
        [Fact]
        public async Task QuickBooksService_GetInvoicesAsync_ReturnsExpectedCount()
        {
            // Arrange
            Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
            Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");
            Environment.SetEnvironmentVariable("QBO_REALM_ID", "test-realm-id");

            var mockSettings = new Mock<ISettingsService>();
            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();
            using var mockHttpClient = new HttpClient();
            var mockServiceProvider = new Mock<IServiceProvider>();

            var appSettings = new AppSettings
            {
                QboAccessToken = "access-token",
                QboRefreshToken = "refresh-token",
                QboTokenExpiry = DateTime.UtcNow.AddMinutes(15)
            };

            mockSettings.Setup(s => s.LoadAsync()).Returns(Task.CompletedTask);
            mockSettings.SetupGet(s => s.Current).Returns(appSettings);
            mockSettings.Setup(s => s.Save());

            mockSecretVault.Setup(v => v.GetSecretAsync(It.IsAny<string>())).ReturnsAsync((string?)null);

            var stubInvoices = new List<QboInvoice>
            {
                new() { CustomerRef = new QboReferenceType { name = "Test Customer" } }
            };
            mockApiClient.Setup(a => a.GetInvoicesAsync()).ReturnsAsync(stubInvoices);

            using var service = new QuickBooksService(
                mockSettings.Object,
                mockSecretVault.Object,
                mockLogger.Object,
                mockApiClient.Object,
                mockHttpClient,
                mockServiceProvider.Object
            );

            // Act
            var invoices = await service.GetInvoicesAsync();

            // Assert
            Assert.NotNull(invoices);
            Assert.All(invoices, i => Assert.False(string.IsNullOrWhiteSpace(i.CustomerRef?.name)));
        }
    }
}
