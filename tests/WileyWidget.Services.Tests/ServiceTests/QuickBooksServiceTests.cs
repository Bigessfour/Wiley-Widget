using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Task = System.Threading.Tasks.Task;
using Moq;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using Intuit.Ipp.Data;

namespace WileyWidget.Services.Tests.ServiceTests
{
    [SuppressMessage("Microsoft.Usage", "CA2000:Dispose objects before losing scope", Justification = "HttpClient is disposed by the service's Dispose method")]
    public class QuickBooksServiceTests
    {
        [Fact]
        public void HasValidAccessToken_ReturnsTrueWhenValid()
        {
            // Arrange
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings
            {
                QboAccessToken = "valid-token",
                QboTokenExpiry = DateTime.UtcNow.AddHours(1)
            };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            using var service = new QuickBooksService(
                mockSettings.Object,
                mockSecretVault.Object,
                mockLogger.Object,
                mockApiClient.Object,
                new HttpClient(),
                new Mock<IServiceProvider>().Object
            );

            // Assert
            Assert.True(service.HasValidAccessToken());
        }

        [Fact]
        public void HasValidAccessToken_ReturnsFalseWhenMissingOrExpired()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = null, QboTokenExpiry = default };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            using var service = new QuickBooksService(
                mockSettings.Object,
                mockSecretVault.Object,
                mockLogger.Object,
                mockApiClient.Object,
                new HttpClient(),
                new Mock<IServiceProvider>().Object
            );

            Assert.False(service.HasValidAccessToken());

            // Expired token
            mockSettings.SetupGet(x => x.Current).Returns(new AppSettings { QboAccessToken = "tok", QboTokenExpiry = DateTime.UtcNow.AddSeconds(-10) });
            Assert.False(service.HasValidAccessToken());
        }

        [Fact]
        public async Task RefreshTokenAsync_Success_UpdatesSettingsAndSaves()
        {
            // Arrange
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboRefreshToken = "old-refresh", QboAccessToken = null, QboTokenExpiry = default };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mockSettings.Setup(x => x.Save()).Verifiable();

            var mockSecretVault = new Mock<ISecretVaultService>();
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-ID")).ReturnsAsync("test-client-id");
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-SECRET")).ReturnsAsync("test-client-secret");

            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var json = "{\"access_token\":\"new-access\",\"refresh_token\":\"new-refresh\",\"expires_in\":3600}";
            var handler = new WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler(WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler.JsonResponse(json));
            using var httpClient = new HttpClient(handler);
            httpClient.BaseAddress = new Uri("http://localhost/");

            using var service = new QuickBooksService(
                mockSettings.Object,
                mockSecretVault.Object,
                mockLogger.Object,
                mockApiClient.Object,
                httpClient,
                new Mock<IServiceProvider>().Object
            );

            // Act
            await service.RefreshTokenAsync();

            // Assert
            Assert.Equal("new-access", mockSettings.Object.Current.QboAccessToken);
            Assert.Equal("new-refresh", mockSettings.Object.Current.QboRefreshToken);
            Assert.True(mockSettings.Object.Current.QboTokenExpiry > DateTime.UtcNow);
            mockSettings.Verify(x => x.Save(), Times.AtLeastOnce());
        }

        [Fact]
        public async Task RefreshTokenAsync_Failure_ClearsTokensAndThrows()
        {
            // Arrange
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboRefreshToken = "old-refresh", QboAccessToken = "old", QboTokenExpiry = DateTime.UtcNow.AddHours(-1) };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mockSettings.Setup(x => x.Save()).Verifiable();

            var mockSecretVault = new Mock<ISecretVaultService>();
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-ID")).ReturnsAsync("test-client-id");
            mockSecretVault.Setup(x => x.GetSecretAsync("QBO-CLIENT-SECRET")).ReturnsAsync("test-client-secret");

            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var errorJson = "{\"error\":\"invalid_request\"}";
            var handler = new WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler(WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler.JsonResponse(errorJson, System.Net.HttpStatusCode.BadRequest));
            using var httpClient = new HttpClient(handler);
            httpClient.BaseAddress = new Uri("http://localhost/");

            using var service = new QuickBooksService(
                mockSettings.Object,
                mockSecretVault.Object,
                mockLogger.Object,
                mockApiClient.Object,
                httpClient,
                new Mock<IServiceProvider>().Object
            );

            // Act & Assert
            await Assert.ThrowsAsync<QuickBooksAuthException>(async () => await service.RefreshTokenAsync());
            Assert.Null(mockSettings.Object.Current.QboAccessToken);
            Assert.Null(mockSettings.Object.Current.QboRefreshToken);
            Assert.Equal(default(DateTime), mockSettings.Object.Current.QboTokenExpiry);
            mockSettings.Verify(x => x.Save(), Times.AtLeastOnce());
        }

        [Fact]
        public async Task RefreshTokenIfNeededAsync_UsesRefreshWhenNeeded()
        {
            // Arrange
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "old", QboRefreshToken = "old-refresh", QboTokenExpiry = DateTime.UtcNow.AddHours(-2) };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mockSettings.Setup(x => x.Save()).Verifiable();

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var prevClientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
            var prevClientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
            try
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

                var json = "{\"access_token\":\"refreshed-access\",\"refresh_token\":\"refreshed-refresh\",\"expires_in\":3600}";
                var handler = new WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler(WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler.JsonResponse(json));
                using var httpClient = new HttpClient(handler);
                httpClient.BaseAddress = new Uri("http://localhost/");

                using var service = new QuickBooksService(
                    mockSettings.Object,
                    mockSecretVault.Object,
                    mockLogger.Object,
                    mockApiClient.Object,
                    httpClient,
                    new Mock<IServiceProvider>().Object
                );

                // Act
                await service.RefreshTokenIfNeededAsync();

                // Assert
                Assert.Equal("refreshed-access", mockSettings.Object.Current.QboAccessToken);
                Assert.Equal("refreshed-refresh", mockSettings.Object.Current.QboRefreshToken);
                mockSettings.Verify(x => x.Save(), Times.AtLeastOnce());
            }
            finally
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", prevClientId);
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", prevClientSecret);
            }
        }

        [Fact]
        public async Task TestConnectionAsync_ReturnsFalseWhenRealmNotSet()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "valid-token", QboTokenExpiry = DateTime.UtcNow.AddHours(1) };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            // Ensure client secrets exist so initialization proceeds but realmId remains unset
            var prevClientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
            var prevClientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
            try
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

                using var service = new QuickBooksService(
                    mockSettings.Object,
                    mockSecretVault.Object,
                    mockLogger.Object,
                    mockApiClient.Object,
                    new HttpClient(),
                    new Mock<IServiceProvider>().Object
                );

                var result = await service.TestConnectionAsync();
                Assert.False(result);
            }
            finally
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", prevClientId);
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", prevClientSecret);
            }
        }

        [Fact]
        public async Task SyncDataAsync_ReturnsFailureWhenAccessTokenInvalid()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "", QboRefreshToken = "", QboTokenExpiry = default };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            using var service = new QuickBooksService(
                mockSettings.Object,
                mockSecretVault.Object,
                mockLogger.Object,
                mockApiClient.Object,
                new HttpClient(),
                new Mock<IServiceProvider>().Object
            );

            var result = await service.SyncDataAsync();
            Assert.False(result.Success);
            Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
        }

        [Fact]
        public async Task RefreshTokenIfNeededAsync_NoRefreshToken_SkipsInteractiveWhenWW_SKIP_INTERACTIVESet()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "", QboRefreshToken = "", QboTokenExpiry = default };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var prevSkip = Environment.GetEnvironmentVariable("WW_SKIP_INTERACTIVE");
            var prevPrint = Environment.GetEnvironmentVariable("WW_PRINT_AUTH_URL");
            var prevClientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
            var prevClientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
            try
            {
                Environment.SetEnvironmentVariable("WW_SKIP_INTERACTIVE", "1");
                Environment.SetEnvironmentVariable("WW_PRINT_AUTH_URL", null);
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

                var mockSecretVault = new Mock<ISecretVaultService>();
                var mockLogger = new Mock<ILogger<QuickBooksService>>();
                var mockApiClient = new Mock<IQuickBooksApiClient>();

                using var service = new QuickBooksService(
                    mockSettings.Object,
                    mockSecretVault.Object,
                    mockLogger.Object,
                    mockApiClient.Object,
                    new HttpClient(),
                    new Mock<IServiceProvider>().Object
                );

                // Should not throw - AcquireTokensInteractiveAsync returns true when WW_SKIP_INTERACTIVE is set
                await service.RefreshTokenIfNeededAsync();
            }
            finally
            {
                Environment.SetEnvironmentVariable("WW_SKIP_INTERACTIVE", prevSkip);
                Environment.SetEnvironmentVariable("WW_PRINT_AUTH_URL", prevPrint);
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", prevClientId);
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", prevClientSecret);
            }
        }

        [Fact]
        public async Task SyncDataAsync_UsesInjectedDataService()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings();
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var mockDs = new Mock<IQuickBooksDataService>();
            mockDs.Setup(x => x.FindCustomers(It.IsAny<int>(), It.IsAny<int>())).Returns(new List<Intuit.Ipp.Data.Customer> { new Intuit.Ipp.Data.Customer(), new Intuit.Ipp.Data.Customer() });
            mockDs.Setup(x => x.FindInvoices(It.IsAny<int>(), It.IsAny<int>())).Returns(new List<Intuit.Ipp.Data.Invoice> { new Intuit.Ipp.Data.Invoice(), new Intuit.Ipp.Data.Invoice(), new Intuit.Ipp.Data.Invoice() });
            mockDs.Setup(x => x.FindBills(It.IsAny<int>(), It.IsAny<int>())).Returns(new List<Intuit.Ipp.Data.Bill> { new Intuit.Ipp.Data.Bill(), new Intuit.Ipp.Data.Bill() });
            mockDs.Setup(x => x.FindAccounts(It.IsAny<int>(), It.IsAny<int>())).Returns(new List<Intuit.Ipp.Data.Account> { new Intuit.Ipp.Data.Account() });
            mockDs.Setup(x => x.FindVendors(It.IsAny<int>(), It.IsAny<int>())).Returns(new List<Intuit.Ipp.Data.Vendor> { new Intuit.Ipp.Data.Vendor() });

            using var service = new QuickBooksService(
                mockSettings.Object,
                mockSecretVault.Object,
                mockLogger.Object,
                mockApiClient.Object,
                new HttpClient(),
                new Mock<IServiceProvider>().Object,
                mockDs.Object
            );

            var result = await service.SyncDataAsync(CancellationToken.None);
            Assert.True(result.Success);
            Assert.Equal(7, result.RecordsSynced);
        }

        [Fact]
        public async Task GetChartOfAccountsAsync_PaginatesUsingInjectedService()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings();
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var mockDs = new Mock<IQuickBooksDataService>();
            mockDs.Setup(x => x.FindAccounts(It.Is<int>(s => s == 1), It.IsAny<int>())).Returns(new List<Intuit.Ipp.Data.Account>
            {
                new Intuit.Ipp.Data.Account { AcctNum = "100", Name = "Acct 100" },
                new Intuit.Ipp.Data.Account { AcctNum = "101", Name = "Acct 101" }
            });
            mockDs.Setup(x => x.FindAccounts(It.Is<int>(s => s > 1), It.IsAny<int>())).Returns(new List<Intuit.Ipp.Data.Account>());
            mockDs.Setup(x => x.FindBills(It.IsAny<int>(), It.IsAny<int>())).Returns(new List<Intuit.Ipp.Data.Bill>());

            using var service = new QuickBooksService(
                mockSettings.Object,
                mockSecretVault.Object,
                mockLogger.Object,
                mockApiClient.Object,
                new HttpClient(),
                new Mock<IServiceProvider>().Object,
                mockDs.Object
            );

            var accounts = await service.GetChartOfAccountsAsync();
            Assert.Equal(2, accounts.Count);
            Assert.Contains(accounts, a => a.AcctNum == "100");
            Assert.Contains(accounts, a => a.AcctNum == "101");
        }

        [Fact]
        public async Task GetJournalEntriesAsync_UsesInjectedDataService()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings();
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var expected = new List<Intuit.Ipp.Data.JournalEntry> { new Intuit.Ipp.Data.JournalEntry(), new Intuit.Ipp.Data.JournalEntry() };
            var mockDs = new Mock<IQuickBooksDataService>();
            mockDs.Setup(x => x.FindJournalEntries(It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(expected);
            mockDs.Setup(x => x.FindBills(It.IsAny<int>(), It.IsAny<int>())).Returns(new List<Intuit.Ipp.Data.Bill>());

            using var service = new QuickBooksService(
                mockSettings.Object,
                mockSecretVault.Object,
                mockLogger.Object,
                mockApiClient.Object,
                new HttpClient(),
                new Mock<IServiceProvider>().Object,
                mockDs.Object
            );

            var entries = await service.GetJournalEntriesAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
            Assert.Equal(expected.Count, entries.Count);
        }

        [Fact]
        public async Task TestConnectionAsync_UsesInjectedDataService()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings();
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var mockDs = new Mock<IQuickBooksDataService>();
            mockDs.Setup(x => x.FindCustomers(It.IsAny<int>(), It.IsAny<int>())).Returns(new List<Customer> { new Customer() });
            mockDs.Setup(x => x.FindBills(It.IsAny<int>(), It.IsAny<int>())).Returns(new List<Intuit.Ipp.Data.Bill>());

            using var service = new QuickBooksService(
                mockSettings.Object,
                mockSecretVault.Object,
                mockLogger.Object,
                mockApiClient.Object,
                new HttpClient(),
                new Mock<IServiceProvider>().Object,
                mockDs.Object
            );

            var result = await service.TestConnectionAsync();
            Assert.True(result);
        }

        [Fact]
        public async Task GetBudgetsAsync_UsesInjectedDataService()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings();
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var expected = new List<WileyWidget.Models.QuickBooksBudget> { new WileyWidget.Models.QuickBooksBudget { QuickBooksId = "QB-1", Name = "Budget", FiscalYear = 2024 } };
            mockApiClient.Setup(x => x.GetBudgetsAsync()).ReturnsAsync(expected);

            using var service = new QuickBooksService(
                mockSettings.Object,
                mockSecretVault.Object,
                mockLogger.Object,
                mockApiClient.Object,
                new HttpClient(),
                new Mock<IServiceProvider>().Object
            );

            var budgets = await service.GetBudgetsAsync();
            Assert.Equal(expected.Count, budgets.Count);
            Assert.Equal("QB-1", budgets[0].QuickBooksId);
        }

        [Fact]
        public async Task SyncBudgetsToAppAsync_ValidBudgets_SyncsSuccessfully()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "valid-token", QboTokenExpiry = DateTime.UtcNow.AddHours(1), QuickBooksRealmId = "realm" };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var handler = new WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler(WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler.JsonResponse("{}"));
            using var httpClient = new HttpClient(handler);
            httpClient.BaseAddress = new Uri("http://localhost/");

            var budgets = new List<WileyWidget.Models.QuickBooksBudget>
            {
                new WileyWidget.Models.QuickBooksBudget { QuickBooksId = "B1", Name = "Budget 1", FiscalYear = 2024 },
                new WileyWidget.Models.QuickBooksBudget { QuickBooksId = "B2", Name = "Budget 2", FiscalYear = 2024 }
            };

            var prevClientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
            var prevClientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
            try
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

                using var service = new QuickBooksService(
                    mockSettings.Object,
                    mockSecretVault.Object,
                    mockLogger.Object,
                    mockApiClient.Object,
                    httpClient,
                    new Mock<IServiceProvider>().Object
                );

                var result = await service.SyncBudgetsToAppAsync(budgets, CancellationToken.None);
                Assert.True(result.Success, $"Sync failed: {result.ErrorMessage}");
                Assert.Equal(2, result.RecordsSynced);
            }
            finally
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", prevClientId);
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", prevClientSecret);
            }
        }

        [Fact]
        public async Task SyncBudgetsToAppAsync_EmptyList_ReturnsSuccessWithZeroSynced()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "valid-token", QboTokenExpiry = DateTime.UtcNow.AddHours(1), QuickBooksRealmId = "realm" };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var handler = new WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler(WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler.JsonResponse("{}"));
            using var httpClient = new HttpClient(handler);

            var budgets = new List<WileyWidget.Models.QuickBooksBudget>();

            var prevClientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
            var prevClientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
            try
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

                using var service = new QuickBooksService(
                    mockSettings.Object,
                    mockSecretVault.Object,
                    mockLogger.Object,
                    mockApiClient.Object,
                    httpClient,
                    new Mock<IServiceProvider>().Object
                );

                var result = await service.SyncBudgetsToAppAsync(budgets, CancellationToken.None);
                Assert.True(result.Success);
                Assert.Equal(0, result.RecordsSynced);
            }
            finally
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", prevClientId);
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", prevClientSecret);
            }
        }

        [Fact]
        public async Task SyncBudgetsToAppAsync_HttpFailure_ReturnsFailureAndLogs()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "valid-token", QboTokenExpiry = DateTime.UtcNow.AddHours(1), QuickBooksRealmId = "realm" };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            int call = 0;
            var handler = new WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler((req, ct) =>
            {
                if (call == 0)
                {
                    call = 1;
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{}") });
                }
                else
                {
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":\"bad\"}") });
                }
            });

            using var httpClient = new HttpClient(handler);
            httpClient.BaseAddress = new Uri("http://localhost/");

            var budgets = new List<WileyWidget.Models.QuickBooksBudget>
            {
                new WileyWidget.Models.QuickBooksBudget { QuickBooksId = "B1", Name = "Budget 1", FiscalYear = 2024 },
                new WileyWidget.Models.QuickBooksBudget { QuickBooksId = "B2", Name = "Budget 2", FiscalYear = 2024 }
            };

            var prevClientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
            var prevClientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
            try
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

                using var service = new QuickBooksService(
                    mockSettings.Object,
                    mockSecretVault.Object,
                    mockLogger.Object,
                    mockApiClient.Object,
                    httpClient,
                    new Mock<IServiceProvider>().Object
                );

                var result = await service.SyncBudgetsToAppAsync(budgets, CancellationToken.None);
                Assert.False(result.Success, $"Expected failure due to partial HTTP error, got success. ErrorMessage={result.ErrorMessage}");
                Assert.Equal(1, result.RecordsSynced);
                Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
            }
            finally
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", prevClientId);
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", prevClientSecret);
            }
        }

        [Fact]
        public async Task SyncBudgetsToAppAsync_Cancellation_ReturnsErrorResult()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "valid-token", QboTokenExpiry = DateTime.UtcNow.AddHours(1), QuickBooksRealmId = "realm" };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var handler = new WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler(WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler.JsonResponse("{}"));
            using var httpClient = new HttpClient(handler);

            var budgets = new List<WileyWidget.Models.QuickBooksBudget>
            {
                new WileyWidget.Models.QuickBooksBudget { QuickBooksId = "B1", Name = "Budget 1", FiscalYear = 2024 }
            };

            var prevClientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
            var prevClientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
            try
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

                using var service = new QuickBooksService(
                    mockSettings.Object,
                    mockSecretVault.Object,
                    mockLogger.Object,
                    mockApiClient.Object,
                    httpClient,
                    new Mock<IServiceProvider>().Object
                );

                using var cts = new CancellationTokenSource();
                cts.Cancel();
                var result = await service.SyncBudgetsToAppAsync(budgets, cts.Token);
                Assert.False(result.Success);
                Assert.Equal("Operation cancelled", result.ErrorMessage);
            }
            finally
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", prevClientId);
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", prevClientSecret);
            }
        }

        [Fact]
        public async Task SyncVendorsToAppAsync_ValidVendors_SyncsSuccessfully()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "valid-token", QboTokenExpiry = DateTime.UtcNow.AddHours(1), QuickBooksRealmId = "realm" };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var handler = new WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler(WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler.JsonResponse("{}"));
            using var httpClient = new HttpClient(handler);
            httpClient.BaseAddress = new Uri("http://localhost/");

            var vendors = new List<Intuit.Ipp.Data.Vendor>
            {
                new Intuit.Ipp.Data.Vendor { Id = "V1" },
                new Intuit.Ipp.Data.Vendor { Id = "V2" }
            };

            var prevClientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
            var prevClientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
            try
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

                using var service = new QuickBooksService(
                    mockSettings.Object,
                    mockSecretVault.Object,
                    mockLogger.Object,
                    mockApiClient.Object,
                    httpClient,
                    new Mock<IServiceProvider>().Object
                );

                var result = await service.SyncVendorsToAppAsync(vendors, CancellationToken.None);
                Assert.True(result.Success, $"Sync failed: {result.ErrorMessage}");
                Assert.Equal(2, result.RecordsSynced);
            }
            finally
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", prevClientId);
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", prevClientSecret);
            }
        }

        [Fact]
        public async Task SyncVendorsToAppAsync_EmptyList_ReturnsSuccessWithZeroSynced()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "valid-token", QboTokenExpiry = DateTime.UtcNow.AddHours(1), QuickBooksRealmId = "realm" };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var handler = new WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler(WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler.JsonResponse("{}"));
            using var httpClient = new HttpClient(handler);
            httpClient.BaseAddress = new Uri("http://localhost/");

            var vendors = new List<Intuit.Ipp.Data.Vendor>();

            var prevClientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
            var prevClientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
            try
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

                using var service = new QuickBooksService(
                    mockSettings.Object,
                    mockSecretVault.Object,
                    mockLogger.Object,
                    mockApiClient.Object,
                    httpClient,
                    new Mock<IServiceProvider>().Object
                );

                var result = await service.SyncVendorsToAppAsync(vendors, CancellationToken.None);
                Assert.True(result.Success);
                Assert.Equal(0, result.RecordsSynced);
            }
            finally
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", prevClientId);
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", prevClientSecret);
            }
        }

        [Fact]
        public async Task SyncVendorsToAppAsync_HttpFailure_ReturnsFailureAndLogs()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "valid-token", QboTokenExpiry = DateTime.UtcNow.AddHours(1), QuickBooksRealmId = "realm" };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var handler = new WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler(WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler.JsonResponse("{\"error\":\"Bad Request\"}", System.Net.HttpStatusCode.BadRequest));
            using var httpClient = new HttpClient(handler);
            httpClient.BaseAddress = new Uri("http://localhost/");

            var vendors = new List<Intuit.Ipp.Data.Vendor>
            {
                new Intuit.Ipp.Data.Vendor { Id = "V1" }
            };

            var prevClientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
            var prevClientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
            try
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

                using var service = new QuickBooksService(
                    mockSettings.Object,
                    mockSecretVault.Object,
                    mockLogger.Object,
                    mockApiClient.Object,
                    httpClient,
                    new Mock<IServiceProvider>().Object
                );

                var result = await service.SyncVendorsToAppAsync(vendors, CancellationToken.None);
                Assert.False(result.Success);
                Assert.Equal(0, result.RecordsSynced);
                Assert.Contains("One or more vendors failed to sync", result.ErrorMessage, StringComparison.Ordinal);
            }
            finally
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", prevClientId);
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", prevClientSecret);
            }
        }

        [Fact]
        public async Task SyncVendorsToAppAsync_Cancellation_ReturnsErrorResult()
        {
            var mockSettings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { QboAccessToken = "valid-token", QboTokenExpiry = DateTime.UtcNow.AddHours(1), QuickBooksRealmId = "realm" };
            mockSettings.SetupGet(x => x.Current).Returns(appSettings);
            mockSettings.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);

            var mockSecretVault = new Mock<ISecretVaultService>();
            var mockLogger = new Mock<ILogger<QuickBooksService>>();
            var mockApiClient = new Mock<IQuickBooksApiClient>();

            var handler = new WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler(WileyWidget.Services.Tests.TestHelpers.FakeHttpMessageHandler.JsonResponse("{}"));
            using var httpClient = new HttpClient(handler);
            httpClient.BaseAddress = new Uri("http://localhost/");

            var vendors = new List<Intuit.Ipp.Data.Vendor>
            {
                new Intuit.Ipp.Data.Vendor { Id = "V1" }
            };

            var prevClientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
            var prevClientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
            try
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

                using var service = new QuickBooksService(
                    mockSettings.Object,
                    mockSecretVault.Object,
                    mockLogger.Object,
                    mockApiClient.Object,
                    httpClient,
                    new Mock<IServiceProvider>().Object
                );

                using var cts = new CancellationTokenSource();
                cts.Cancel();

                var result = await service.SyncVendorsToAppAsync(vendors, cts.Token);
                Assert.False(result.Success);
                Assert.Contains("Operation cancelled", result.ErrorMessage, StringComparison.Ordinal);
            }
            finally
            {
                Environment.SetEnvironmentVariable("QBO_CLIENT_ID", prevClientId);
                Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", prevClientSecret);
            }
        }
    }
}

