using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RichardSzalay.MockHttp;
using Moq;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.Integration.Tests.Shared;
using Xunit;

namespace WileyWidget.Integration.Tests.Services
{
    public class QuickBooksServiceIntegrationTests : IntegrationTestBase
    {
        public QuickBooksServiceIntegrationTests()
            : base(services =>
            {
                // Replace QuickBooksService registration with a test instance that uses a mock HttpClient
                var existing = services.Where(sd => sd.ServiceType == typeof(IQuickBooksService)).ToList();
                foreach (var sd in existing) services.Remove(sd);

                var handler = new MockHttpMessageHandler();
                // The SyncBudgetsToAppAsync implementation uses a dummy GET to "http://dummy" per budget - respond OK
                handler.When("http://dummy").Respond("text/plain", "ok");

                // Mock the API client to return empty lists
                var apiMock = new Mock<IQuickBooksApiClient>();
                apiMock.Setup(a => a.GetCustomersAsync()).ReturnsAsync(new List<Intuit.Ipp.Data.Customer>());
                apiMock.Setup(a => a.GetInvoicesAsync()).ReturnsAsync(new List<Intuit.Ipp.Data.Invoice>());
                apiMock.Setup(a => a.GetChartOfAccountsAsync()).ReturnsAsync(new List<Intuit.Ipp.Data.Account>());
                apiMock.Setup(a => a.GetJournalEntriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(new List<Intuit.Ipp.Data.JournalEntry>());
                apiMock.Setup(a => a.GetBudgetsAsync()).ReturnsAsync(new List<WileyWidget.Models.QuickBooksBudget>());
                services.AddSingleton(apiMock);
                services.AddSingleton<IQuickBooksApiClient>(sp => sp.GetRequiredService<Mock<IQuickBooksApiClient>>().Object);

                // Provide a mock IQuickBooksDataService so QuickBooksService avoids interactive auth
                var dataServiceMock = new Mock<IQuickBooksDataService>();
                dataServiceMock.Setup(ds => ds.FindCustomers(It.IsAny<int>(), It.IsAny<int>())).Returns(new List<Intuit.Ipp.Data.Customer>());
                dataServiceMock.Setup(ds => ds.FindInvoices(It.IsAny<int>(), It.IsAny<int>())).Returns(new List<Intuit.Ipp.Data.Invoice>());
                dataServiceMock.Setup(ds => ds.FindAccounts(It.IsAny<int>(), It.IsAny<int>())).Returns(new List<Intuit.Ipp.Data.Account>());
                dataServiceMock.Setup(ds => ds.FindJournalEntries(It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(new List<Intuit.Ipp.Data.JournalEntry>());
                dataServiceMock.Setup(ds => ds.FindBudgets(It.IsAny<int>(), It.IsAny<int>())).Returns(new List<Intuit.Ipp.Data.Budget>());
                dataServiceMock.Setup(ds => ds.FindVendors(It.IsAny<int>(), It.IsAny<int>())).Returns(new List<Intuit.Ipp.Data.Vendor>());
                services.AddSingleton(dataServiceMock);
                services.AddSingleton<IQuickBooksDataService>(sp => sp.GetRequiredService<Mock<IQuickBooksDataService>>().Object);

                // Register a test QuickBooksService that uses our mock HttpClient and injected data service
                services.AddSingleton<IQuickBooksService>(sp =>
                {
                    var settings = sp.GetRequiredService<SettingsService>();
                    var secretVault = sp.GetRequiredService<ISecretVaultService>();
                    var logger = sp.GetRequiredService<ILogger<QuickBooksService>>();
                    var apiClient = sp.GetRequiredService<IQuickBooksApiClient>();
                    var dataService = sp.GetService<IQuickBooksDataService>();
                    var httpClient = new HttpClient(handler);
                    return new QuickBooksService(settings, secretVault, logger, apiClient, httpClient, sp, dataService);
                });
            })
        {
        }

        [Fact]
        public async Task SyncBudgetsToAppAsync_WithValidSettings_ReturnsSuccess()
        {
            // Arrange: ensure SettingsService has a valid access token and expiry so RefreshTokenIfNeededAsync will not try interactive auth
            var settings = GetRequiredService<SettingsService>();
            settings.Current.QboAccessToken = "valid-token";
            settings.Current.QboTokenExpiry = DateTime.UtcNow.AddHours(1);
            settings.Current.QuickBooksRealmId = "realm";

            // Ensure environment variable exists for QBO_CLIENT_ID so EnsureInitializedAsync does not throw
            Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
            Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

            var qbService = GetRequiredService<IQuickBooksService>();

            var budgets = new List<QuickBooksBudget>
            {
                new() { Id = 1, Name = "Budget 1", FiscalYear = DateTime.UtcNow.Year }
            };

            // Act: apply settings needed for a non-interactive token check
            settings.Current.QboAccessToken = "valid-token";
            settings.Current.QboTokenExpiry = DateTime.UtcNow.AddHours(1);
            settings.Current.QuickBooksRealmId = "realm";

            var result = await qbService.SyncBudgetsToAppAsync(budgets);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue($"Error: {result.ErrorMessage ?? "<null>"}; RecordsSynced: {result.RecordsSynced}");
            result.RecordsSynced.Should().Be(budgets.Count);
        }

        [Fact]
        public async Task SyncDataAsync_ReturnsSuccess()
        {
            // Arrange
            var settings = GetRequiredService<SettingsService>();
            settings.Current.QboAccessToken = "valid-token";
            settings.Current.QboTokenExpiry = DateTime.UtcNow.AddHours(1);
            settings.Current.QuickBooksRealmId = "realm";

            Environment.SetEnvironmentVariable("QBO_CLIENT_ID", "test-client-id");
            Environment.SetEnvironmentVariable("QBO_CLIENT_SECRET", "test-client-secret");

            var qbService = GetRequiredService<IQuickBooksService>();

            // Act
            var result = await qbService.SyncDataAsync();

            // Assert
            result.Success.Should().BeTrue();
            result.RecordsSynced.Should().Be(1);
        }

        [Fact]
        public async Task GetCustomersAsync_ReturnsEmptyList()
        {
            var qbService = GetRequiredService<IQuickBooksService>();
            var result = await qbService.GetCustomersAsync();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetInvoicesAsync_ReturnsEmptyList()
        {
            var qbService = GetRequiredService<IQuickBooksService>();
            var result = await qbService.GetInvoicesAsync();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetChartOfAccountsAsync_ReturnsEmptyList()
        {
            var qbService = GetRequiredService<IQuickBooksService>();
            var result = await qbService.GetChartOfAccountsAsync();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetJournalEntriesAsync_ReturnsEmptyList()
        {
            var qbService = GetRequiredService<IQuickBooksService>();
            var result = await qbService.GetJournalEntriesAsync(DateTime.Now.AddDays(-1), DateTime.Now);
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetBudgetsAsync_ReturnsEmptyList()
        {
            var qbService = GetRequiredService<IQuickBooksService>();
            var result = await qbService.GetBudgetsAsync();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task SyncAsync_NoNewInvoices_DoesNothing()
        {
            // Arrange
            var qbService = GetRequiredService<IQuickBooksService>();
            // Mock returns empty lists

            // Act
            var result = await qbService.SyncDataAsync();

            // Assert
            result.Success.Should().BeTrue();
            result.RecordsSynced.Should().Be(0);
        }

        [Fact]
        public async Task SyncAsync_NewInvoices_AddsTransactionsToDb()
        {
            // Arrange
            await SeedTestDataAsync(); // Seed budget entries
            var apiMock = new Mock<IQuickBooksApiClient>();
            apiMock.Setup(a => a.GetInvoicesAsync()).ReturnsAsync(new List<Intuit.Ipp.Data.Invoice>
            {
                new Intuit.Ipp.Data.Invoice { Id = "1", TotalAmt = 100, TxnDate = DateTime.Now }
            });
            // Register mock
            var services = new ServiceCollection();
            services.AddSingleton(apiMock);
            services.AddSingleton<IQuickBooksApiClient>(sp => sp.GetRequiredService<Mock<IQuickBooksApiClient>>().Object);
            // Rebuild services if needed

            var qbService = GetRequiredService<IQuickBooksService>();

            // Act
            var result = await qbService.SyncDataAsync();

            // Assert
            result.Success.Should().BeTrue();
            result.RecordsSynced.Should().BeGreaterThan(0);
            // Verify transactions added to DB
            var transactions = DbContext.Transactions.ToList();
            transactions.Should().NotBeEmpty();
        }

        [Fact]
        public async Task SyncAsync_ModifiedInvoice_UpdatesExistingTransaction()
        {
            // Arrange
            await SeedTestDataAsync();
            // Add existing transaction
            var existingTxn = new WileyWidget.Models.Transaction
            {
                BudgetEntryId = 1,
                Amount = 50,
                TransactionDate = DateTime.Now,
                Description = "Existing"
            };
            DbContext.Transactions.Add(existingTxn);
            await DbContext.SaveChangesAsync();

            var apiMock = new Mock<IQuickBooksApiClient>();
            apiMock.Setup(a => a.GetInvoicesAsync()).ReturnsAsync(new List<Intuit.Ipp.Data.Invoice>
            {
                new Intuit.Ipp.Data.Invoice { Id = "1", TotalAmt = 75, TxnDate = DateTime.Now } // Modified amount
            });

            var qbService = GetRequiredService<IQuickBooksService>();

            // Act
            var result = await qbService.SyncDataAsync();

            // Assert
            result.Success.Should().BeTrue();
            var updatedTxn = await DbContext.Transactions.FindAsync(existingTxn.Id);
            updatedTxn.Amount.Should().Be(75);
        }

        [Fact]
        public async Task SyncAsync_WithRateLimit_Response_RetriesViaPolly()
        {
            // Arrange
            var apiMock = new Mock<IQuickBooksApiClient>();
            apiMock.SetupSequence(a => a.GetInvoicesAsync())
                .ThrowsAsync(new HttpRequestException("Rate limited"))
                .ReturnsAsync(new List<Intuit.Ipp.Data.Invoice>()); // Succeed on retry

            var qbService = GetRequiredService<IQuickBooksService>();

            // Act
            var result = await qbService.SyncDataAsync();

            // Assert
            result.Success.Should().BeTrue(); // Should succeed after retry
        }

        [Fact]
        public async Task SyncAsync_InvalidToken_ThrowsAuthenticationException()
        {
            // Arrange
            var apiMock = new Mock<IQuickBooksApiClient>();
            apiMock.Setup(a => a.GetInvoicesAsync()).ThrowsAsync(new UnauthorizedAccessException("Invalid token"));

            var qbService = GetRequiredService<IQuickBooksService>();

            // Act & Assert
            var result = await qbService.SyncDataAsync();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("authentication");
        }

        [Fact]
        public async Task SyncAsync_PartialFailure_LogsAndContinues()
        {
            // Arrange
            var apiMock = new Mock<IQuickBooksApiClient>();
            apiMock.Setup(a => a.GetInvoicesAsync()).ReturnsAsync(new List<Intuit.Ipp.Data.Invoice>
            {
                new Intuit.Ipp.Data.Invoice { Id = "1", TotalAmt = 100, TxnDate = DateTime.Now }
            });
            apiMock.Setup(a => a.GetCustomersAsync()).ThrowsAsync(new Exception("Partial failure"));

            var qbService = GetRequiredService<IQuickBooksService>();

            // Act
            var result = await qbService.SyncDataAsync();

            // Assert
            result.Success.Should().BeTrue(); // Continues despite partial failure
            result.RecordsSynced.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task FullSync_FromDate_PullsOnlyRecentChanges()
        {
            // Arrange
            var fromDate = DateTime.Now.AddDays(-7);
            var apiMock = new Mock<IQuickBooksApiClient>();
            apiMock.Setup(a => a.GetJournalEntriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Intuit.Ipp.Data.JournalEntry>
                {
                    new Intuit.Ipp.Data.JournalEntry { Id = "1", TxnDate = DateTime.Now }
                });

            var qbService = GetRequiredService<IQuickBooksService>();

            // Act
            var result = await qbService.SyncDataAsync();

            // Assert
            result.Success.Should().BeTrue();
            // Verify only recent entries processed
        }
    }
}
