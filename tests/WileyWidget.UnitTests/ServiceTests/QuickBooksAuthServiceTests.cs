using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Tests.TestHelpers;

namespace WileyWidget.Services.Tests.ServiceTests
{
    public class QuickBooksAuthServiceTests
    {
        [Fact]
        public async Task ConcurrentRefresh_OnlyOneHttpCall_PerformsSingleRefresh()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new Models.AppSettings { QboAccessToken = null, QboRefreshToken = "refresh-token", QboTokenExpiry = default };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mockSettings.Setup(x => x.Save()).Verifiable();

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger>();
            var mockServiceProvider = new Mock<IServiceProvider>();

            int callCount = 0;
            using FakeHttpMessageHandler handler = new FakeHttpMessageHandler(async (req, ct) =>
            {
                Interlocked.Increment(ref callCount);
                // Slow down to allow concurrency
                await Task.Delay(100, ct);
                string json = "{\"access_token\":\"new-access\",\"refresh_token\":\"new-refresh\",\"expires_in\":3600}";
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            });

            using var httpClient = new HttpClient(handler);
            httpClient.BaseAddress = new Uri("http://localhost/");

            using var authService = new QuickBooksAuthService(
                mockSettings.Object,
                mockSecretVault.Object,
                mockLogger.Object,
                httpClient,
                mockServiceProvider.Object
            );

            var prevClientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
            var prevClientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
            try
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

                var tasks = new List<Task>();
                for (int i = 0; i < 10; i++)
                {
                    tasks.Add(Task.Run(async () => await authService.RefreshTokenIfNeededAsync()));
                }

                await Task.WhenAll(tasks);

                Assert.Equal(1, callCount);
                Assert.Equal("new-access", mockSettings.Object.Current.QboAccessToken);
                Assert.Equal("new-refresh", mockSettings.Object.Current.QboRefreshToken);
                Assert.True(mockSettings.Object.Current.QboTokenExpiry > DateTime.UtcNow);
            }
            finally
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", prevClientId);
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", prevClientSecret);
            }
        }

        [Fact]
        public async Task RefreshTokenIfNeededAsync_RefreshesWhenTokenNearExpiry()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new Models.AppSettings { QboAccessToken = "old", QboRefreshToken = "refresh-token", QboTokenExpiry = DateTime.UtcNow.AddSeconds(30) };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mockSettings.Setup(x => x.Save()).Verifiable();

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger>();
            var mockServiceProvider = new Mock<IServiceProvider>();

            var json = "{\"access_token\":\"refreshed\",\"refresh_token\":\"refreshed-refresh\",\"expires_in\":3600}";
            var handler = new FakeHttpMessageHandler(WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler.JsonResponse(json));

            using var httpClient = new HttpClient(handler);
            httpClient.BaseAddress = new Uri("http://localhost/");

            using var authService = new QuickBooksAuthService(
                mockSettings.Object,
                mockSecretVault.Object,
                mockLogger.Object,
                httpClient,
                mockServiceProvider.Object
            );

            var prevClientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
            var prevClientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
            try
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

                await authService.RefreshTokenIfNeededAsync();

                Assert.Equal("refreshed", mockSettings.Object.Current.QboAccessToken);
                Assert.Equal("refreshed-refresh", mockSettings.Object.Current.QboRefreshToken);
            }
            finally
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", prevClientId);
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", prevClientSecret);
            }
        }

        [Fact]
        public async Task EnsureInitialized_ThrowsWhenClientSecretMissing()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new Models.AppSettings { QboRefreshToken = "refresh", QboAccessToken = null, QboTokenExpiry = default };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger>();
            var mockServiceProvider = new Mock<IServiceProvider>();

            using var handler = new FakeHttpMessageHandler((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }));
            using var httpClient = new HttpClient(handler);

            using var authService = new QuickBooksAuthService(
                mockSettings.Object,
                mockSecretVault.Object,
                mockLogger.Object,
                httpClient,
                mockServiceProvider.Object
            );

            var prevClientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
            var prevClientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
            try
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", null);

                await Assert.ThrowsAsync<QuickBooksAuthException>(async () => await authService.RefreshTokenIfNeededAsync());
            }
            finally
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", prevClientId);
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", prevClientSecret);
            }
        }
    }
}
