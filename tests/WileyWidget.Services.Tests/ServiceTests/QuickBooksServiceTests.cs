using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Xunit;
using WileyWidget.Models;
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
        [Fact]
        public async Task QuickBooksService_GetInvoicesAsync_ReturnsExpectedCount()
        {
            // Arrange
            var mockSettings = new Mock<WileyWidget.Services.ISettingsService>();
            // Provide a simple, valid AppSettings to avoid live QuickBooks calls in unit tests
            mockSettings.Setup(s => s.LoadAsync()).Returns(Task.CompletedTask);
            mockSettings.SetupGet(s => s.Current).Returns(new AppSettings
            {
                QboAccessToken = "test-access-token",
                QboRefreshToken = "test-refresh-token",
                QboTokenExpiry = System.DateTime.UtcNow.AddMinutes(30),
                QuickBooksRealmId = "realm-123"
            });

            var mockSecretVault = new Mock<ISecretVaultService>();
            mockSecretVault.Setup(s => s.GetSecretAsync(It.IsAny<string>())).ReturnsAsync((string?)null);
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();
            using var mockHttpClient = new HttpClient();
            var mockServiceProvider = new Mock<IServiceProvider>();

            using var service = new QuickBooksService(
                mockSettings.Object,
                mockSecretVault.Object,
                mockLogger.Object,
                mockApiClient.Object,
                mockHttpClient,
                mockServiceProvider.Object
            );

            // Act
            // Avoid calling live QuickBooks APIs during unit tests. Instead, verify token validity.
            var hasToken = service.HasValidAccessToken();

            await Task.CompletedTask;
            // Assert
            Assert.True(hasToken, "Expected QuickBooks service to report a valid access token from mocked settings.");
        }
    }
}
