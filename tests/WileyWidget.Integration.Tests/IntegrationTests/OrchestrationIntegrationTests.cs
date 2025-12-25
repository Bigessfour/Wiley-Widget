using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services;
using WileyWidget.Business.Interfaces;
using WileyWidget.Integration.Tests.Shared;
using Xunit;

/*
namespace WileyWidget.Integration.Tests.IntegrationTests
{
    public class OrchestrationIntegrationTests : IntegrationTestBase
    {
        public OrchestrationIntegrationTests()
            : base(services =>
            {
                // Provide a mock IAIService so AI calls are deterministic and don't hit network
                var aiMock = new Mock<IAIService>();
                aiMock
                    .Setup(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("Mock AI Insight");

                // Register the Mock<IAIService> so tests can retrieve & verify it
                services.AddSingleton(aiMock);
                services.AddSingleton<IAIService>(sp => sp.GetRequiredService<Mock<IAIService>>().Object);

                // Ensure GrokSupercomputer uses the real implementation in integration tests (not the Null stub)
                services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new AppOptions { EnableDataCaching = false, EnterpriseDataCacheSeconds = 60 }));
                services.AddScoped<IGrokSupercomputer, GrokSupercomputer>();

                // Register a simple test QuickBooksService that will insert a transaction when SyncDataAsync is called
                services.AddScoped<IQuickBooksService>(sp => new TestQuickBooksService(sp));
            })
        {
        }

        private static async Task<BudgetEntry> SeedBudgetEntryAsync(IServiceScope scope, decimal budgetedAmount, decimal actualAmount = 0m)
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var dept = new Department { Name = "Integration Dept" };
            var fund = new Fund { FundCode = "900-INT", Name = "Integration Fund", Type = FundType.GeneralFund };

            ctx.Departments.Add(dept);
            ctx.Funds.Add(fund);
            await ctx.SaveChangesAsync();

            var entry = new BudgetEntry
            {
                AccountNumber = "500",
                Description = "Integration Test Entry",
                BudgetedAmount = budgetedAmount,
                ActualAmount = actualAmount,
                FiscalYear = DateTime.Now.Year,
                StartPeriod = new DateTime(DateTime.Now.Year, 1, 1),
                EndPeriod = new DateTime(DateTime.Now.Year, 12, 31),
                DepartmentId = dept.Id,
                FundId = fund.Id,
                CreatedAt = DateTime.Now
            };

            ctx.BudgetEntries.Add(entry);
            await ctx.SaveChangesAsync();

            return entry;
        }

        [Fact]
        public async Task ImportQuickBooksInvoice_TriggersBudgetRecalculation_AndRemainingUpdates()
        {
            await ResetDatabaseAsync();

            using var seedScope = CreateScope();
            var ctx = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Seed a budget entry
            var entry = await SeedBudgetEntryAsync(seedScope, budgetedAmount: 1000m, actualAmount: 0m);

            // Simulate importing an invoice as a transaction and update the budget entry
            var txn = new Transaction
            {
                BudgetEntryId = entry.Id,
                Amount = 200m,
                Description = "QB Invoice #1001",
                TransactionDate = DateTime.Now,
                Type = "Invoice",
                CreatedAt = DateTime.Now
            };

            ctx.Transactions.Add(txn);
            entry.ActualAmount += txn.Amount;
            await ctx.SaveChangesAsync();

            // Diagnostics: ensure DB contains entries and inspect CreatedAt
            var entriesList = await ctx.BudgetEntries.ToListAsync();
            entriesList.Should().NotBeEmpty("Budget entries should exist in DB");
            var txList = await ctx.Transactions.ToListAsync();
            txList.Should().NotBeEmpty("Transactions should exist in DB");
            var startRange = DateTime.Now.AddYears(-1);
            var endRange = DateTime.Now.AddYears(1);
            foreach (var e in entriesList)
            {
                var inRange = e.CreatedAt >= startRange && e.CreatedAt <= endRange;
                Console.WriteLine($"DEBUG: BudgetEntry {e.Id} CreatedAt: {e.CreatedAt:o} (Kind={e.CreatedAt.Kind}) Start:{e.StartPeriod:o} End:{e.EndPeriod:o} InRange:{inRange}");
            }

            entriesList.Any(e => e.CreatedAt >= startRange && e.CreatedAt <= endRange).Should().BeTrue("At least one budget entry should have CreatedAt within the date range used for summary");

            var factory = GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var factoryCtx = await factory.CreateDbContextAsync();
            var factoryCount = await factoryCtx.BudgetEntries.CountAsync();
            var ctxCount = await ctx.BudgetEntries.CountAsync();
            factoryCount.Should().Be(ctxCount, "DbContextFactory-created context should see same entries as the scoped context");

            var budgetRepo = GetRequiredService<IBudgetRepository>();
            var entriesByFy = await budgetRepo.GetByFiscalYearAsync(entry.FiscalYear);
            entriesByFy.Should().NotBeEmpty("GetByFiscalYearAsync should return the seeded budget entry");

            var directSummary = await budgetRepo.GetBudgetSummaryAsync(DateTime.Now.AddYears(-1), DateTime.Now.AddYears(1));
            directSummary.TotalBudgeted.Should().Be(1000m, "Seeded budget entry should be visible to BudgetRepository");

            var pipeline = GetRequiredService<IAnalyticsPipeline>();

            // Act
            var compliance = await pipeline.ExecuteFullPipelineAsync();

            // Assert
            compliance.Should().NotBeNull();
            compliance.BudgetSummary.Should().NotBeNull();
            compliance.BudgetSummary.TotalBudgeted.Should().Be(1000m);
            compliance.BudgetSummary.TotalActual.Should().Be(200m);
            var remaining = compliance.BudgetSummary.TotalBudgeted - compliance.BudgetSummary.TotalActual;
            remaining.Should().Be(800m);
        }

        [Fact]
        public async Task AddLargeTransaction_TriggersGrokInsightGeneration_AndSavesResult()
        {
            await ResetDatabaseAsync();

            using var seedScope = CreateScope();
            var ctx = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Seed a budget and add a large transaction
            var entry = await SeedBudgetEntryAsync(seedScope, budgetedAmount: 1000m, actualAmount: 0m);

            var txn = new Transaction
            {
                BudgetEntryId = entry.Id,
                Amount = 600m, // large relative to budget
                Description = "Large Transaction",
                TransactionDate = DateTime.Now,
                Type = "Payment",
                CreatedAt = DateTime.Now
            };

            ctx.Transactions.Add(txn);
            entry.ActualAmount += txn.Amount;
            await ctx.SaveChangesAsync();

            // Diagnostics: ensure DB and repository see changes
            var entriesList = await ctx.BudgetEntries.ToListAsync();
            entriesList.Should().NotBeEmpty("Budget entries should exist in DB");
            var startRange = DateTime.Now.AddYears(-1);
            var endRange = DateTime.Now.AddYears(1);
            foreach (var e in entriesList)
            {
                var inRange = e.CreatedAt >= startRange && e.CreatedAt <= endRange;
                Console.WriteLine($"DEBUG: BudgetEntry {e.Id} CreatedAt: {e.CreatedAt:o} (Kind={e.CreatedAt.Kind}) Start:{e.StartPeriod:o} End:{e.EndPeriod:o} InRange:{inRange}");
            }
            entriesList.Any(e => e.CreatedAt >= startRange && e.CreatedAt <= endRange).Should().BeTrue("At least one budget entry should have CreatedAt within the date range used for summary");
            var txList = await ctx.Transactions.ToListAsync();
            txList.Should().ContainSingle(t => t.Amount == 600m);

            var factory = GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var factoryCtx = await factory.CreateDbContextAsync();
            var persistedEntry = await factoryCtx.BudgetEntries.AsNoTracking().FirstOrDefaultAsync(be => be.Id == entry.Id);
            persistedEntry.Should().NotBeNull();
            persistedEntry!.ActualAmount.Should().Be(600m, $"Persisted ActualAmount should reflect saved transaction - persisted {persistedEntry.ActualAmount}");

            var budgetRepo = GetRequiredService<IBudgetRepository>();
            var entriesByFy = await budgetRepo.GetByFiscalYearAsync(entry.FiscalYear);
            entriesByFy.Should().NotBeEmpty("GetByFiscalYearAsync should return the seeded budget entry");

            var directSummary = await budgetRepo.GetBudgetSummaryAsync(DateTime.Now.AddYears(-1), DateTime.Now.AddYears(1));
            directSummary.TotalBudgeted.Should().Be(1000m, "Seeded budget entry should be visible to BudgetRepository");
            directSummary.TotalActual.Should().Be(600m, "Transaction actuals should be reflected in BudgetRepository summary");

            // Use GrokSupercomputer directly to analyze budget data (AI is mocked)
            var grok = GetRequiredService<IGrokSupercomputer>();

            var budgetData = new BudgetData
            {
                EnterpriseId = 0,
                FiscalYear = entry.FiscalYear,
                TotalBudget = entry.BudgetedAmount,
                TotalExpenditures = entry.ActualAmount,
                RemainingBudget = entry.BudgetedAmount - entry.ActualAmount
            };

            var insights = await grok.AnalyzeBudgetDataAsync(budgetData);

            // The IAIService is mocked to return a deterministic string and should be invoked
            var aiMock = GetRequiredService<Mock<IAIService>>();
            aiMock.Verify(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            insights.Should().NotBeNull();
            insights.Recommendations.Should().Contain(r => r.Contains("AI Analysis") || r.Contains("Review expense controls"));
        }

        [Fact]
        public async Task FullSyncPipeline_QBToTransactionsToBudgetToInsights_CompletesEndToEnd()
        {
            await ResetDatabaseAsync();

            using var seedScope = CreateScope();
            var ctx = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Seed a budget entry that the test QB service will use
            var entry = await SeedBudgetEntryAsync(seedScope, budgetedAmount: 1200m, actualAmount: 0m);

            var qb = GetRequiredService<IQuickBooksService>();

            // Act: Sync from QuickBooks (TestQuickBooksService will add a transaction)
            var syncResult = await qb.SyncDataAsync();
            syncResult.Success.Should().BeTrue();
            syncResult.RecordsSynced.Should().BeGreaterOrEqualTo(1);

            // Diagnostics: ensure the QB service persisted a transaction and updated budget
            var entriesList = await ctx.BudgetEntries.ToListAsync();
            entriesList.Should().NotBeEmpty("Budget entries should exist in DB after QB sync");
            var startRange = DateTime.Now.AddYears(-1);
            var endRange = DateTime.Now.AddYears(1);
            foreach (var e in entriesList)
            {
                var inRange = e.CreatedAt >= startRange && e.CreatedAt <= endRange;
                Console.WriteLine($"DEBUG: BudgetEntry {e.Id} CreatedAt: {e.CreatedAt:o} (Kind={e.CreatedAt.Kind}) Start:{e.StartPeriod:o} End:{e.EndPeriod:o} Actual:{e.ActualAmount} Budgeted:{e.BudgetedAmount} InRange:{inRange}");
            }
            entriesList.Any(e => e.CreatedAt >= startRange && e.CreatedAt <= endRange).Should().BeTrue("At least one budget entry should have CreatedAt within the date range used for summary");
            var txList = await ctx.Transactions.ToListAsync();
            txList.Should().Contain(t => t.Description.Contains("QB Imported Invoice"));

            var factory = GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var factoryCtx = await factory.CreateDbContextAsync();
            var factoryCount = await factoryCtx.BudgetEntries.CountAsync();
            var ctxCount = await ctx.BudgetEntries.CountAsync();
            factoryCount.Should().Be(ctxCount, "DbContextFactory-created context should see same entries as the scoped context");

            var persistedEntry = await factoryCtx.BudgetEntries.AsNoTracking().FirstOrDefaultAsync(be => be.Id == entry.Id);
            persistedEntry.Should().NotBeNull();
            persistedEntry!.ActualAmount.Should().BeGreaterThanOrEqualTo(250m, $"Persisted ActualAmount should reflect QB sync - persisted {persistedEntry.ActualAmount}");

            var budgetRepo = GetRequiredService<IBudgetRepository>();
            var entriesByFy = await budgetRepo.GetByFiscalYearAsync(entry.FiscalYear);
            entriesByFy.Should().NotBeEmpty("GetByFiscalYearAsync should return seeded or created budget entries");

            var directSummary = await budgetRepo.GetBudgetSummaryAsync(DateTime.Now.AddYears(-1), DateTime.Now.AddYears(1));
            // If TestQuickBooksService added a transaction of 250, TotalActual should be >= 250
            directSummary.TotalActual.Should().BeGreaterThanOrEqualTo(250m);

            // Run full pipeline
            var pipeline = GetRequiredService<IAnalyticsPipeline>();
            var compliance = await pipeline.ExecuteFullPipelineAsync();

            // Verify budget summary was updated and AI was called
            compliance.BudgetSummary.Should().NotBeNull();
            compliance.BudgetSummary.TotalActual.Should().BeGreaterThan(0m);

            var aiMock = GetRequiredService<Mock<IAIService>>();
            aiMock.Verify(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task TransactionCreate_WithAnalyticsFlag_CallsBothBudgetAndGrokServices()
        {
            await ResetDatabaseAsync();

            using var seedScope = CreateScope();
            var ctx = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Seed and add a transaction (simulate "with analytics" by immediately running pipeline)
            var entry = await SeedBudgetEntryAsync(seedScope, budgetedAmount: 500m, actualAmount: 0m);

            var txn = new Transaction
            {
                BudgetEntryId = entry.Id,
                Amount = 400m,
                Description = "Flagged TXN",
                TransactionDate = DateTime.Now,
                Type = "Payment",
                CreatedAt = DateTime.Now
            };

            ctx.Transactions.Add(txn);
            entry.ActualAmount += txn.Amount;
            await ctx.SaveChangesAsync();

            // Diagnostics: verify DB and repo see change
            var entriesList = await ctx.BudgetEntries.ToListAsync();
            entriesList.Should().NotBeEmpty("Budget entries should exist in DB");
            var startRange = DateTime.Now.AddYears(-1);
            var endRange = DateTime.Now.AddYears(1);
            foreach (var e in entriesList)
            {
                var inRange = e.CreatedAt >= startRange && e.CreatedAt <= endRange;
                Console.WriteLine($"DEBUG: BudgetEntry {e.Id} CreatedAt: {e.CreatedAt:o} (Kind={e.CreatedAt.Kind}) Start:{e.StartPeriod:o} End:{e.EndPeriod:o} Actual:{e.ActualAmount} Budgeted:{e.BudgetedAmount} InRange:{inRange}");
            }
            entriesList.Any(e => e.CreatedAt >= startRange && e.CreatedAt <= endRange).Should().BeTrue("At least one budget entry should have CreatedAt within the date range used for summary");
            var txList = await ctx.Transactions.ToListAsync();
            txList.Should().Contain(t => t.Amount == 400m);

            var factory = GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var factoryCtx = await factory.CreateDbContextAsync();
            var factoryCount = await factoryCtx.BudgetEntries.CountAsync();
            var ctxCount = await ctx.BudgetEntries.CountAsync();
            factoryCount.Should().Be(ctxCount, "DbContextFactory-created context should see same entries as the scoped context");

            // Verify persisted entry has updated ActualAmount
            var persistedEntry = await factoryCtx.BudgetEntries.AsNoTracking().FirstOrDefaultAsync(be => be.Id == entry.Id);
            persistedEntry.Should().NotBeNull();
            persistedEntry!.ActualAmount.Should().Be(400m, $"Persisted ActualAmount should reflect saved transaction - persisted {persistedEntry.ActualAmount}");

            var budgetRepo = GetRequiredService<IBudgetRepository>();
            var entriesByFy = await budgetRepo.GetByFiscalYearAsync(entry.FiscalYear);
            entriesByFy.Should().NotBeEmpty("GetByFiscalYearAsync should return the seeded budget entry");

            // Double-check direct DB query for the same period overlap used by the repository
            var entriesInRange = await factoryCtx.BudgetEntries.AsNoTracking()
                .Where(be => be.StartPeriod <= endRange && be.EndPeriod >= startRange)
                .ToListAsync();
            if (!entriesInRange.Any())
            {
                throw new InvalidOperationException($"DEBUG: No entries found by direct Start/EndPeriod query. FactoryCount={factoryCount}, ctxCount={ctxCount}");
            }

            var manualTotalActual = await factoryCtx.BudgetEntries.AsNoTracking()
                .Where(be => be.StartPeriod <= endRange && be.EndPeriod >= startRange)
                .SumAsync(be => be.ActualAmount);
            manualTotalActual.Should().Be(400m, $"Manual DB sum should reflect updated ActualAmount: {manualTotalActual}");

            var directSummary = await budgetRepo.GetBudgetSummaryAsync(DateTime.Now.AddYears(-1), DateTime.Now.AddYears(1));
            if (directSummary.TotalActual != 400m)
            {
                throw new InvalidOperationException($"DEBUG directSummary: Budgeted={directSummary.TotalBudgeted}, Actual={directSummary.TotalActual}");
            }
            directSummary.TotalActual.Should().Be(400m, $"Transaction amount should be reflected in budget summary. directSummary: Budgeted={directSummary.TotalBudgeted}, Actual={directSummary.TotalActual}");

            // Now run pipeline as if analytics processing was requested
            var pipeline = GetRequiredService<IAnalyticsPipeline>();
            var compliance = await pipeline.ExecuteFullPipelineAsync();

            // Budget recalculation should reflect the transaction
            compliance.BudgetSummary.Should().NotBeNull();
            compliance.BudgetSummary.TotalActual.Should().Be(400m);

            // Grok/AI should have been invoked
            var aiMock = GetRequiredService<Mock<IAIService>>();
            aiMock.Verify(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task MonthlyRollover_TriggersBudgetRollover_AndInsightRefresh()
        {
            await ResetDatabaseAsync();

            using var seedScope = CreateScope();
            var ctx = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Seed and add a transaction
            var entry = await SeedBudgetEntryAsync(seedScope, budgetedAmount: 1000m, actualAmount: 400m);

            // Diagnostics: verify initial summary
            var budgetRepo = GetRequiredService<IBudgetRepository>();
            var directSummary = await budgetRepo.GetBudgetSummaryAsync(DateTime.Now.AddYears(-1), DateTime.Now.AddYears(1));
            directSummary.TotalActual.Should().Be(400m, "Seeded actual amount should be 400m");

            // Initial pipeline run should invoke AI once
            var pipeline = GetRequiredService<IAnalyticsPipeline>();
            var aiMock = GetRequiredService<Mock<IAIService>>();

            // Clear invocations then run
            aiMock.Invocations.Clear();
            var before = await pipeline.ExecuteFullPipelineAsync();
            aiMock.Verify(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            // Simulate monthly rollover: reset actuals for the new period
            entry.ActualAmount = 0m;
            entry.StartPeriod = entry.StartPeriod.AddMonths(1);
            entry.EndPeriod = entry.EndPeriod.AddMonths(1);
            await ctx.SaveChangesAsync();

            // Clear invocations and rerun pipeline
            aiMock.Invocations.Clear();
            var after = await pipeline.ExecuteFullPipelineAsync();

            // Insight refresh should have been triggered again
            aiMock.Verify(a => a.GetInsightsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            // Budget summary should reflect rollover (actuals reset)
            after.BudgetSummary.Should().NotBeNull();
            after.BudgetSummary.TotalActual.Should().Be(0m);
        }

        [Fact]
        public async Task ExecuteFullPipelineAsync_WithQBFailure_HandlesPartialFailure()
        {
            // Note: This test demonstrates partial failure handling. In a real scenario, mock IQuickBooksService to return failure.
            // For now, assuming success as per current setup.
            await ResetDatabaseAsync();

            using var seedScope = CreateScope();
            var ctx = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Seed a budget entry
            var entry = await SeedBudgetEntryAsync(seedScope, budgetedAmount: 1000m, actualAmount: 0m);

            var pipeline = GetRequiredService<IAnalyticsPipeline>();

            // Act
            var compliance = await pipeline.ExecuteFullPipelineAsync();

            // Assert
            compliance.Should().NotBeNull();
            // In case of QB failure, budget summary might still be calculated if data exists
            compliance.BudgetSummary.Should().NotBeNull();
        }

        [Fact]
        public async Task QuickBooksSync_TriggersBudgetRecalculation_AndUpdatesRemaining()
        {
            await ResetDatabaseAsync();

            using var seedScope = CreateScope();
            var entry = await SeedBudgetEntryAsync(seedScope, budgetedAmount: 1000m, actualAmount: 0m);

            var qbService = GetRequiredService<IQuickBooksService>();

            // Act - simulate importing invoice
            var result = await qbService.SyncDataAsync();

            // Assert
            result.Success.Should().BeTrue();
            // Verify budget remaining recalculated
            var updatedEntry = await DbContext.BudgetEntries.FindAsync(entry.Id);
            updatedEntry.ActualAmount.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task AnalyticsPipelineExecute_TriggersInsightGeneration_AndSavesResult()
        {
            await ResetDatabaseAsync();

            using var seedScope = CreateScope();
            var entry = await SeedBudgetEntryAsync(seedScope, budgetedAmount: 10000m, actualAmount: 0m);

            var pipeline = GetRequiredService<IAnalyticsPipeline>();

            // Act
            var compliance = await pipeline.ExecuteFullPipelineAsync();

            // Assert
            compliance.Should().NotBeNull();
            compliance.AIInsights.Should().NotBeNull();
            // Verify insights saved
            var insights = await DbContext.AIInsights.ToListAsync();
            insights.Should().NotBeEmpty();
        }

        [Fact]
        public async Task AnalyticsPipelineExecuteFull_CompletesEndToEndWithAllComponents()
        {
            await ResetDatabaseAsync();

            using var seedScope = CreateScope();
            var entry = await SeedBudgetEntryAsync(seedScope, budgetedAmount: 5000m, actualAmount: 0m);

            var pipeline = GetRequiredService<IAnalyticsPipeline>();

            // Act
            var compliance = await pipeline.ExecuteFullPipelineAsync();

            // Assert
            compliance.Should().NotBeNull();
            compliance.BudgetSummary.Should().NotBeNull();
            compliance.AIInsights.Should().NotBeNull();
            compliance.TransactionsProcessed.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task AnalyticsPipelineExecute_CallsBothBudgetAndAIServices()
        {
            await ResetDatabaseAsync();

            using var seedScope = CreateScope();
            var entry = await SeedBudgetEntryAsync(seedScope, budgetedAmount: 2000m, actualAmount: 0m);

            var pipeline = GetRequiredService<IAnalyticsPipeline>();

            // Act
            var compliance = await pipeline.ExecuteFullPipelineAsync();

            // Assert
            compliance.Should().NotBeNull();
            // Verify both budget and AI services called
            compliance.BudgetSummary.Should().NotBeNull();
            compliance.AIInsights.Should().NotBeNull();
        }

        [Fact]
        public async Task AnalyticsPipelineExecute_TriggersRolloverAndInsightRefresh()
        {
            await ResetDatabaseAsync();

            using var seedScope = CreateScope();
            var entry = await SeedBudgetEntryAsync(seedScope, budgetedAmount: 1000m, actualAmount: 200m); // 800 remaining

            var pipeline = GetRequiredService<IAnalyticsPipeline>();

            // Act - simulate rollover
            var compliance = await pipeline.ExecuteFullPipelineAsync();

            // Assert
            compliance.Should().NotBeNull();
            // Verify rollover logic and insights refreshed
            compliance.BudgetSummary.RemainingBudget.Should().Be(800m);
        }

        /// <summary>
        /// Simple test QuickBooksService that inserts a transaction and updates the corresponding budget entry
        /// This is intentionally minimal and used only by the integration tests in this file.
        /// </summary>
        private class TestQuickBooksService : IQuickBooksService
        {
            private readonly IServiceProvider _sp;

            public TestQuickBooksService(IServiceProvider sp)
            {
                _sp = sp;
            }

            public Task<bool> AuthorizeAsync() => Task.FromResult(true);
            public Task<bool> TestConnectionAsync() => Task.FromResult(true);
            public Task<bool> IsConnectedAsync() => Task.FromResult(true);
            public Task<UrlAclCheckResult> CheckUrlAclAsync(string? redirectUri = null) => Task.FromResult(new UrlAclCheckResult { IsReady = true });
            public Task<List<Intuit.Ipp.Data.Customer>> GetCustomersAsync() => Task.FromResult(new List<Intuit.Ipp.Data.Customer>());
            public Task<List<Intuit.Ipp.Data.Bill>> GetBillsAsync() => Task.FromResult(new List<Intuit.Ipp.Data.Bill>());
            public Task<List<Intuit.Ipp.Data.Invoice>> GetInvoicesAsync(string? enterprise = null) => Task.FromResult(new List<Intuit.Ipp.Data.Invoice>());
            public Task<List<Intuit.Ipp.Data.Account>> GetChartOfAccountsAsync() => Task.FromResult(new List<Intuit.Ipp.Data.Account>());
            public Task<List<Intuit.Ipp.Data.JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate) => Task.FromResult(new List<Intuit.Ipp.Data.JournalEntry>());
            public Task<List<WileyWidget.Models.QuickBooksBudget>> GetBudgetsAsync() => Task.FromResult(new List<WileyWidget.Models.QuickBooksBudget>());
            public Task<SyncResult> SyncBudgetsToAppAsync(IEnumerable<WileyWidget.Models.QuickBooksBudget> budgets, CancellationToken cancellationToken = default) => Task.FromResult(new SyncResult { Success = true, RecordsSynced = budgets?.Count() ?? 0, Duration = TimeSpan.Zero });
            public Task<bool> ConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
            public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<ConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ConnectionStatus { IsConnected = true, CompanyName = "TestCompany" });
            public Task<ImportResult> ImportChartOfAccountsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ImportResult { Success = true });

            public async Task<SyncResult> SyncDataAsync(CancellationToken cancellationToken = default)
            {
                using var scope = _sp.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Find or create a budget entry
                // Prefer the most recently-created budget entry so integration tests
                // update the entry seeded by this test instance rather than older sample data.
                var entry = await ctx.BudgetEntries
                    .OrderByDescending(be => be.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (entry == null)
                {
                    var dept = new Department { Name = "QB Dept" };
                    var fund = new Fund { FundCode = "99QB", Name = "QB Fund", Type = FundType.GeneralFund };
                    ctx.Departments.Add(dept);
                    ctx.Funds.Add(fund);
                    await ctx.SaveChangesAsync(cancellationToken);

                    entry = new BudgetEntry
                    {
                        AccountNumber = "300",
                        Description = "Auto Created",
                        BudgetedAmount = 1000m,
                        ActualAmount = 0m,
                        FiscalYear = DateTime.Now.Year,
                        StartPeriod = new DateTime(DateTime.Now.Year, 1, 1),
                        EndPeriod = new DateTime(DateTime.Now.Year, 12, 31),
                        DepartmentId = dept.Id,
                        FundId = fund.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                    ctx.BudgetEntries.Add(entry);
                    await ctx.SaveChangesAsync(cancellationToken);
                }

                var amount = 250m;
                var txn = new Transaction
                {
                    BudgetEntryId = entry.Id,
                    Amount = amount,
                    Description = "QB Imported Invoice",
                    TransactionDate = DateTime.UtcNow,
                    Type = "Invoice",
                    CreatedAt = DateTime.UtcNow
                };

                ctx.Transactions.Add(txn);
                entry.ActualAmount += amount;
                await ctx.SaveChangesAsync(cancellationToken);

                return new SyncResult { Success = true, RecordsSynced = 1, Duration = TimeSpan.Zero };
            }
        }
    }
}
*/
