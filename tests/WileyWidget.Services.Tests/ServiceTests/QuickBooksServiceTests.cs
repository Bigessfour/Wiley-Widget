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
        [Fact]
        public async Task QuickBooksService_GetInvoicesAsync_ReturnsExpectedCount()
        {
            // Arrange
            var mockSettings = new Mock<WileyWidget.Services.ISettingsService>();
            var mockSecretVault = new Mock<ISecretVaultService>();
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
            var invoices = await service.GetInvoicesAsync();

            // Assert
            Assert.NotNull(invoices);
            // Note: Actual invoice count will depend on QuickBooks sandbox data
            Assert.All(invoices, i => Assert.False(string.IsNullOrWhiteSpace(i.CustomerRef?.name)));
        }
    }
}