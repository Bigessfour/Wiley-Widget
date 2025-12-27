using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using WileyWidget.TestUtilities;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Integration.Tests.ExternalServices
{
    public class QuickBooksIntegrationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public async Task QuickBooksAuthService_RefreshToken_Succeeds_WhenTokenEndpointReturnsValidResponse()
        {
            // Arrange
            var settings = new TestHelpers.FakeSettingsService();
            settings.Current.QboRefreshToken = "initial-refresh";
            settings.Current.QboAccessToken = null;
            settings.Current.QboTokenExpiry = default;

            var secretVault = new TestHelpers.FakeSecretVaultService();

            // Provide env vars to satisfy initialization
            Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
            Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");
            Environment.SetEnvironmentVariable("QBO_REALM_ID", "realm-1");

            var tokenResponse = JsonSerializer.Serialize(new
            {
                access_token = "new-access-token",
                refresh_token = "new-refresh-token",
                expires_in = 3600,
                x_refresh_token_expires_in = 5184000
            });

            var httpClient = TestHelpers.CreateHttpClient((req, ct) =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(tokenResponse, Encoding.UTF8, "application/json")
                };
                return Task.FromResult(resp);
            });

            var logger = new LoggerFactory().CreateLogger("QuickBooksAuthTest");
            var svcProvider = new ServiceCollection().BuildServiceProvider();

            var auth = new QuickBooksAuthService(settings, secretVault, logger, httpClient, svcProvider);

            // Act
            await auth.RefreshTokenAsync();

            // Assert
            settings.Current.QboAccessToken.Should().Be("new-access-token");
            settings.Current.QboRefreshToken.Should().Be("new-refresh-token");
            settings.Current.QboTokenExpiry.Should().BeAfter(DateTime.UtcNow.AddSeconds(-1));

            // Cleanup env
            Environment.SetEnvironmentVariable("QBO_CLIENT_ID", null);
            Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", null);
            Environment.SetEnvironmentVariable("QBO_REALM_ID", null);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task QuickBooksAuthService_RefreshToken_ThrowsOnBadRequest()
        {
            // Arrange
            var settings = new TestHelpers.FakeSettingsService();
            settings.Current.QboRefreshToken = "bad-refresh";

            Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
            Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

            var httpClient = TestHelpers.CreateHttpClient((req, ct) =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":\"invalid_grant\"}", Encoding.UTF8, "application/json")
                };
                return Task.FromResult(resp);
            });

            var auth = new QuickBooksAuthService(settings, new TestHelpers.FakeSecretVaultService(), new LoggerFactory().CreateLogger("QBAuth"), httpClient, new ServiceCollection().BuildServiceProvider());

            // Act / Assert
            await Assert.ThrowsAsync<QuickBooksAuthException>(() => auth.RefreshTokenAsync());

            // Cleanup env
            Environment.SetEnvironmentVariable("QBO_CLIENT_ID", null);
            Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", null);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task QuickBooksService_TestConnectionAsync_UsesInjectedDataService()
        {
            // Arrange
            var settings = new TestHelpers.FakeSettingsService();
            var apiClient = Moq.Mock.Of<IQuickBooksApiClient>();
            var logger = new LoggerFactory().CreateLogger<QuickBooksService>();
            var svcProvider = new ServiceCollection().BuildServiceProvider();
            var injected = new TestHelpers.FakeQuickBooksDataService();
            var txnRepo = new Moq.Mock<WileyWidget.Business.Interfaces.ITransactionRepository>().Object;

            var qbService = new QuickBooksService(settings, new TestHelpers.FakeSecretVaultService(), logger, apiClient, new HttpClient(), svcProvider, txnRepo, injected);

            // Act
            var ok = await qbService.TestConnectionAsync();

            // Assert
            ok.Should().BeTrue();
        }
    }
}
