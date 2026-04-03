using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using IppAccount = Intuit.Ipp.Data.Account;
using IppJournalEntry = Intuit.Ipp.Data.JournalEntry;
using IppJournalEntryLineDetail = Intuit.Ipp.Data.JournalEntryLineDetail;
using IppLine = Intuit.Ipp.Data.Line;
using IppPostingTypeEnum = Intuit.Ipp.Data.PostingTypeEnum;
using IppReferenceType = Intuit.Ipp.Data.ReferenceType;
using WileyWidget.Business.Interfaces;
using WileyWidget.Business.Services;
using WileyWidget.Data;
using WileyWidget.LayerProof.Tests.Data.E2E;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.LayerProof.Tests.Business;

[Trait("Category", "Business")]
[Trait("Category", "LayerProof")]
[Trait("Category", "E2E")]
public sealed class QuickBooksBudgetSyncServiceDockerProofTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public QuickBooksBudgetSyncServiceDockerProofTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SyncFiscalYearActualsAsync_Updates_Budget_Actuals_And_Publishes_Event()
    {
        await _fixture.ResetDatabaseAsync();

        await using var provider = BuildProvider(_fixture.ConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var fiscalYear = 2026;

        var seeded = await SeedBudgetSyncDataAsync(db, fiscalYear);

        var quickBooks = new Mock<IQuickBooksService>(MockBehavior.Strict);
        quickBooks.Setup(service => service.GetChartOfAccountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IppAccount>
            {
                new IppAccount { Id = "acct-expense", AcctNum = seeded.ExpenseAccountNumber, Name = "Layer Proof Expense" },
                new IppAccount { Id = "acct-revenue", AcctNum = "410.1", Name = "Layer Proof Revenue" },
            });
        quickBooks.Setup(service => service.GetJournalEntriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IppJournalEntry>
            {
                CreateJournalEntry("acct-expense", "Layer Proof Expense", 150m, IppPostingTypeEnum.Debit),
                CreateJournalEntry("acct-expense", "Layer Proof Expense", 25m, IppPostingTypeEnum.Credit),
                CreateJournalEntry("acct-revenue", "Layer Proof Revenue", 999m, IppPostingTypeEnum.Debit),
            });

        var eventBus = new Mock<IAppEventBus>(MockBehavior.Strict);
        eventBus.Setup(bus => bus.Publish(It.Is<BudgetActualsUpdatedEvent>(evt => evt.FiscalYear == fiscalYear && evt.UpdatedCount == 1)));

        var service = new QuickBooksBudgetSyncService(
            quickBooks.Object,
            services.GetRequiredService<IBudgetRepository>(),
            NullLogger<QuickBooksBudgetSyncService>.Instance,
            new ConfigurationBuilder().Build(),
            eventBus.Object);

        var updatedCount = await service.SyncFiscalYearActualsAsync(fiscalYear);

        updatedCount.Should().Be(1);

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refreshedEntry = await verificationDb.BudgetEntries.AsNoTracking().SingleAsync(entry => entry.Id == seeded.BudgetEntryId);
        refreshedEntry.ActualAmount.Should().Be(125m);
        refreshedEntry.Variance.Should().Be(refreshedEntry.BudgetedAmount - 125m);
        refreshedEntry.UpdatedAt.Should().NotBeNull();

        quickBooks.VerifyAll();
        eventBus.VerifyAll();
    }

    [Fact]
    public async Task SyncFiscalYearActualsAsync_Returns_Zero_When_No_Expense_Lines_Match()
    {
        await _fixture.ResetDatabaseAsync();

        await using var provider = BuildProvider(_fixture.ConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var fiscalYear = 2026;

        var seeded = await SeedBudgetSyncDataAsync(db, fiscalYear);

        var quickBooks = new Mock<IQuickBooksService>(MockBehavior.Strict);
        quickBooks.Setup(service => service.GetChartOfAccountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IppAccount>
            {
                new IppAccount { Id = "acct-revenue", AcctNum = "410.1", Name = "Layer Proof Revenue" },
            });
        quickBooks.Setup(service => service.GetJournalEntriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IppJournalEntry>
            {
                CreateJournalEntry("acct-revenue", "Layer Proof Revenue", 999m, IppPostingTypeEnum.Debit),
            });

        var eventBus = new Mock<IAppEventBus>(MockBehavior.Strict);

        var service = new QuickBooksBudgetSyncService(
            quickBooks.Object,
            services.GetRequiredService<IBudgetRepository>(),
            NullLogger<QuickBooksBudgetSyncService>.Instance,
            new ConfigurationBuilder().Build(),
            eventBus.Object);

        var updatedCount = await service.SyncFiscalYearActualsAsync(fiscalYear);

        updatedCount.Should().Be(0);

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refreshedEntry = await verificationDb.BudgetEntries.AsNoTracking().SingleAsync(entry => entry.Id == seeded.BudgetEntryId);
        refreshedEntry.ActualAmount.Should().Be(0m);
        refreshedEntry.Variance.Should().Be(0m);

        quickBooks.VerifyAll();
        eventBus.Verify(bus => bus.Publish(It.IsAny<BudgetActualsUpdatedEvent>()), Times.Never);
    }

    [Fact]
    public async Task SyncFiscalYearActualsAsync_Maps_Lines_By_Account_Name_When_Id_Does_Not_Match()
    {
        await _fixture.ResetDatabaseAsync();

        await using var provider = BuildProvider(_fixture.ConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var fiscalYear = 2026;

        var seeded = await SeedBudgetSyncDataAsync(db, fiscalYear);

        var quickBooks = new Mock<IQuickBooksService>(MockBehavior.Strict);
        quickBooks.Setup(service => service.GetChartOfAccountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IppAccount>
            {
                new IppAccount { Id = "acct-expense", AcctNum = seeded.ExpenseAccountNumber, Name = "Layer Proof Expense" },
            });
        quickBooks.Setup(service => service.GetJournalEntriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IppJournalEntry>
            {
                CreateJournalEntry(string.Empty, "Layer Proof Expense", 80m, IppPostingTypeEnum.Debit),
            });

        var eventBus = new Mock<IAppEventBus>(MockBehavior.Strict);
        eventBus.Setup(bus => bus.Publish(It.Is<BudgetActualsUpdatedEvent>(evt => evt.FiscalYear == fiscalYear && evt.UpdatedCount == 1)));

        var service = new QuickBooksBudgetSyncService(
            quickBooks.Object,
            services.GetRequiredService<IBudgetRepository>(),
            NullLogger<QuickBooksBudgetSyncService>.Instance,
            new ConfigurationBuilder().Build(),
            eventBus.Object);

        var updatedCount = await service.SyncFiscalYearActualsAsync(fiscalYear);

        updatedCount.Should().Be(1);

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refreshedEntry = await verificationDb.BudgetEntries.AsNoTracking().SingleAsync(entry => entry.Id == seeded.BudgetEntryId);
        refreshedEntry.ActualAmount.Should().Be(80m);
        refreshedEntry.Variance.Should().Be(refreshedEntry.BudgetedAmount - 80m);

        quickBooks.VerifyAll();
        eventBus.VerifyAll();
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Warning);
        });
        services.AddMemoryCache();
        services.AddSingleton(Mock.Of<ITelemetryService>());

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        services.AddDbContextFactory<AppDbContext>(options => options.UseSqlServer(connectionString), ServiceLifetime.Scoped);
        services.AddScoped<IBudgetRepository, BudgetRepository>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
    }

    private static async Task<SeededBudgetSyncData> SeedBudgetSyncDataAsync(AppDbContext db, int fiscalYear)
    {
        var uniqueToken = $"BUS{Guid.NewGuid():N}";
        var periodStart = new DateTime(fiscalYear, 1, 1);
        var periodEnd = new DateTime(fiscalYear, 12, 31);

        var department = new Department
        {
            Name = $"Business Proof Department {uniqueToken}",
            DepartmentCode = $"BP-{uniqueToken[..8]}",
        };

        var fund = new Fund
        {
            FundCode = $"200-{uniqueToken[..4]}",
            Name = $"Business Proof Fund {uniqueToken}",
            Type = FundType.GeneralFund,
        };

        var budgetPeriod = new BudgetPeriod
        {
            Year = fiscalYear,
            Name = $"{fiscalYear} Business Proof",
            CreatedDate = DateTime.UtcNow,
            StartDate = periodStart,
            EndDate = periodEnd,
            Status = BudgetStatus.Adopted,
            IsActive = true,
        };

        db.Departments.Add(department);
        db.Funds.Add(fund);
        db.BudgetPeriods.Add(budgetPeriod);
        await db.SaveChangesAsync();

        var account = new MunicipalAccount
        {
            DepartmentId = department.Id,
            BudgetPeriodId = budgetPeriod.Id,
            FundId = fund.Id,
            AccountNumber = new AccountNumber("510.1"),
            Name = $"Business Proof Expense Account {uniqueToken}",
            Type = AccountType.Services,
            TypeDescription = "Services",
            FundType = MunicipalFundType.General,
            FundDescription = "General Fund",
            Balance = 0m,
            BudgetAmount = 5000m,
        };

        db.MunicipalAccounts.Add(account);
        await db.SaveChangesAsync();

        var budgetEntry = new BudgetEntry
        {
            AccountNumber = account.AccountNumber!.Value,
            Description = $"Business Proof Budget Entry {uniqueToken}",
            BudgetedAmount = 1000m,
            ActualAmount = 0m,
            Variance = 0m,
            FiscalYear = fiscalYear,
            StartPeriod = periodStart,
            EndPeriod = periodEnd,
            FundType = FundType.GeneralFund,
            EncumbranceAmount = 0m,
            DepartmentId = department.Id,
            FundId = fund.Id,
            MunicipalAccountId = account.Id,
        };

        db.BudgetEntries.Add(budgetEntry);
        await db.SaveChangesAsync();

        return new SeededBudgetSyncData(budgetEntry.Id, account.AccountNumber.Value);
    }

    private static IppJournalEntry CreateJournalEntry(string accountId, string accountName, decimal amount, IppPostingTypeEnum postingType)
    {
        return new IppJournalEntry
        {
            Line =
            [
                new IppLine
                {
                    Amount = amount,
                    AnyIntuitObject = new IppJournalEntryLineDetail
                    {
                        AccountRef = new IppReferenceType
                        {
                            Value = accountId,
                            name = accountName,
                        },
                        PostingType = postingType,
                    },
                },
            ],
        };
    }

    private sealed record SeededBudgetSyncData(int BudgetEntryId, string ExpenseAccountNumber);
}
