using System;
using System.Net;
using System.Net.Http;
using System.Text;
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
    public class QuickBooksAuthServiceTests
    {
        [Fact]
        public async Task RefreshTokenAsync_Success_UpdatesSettingsAndSaves()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboRefreshToken = "old-refresh", QboAccessToken = null, QboTokenExpiry = default };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mockSettings.Setup(x => x.Save()).Verifiable();

            var mockSecretVault = new Mock<ISecretVaultService>();
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-ID")).ReturnsAsync("test-client-id");
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-SECRET")).ReturnsAsync("test-client-secret");

            var mockLogger = new Mock<ILogger>();
            var json = "{\"access_token\":\"new-access\",\"refresh_token\":\"new-refresh\",\"expires_in\":3600}";
            var handler = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse(json));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

            using var auth = new QuickBooksAuthService(mockSettings.Object, mockSecretVault.Object, mockLogger.Object, httpClient, new Mock<IServiceProvider>().Object);

            await auth.RefreshTokenAsync();

            Assert.Equal("new-access", mockSettings.Object.Current.QboAccessToken);
            Assert.Equal("new-refresh", mockSettings.Object.Current.QboRefreshToken);
            Assert.True(mockSettings.Object.Current.QboTokenExpiry > DateTime.UtcNow);
            mockSettings.Verify(x => x.Save(), Times.AtLeastOnce());
        }

        [Fact]
        public async Task RefreshTokenAsync_RetriesOnTransientFailure_SucceedsEventually()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboRefreshToken = "old-refresh", QboAccessToken = null, QboTokenExpiry = default };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mockSettings.Setup(x => x.Save()).Verifiable();

            var mockSecretVault = new Mock<ISecretVaultService>();
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-ID")).ReturnsAsync("test-client-id");
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-SECRET")).ReturnsAsync("test-client-secret");

            var mockLogger = new Mock<ILogger>();
            int attempts = 0;
            var handler = new FakeHttpMessageHandler((req, ct) =>
            {
                attempts++;
                if (attempts < 3)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent("{\"error\":\"server\"}", Encoding.UTF8, "application/json")
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"new\",\"refresh_token\":\"r\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
                });
            });

            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            using var auth = new QuickBooksAuthService(mockSettings.Object, mockSecretVault.Object, mockLogger.Object, httpClient, new Mock<IServiceProvider>().Object);

            await auth.RefreshTokenAsync();

            Assert.Equal("new", mockSettings.Object.Current.QboAccessToken);
            Assert.Equal("r", mockSettings.Object.Current.QboRefreshToken);
            Assert.Equal(3, attempts);
            mockSettings.Verify(x => x.Save(), Times.AtLeastOnce());
        }

        [Fact]
        public async Task RefreshTokenAsync_BadRequest_ClearsTokensAndThrows()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboRefreshToken = "old-refresh", QboAccessToken = "old", QboTokenExpiry = DateTime.UtcNow.AddHours(-1) };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mockSettings.Setup(x => x.Save()).Verifiable();

            var mockSecretVault = new Mock<ISecretVaultService>();
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-ID")).ReturnsAsync("test-client-id");
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-SECRET")).ReturnsAsync("test-client-secret");

            var mockLogger = new Mock<ILogger>();
            var errorJson = "{\"error\":\"invalid_request\"}";
            var handler = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse(errorJson, HttpStatusCode.BadRequest));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

            using var auth = new QuickBooksAuthService(mockSettings.Object, mockSecretVault.Object, mockLogger.Object, httpClient, new Mock<IServiceProvider>().Object);

            await Assert.ThrowsAsync<QuickBooksAuthException>(async () => await auth.RefreshTokenAsync());
            Assert.Null(mockSettings.Object.Current.QboAccessToken);
            Assert.Null(mockSettings.Object.Current.QboRefreshToken);
            Assert.Equal(default(DateTime), mockSettings.Object.Current.QboTokenExpiry);
            mockSettings.Verify(x => x.Save(), Times.AtLeastOnce());
        }

        [Fact]
        public async Task RefreshTokenAsync_InvalidJson_ThrowsQuickBooksAuthException()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboRefreshToken = "old-refresh", QboAccessToken = "old", QboTokenExpiry = DateTime.UtcNow.AddHours(-1) };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mockSettings.Setup(x => x.Save()).Verifiable();

            var mockSecretVault = new Mock<ISecretVaultService>();
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-ID")).ReturnsAsync("test-client-id");
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-SECRET")).ReturnsAsync("test-client-secret");

            var mockLogger = new Mock<ILogger>();
            var handler = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse("not-a-json"));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

            using var auth = new QuickBooksAuthService(mockSettings.Object, mockSecretVault.Object, mockLogger.Object, httpClient, new Mock<IServiceProvider>().Object);

            await Assert.ThrowsAsync<QuickBooksAuthException>(async () => await auth.RefreshTokenAsync());
            Assert.Null(mockSettings.Object.Current.QboAccessToken);
            Assert.Null(mockSettings.Object.Current.QboRefreshToken);
            Assert.Equal(default(DateTime), mockSettings.Object.Current.QboTokenExpiry);
            mockSettings.Verify(x => x.Save(), Times.AtLeastOnce());
        }

        [Fact]
        public async Task RefreshTokenIfNeededAsync_NoCallWhenAccessTokenValid()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "valid-token", QboTokenExpiry = DateTime.UtcNow.AddHours(1) };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-ID")).ReturnsAsync("test-client-id");
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-SECRET")).ReturnsAsync("test-client-secret");

            var mockLogger = new Mock<ILogger>();
            var handler = new FakeHttpMessageHandler((req, ct) => throw new InvalidOperationException("HTTP should not be called"));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

            using var auth = new QuickBooksAuthService(mockSettings.Object, mockSecretVault.Object, mockLogger.Object, httpClient, new Mock<IServiceProvider>().Object);

            // Act - should not throw
            await auth.RefreshTokenIfNeededAsync();
        }

        [Fact]
        public async Task RefreshTokenIfNeededAsync_NoRefreshToken_ThrowsInvalidOperationException()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "", QboRefreshToken = "", QboTokenExpiry = default };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-ID")).ReturnsAsync("test-client-id");
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-SECRET")).ReturnsAsync("test-client-secret");

            var mockLogger = new Mock<ILogger>();
            var handler = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonResponse("{}"));
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

            using var auth = new QuickBooksAuthService(mockSettings.Object, mockSecretVault.Object, mockLogger.Object, httpClient, new Mock<IServiceProvider>().Object);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await auth.RefreshTokenIfNeededAsync());
        }
    }
}