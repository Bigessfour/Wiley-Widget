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
using WileyWidget.Models;

namespace WileyWidget.Services.Tests.ServiceTests
{
    public class QuickBooksServiceTests
    {
        [Fact]
        public void QuickBooksService_HasNoValidAccessToken_WhenSettingsEmpty()
        {
            // Arrange: ensure settings loader returns an AppSettings instance (non-null) but no tokens
            var mockSettings = new Mock<WileyWidget.Services.ISettingsService>();
            mockSettings.Setup(s => s.LoadAsync()).Returns(Task.CompletedTask);
            mockSettings.SetupGet(s => s.Current).Returns(new AppSettings
            {
                QboAccessToken = null,
                QboRefreshToken = null,
                QboTokenExpiry = default
            });

            var mockSecretVault = new Mock<ISecretVaultService>();
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

            // Act & Assert
            Assert.False(service.HasValidAccessToken());
        }
    }
}