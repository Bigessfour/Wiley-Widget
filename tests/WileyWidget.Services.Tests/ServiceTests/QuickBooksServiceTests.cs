using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Xunit;
using Task = System.Threading.Tasks.Task;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Services.Tests.TestHelpers;
using WileyWidget.Services;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Services.Tests.ServiceTests
{
    public class QuickBooksServiceTests
    {
        [Fact(Skip = "Integration test requiring Intuit SDK and external QuickBooks sandbox; skipped in unit test runs.")]
        public async Task QuickBooksService_GetInvoicesAsync_ReturnsExpectedCount()
        {
            // Arrange
            var mockSettings = new Mock<WileyWidget.Services.ISettingsService>();
            // Provide default settings for tests so QBO token checks don't throw a NullReference
            var appSettings = new WileyWidget.Models.AppSettings
            {
                QboAccessToken = "test-token",
                QboRefreshToken = "test-refresh",
                QboTokenExpiry = System.DateTime.UtcNow.AddHours(1)
            };
            mockSettings.Setup(s => s.LoadAsync()).Returns(Task.CompletedTask);
            mockSettings.SetupGet(s => s.Current).Returns(appSettings);
            mockSettings.Setup(s => s.Current).Returns(appSettings);
            var mockSecretVault = new Mock<ISecretVaultService>();
            // Validate the mocked Current is present
            Assert.NotNull(mockSettings.Object.Current);
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();
            var mockHttpClient = new HttpClient();
            var mockServiceProvider = new Mock<IServiceProvider>();

            var service = new QuickBooksService(
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
            // Note: Actual invoice count will depend on QuickBooks sandbox data
            Assert.All(invoices, i => Assert.False(string.IsNullOrWhiteSpace(i.CustomerRef?.name)));
        }
    }
}